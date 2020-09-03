/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Dash.MPD;
using JuvoPlayer.Demuxers;
using Period = JuvoPlayer.Common.Period;

namespace JuvoPlayer.Dash
{
    public class DashStreamProvider : IStreamProvider
    {
        private readonly IClock _clock;
        private readonly Func<IDemuxer> _demuxerFactory;
        private readonly IDownloader _downloader;
        private readonly IManifestLoader _manifestLoader;
        private readonly string _mpdUri;
        private readonly List<DashPeriod> _periods;
        private readonly IThroughputHistory _throughputHistory;
        private readonly List<DashStream> _dashStreams;
        private int _firstPeriodId;
        private Manifest _manifest;

        public DashStreamProvider(IManifestLoader loader,
            IThroughputHistory throughputHistory,
            IDownloader downloader,
            Func<IDemuxer> demuxerFactory,
            IClock clock,
            string mpdUri)
        {
            _manifestLoader = loader;
            _throughputHistory = throughputHistory;
            _downloader = downloader;
            _demuxerFactory = demuxerFactory;
            _clock = clock;
            _mpdUri = mpdUri;
            _periods = new List<DashPeriod>();
            _dashStreams = new List<DashStream>();
        }

        public async Task<Timeline> Prepare()
        {
            var manifest = await _manifestLoader.Load(_mpdUri, CancellationToken.None);
            ProcessManifest(manifest);
            return CreateTimeline(manifest);
        }

        public StreamGroup[] GetStreamGroups(Period period)
        {
            var dashPeriod = _periods.SingleOrDefault(
                p => p.Id == period.Id);
            if (dashPeriod == null)
                throw new ArgumentException("Invalid period");
            return dashPeriod.AvailableStreams;
        }

        public IStream CreateStream(
            Period clientPeriod,
            StreamGroup streamGroup,
            IStreamSelector streamSelector)
        {
            var dashPeriod = _periods.Single(p => p.Id == clientPeriod.Id);
            var adaptationSet = dashPeriod.GetAdaptationSet(streamGroup);
            streamSelector = FixupStreamSelector(
                streamSelector,
                streamGroup);
            var dashStream = new DashStream(
                _throughputHistory,
                _downloader,
                _clock,
                _demuxerFactory.Invoke(),
                streamSelector);
            dashStream.SetAdaptationSet(
                streamGroup,
                adaptationSet,
                _manifest.GetPeriodDuration(dashPeriod.Period));
            _dashStreams.Add(dashStream);
            return dashStream;
        }

        public void UpdateStream(IStream stream, IStreamSelector streamSelector)
        {
            if (stream is DashStream dashStream)
            {
                var streamGroup = dashStream.StreamGroup;
                streamSelector = FixupStreamSelector(
                    streamSelector,
                    streamGroup);
                dashStream.SetStreamSelector(streamSelector);
            }
        }

        public void ReleaseStream(IStream stream)
        {
            if (stream is DashStream dashStream)
                _dashStreams.Remove(dashStream);
        }

        public TimeSpan? GetDuration()
        {
            return _manifest.MediaPresentationDuration;
        }

        private void ProcessManifest(Manifest manifest)
        {
            UpdatePeriods(manifest);
            _manifest = manifest;
        }

        private Timeline CreateTimeline(Manifest manifest)
        {
            var clientPeriods =
                ConvertDashPeriodsToClientPeriods(manifest);
            var timeline = new Timeline(clientPeriods,
                manifest.Dynamic);
            return timeline;
        }

        private void UpdatePeriods(Manifest newManifest)
        {
            var previousManifest = _manifest;
            var newPeriods = newManifest.Periods;
            var previousPeriodCount = _manifest?.Periods.Count ?? 0;
            var newStartTime = newPeriods[0].Start;
            var removedPeriodCount = 0;
            while (removedPeriodCount < previousPeriodCount
                   && previousManifest.Periods[0].Start < newStartTime)
                ++removedPeriodCount;
            for (var index = 0; index < removedPeriodCount; index++)
                _periods.RemoveAt(0);
            _firstPeriodId += removedPeriodCount;
            for (var index = 0; index < newPeriods.Count; ++index)
            {
                if (index == _periods.Count)
                    _periods.Add(new DashPeriod(_firstPeriodId + index));
                var period = newPeriods[index];
                _periods[index].Update(period);
            }
        }

        private Period[] ConvertDashPeriodsToClientPeriods(Manifest manifest)
        {
            var clientPeriods = new Period[_periods.Count];
            for (var index = 0; index < _periods.Count; ++index)
            {
                var period = _periods[index];
                var periodStartTime = period.Period.Start;
                var periodEndTime = index + 1 < _periods.Count
                    ? _periods[index + 1].Period.Start
                    : manifest.MediaPresentationDuration;
                var periodDuration =
                    periodStartTime.HasValue && periodEndTime.HasValue
                        ? periodEndTime - periodStartTime
                        : null;
                var clientPeriod = new Period(period.Id,
                    periodStartTime,
                    periodDuration);
                clientPeriods[index] = clientPeriod;
            }

            return clientPeriods;
        }

        private IStreamSelector FixupStreamSelector(
            IStreamSelector baseStreamSelector,
            StreamGroup streamGroup)
        {
            var contentType = streamGroup.ContentType;
            switch (contentType)
            {
                case ContentType.Video:
                    return baseStreamSelector
                           ?? new ThroughputHistoryStreamSelector(_throughputHistory);
                case ContentType.Audio:
                {
                    var capabilities = Platform.Current.Capabilities;
                    var supportsSeamlessAudioChange =
                        capabilities.SupportsSeamlessAudioChange;

                    if (baseStreamSelector is ThroughputHistoryStreamSelector)
                    {
                        if (!supportsSeamlessAudioChange)
                        {
                            throw new ArgumentException(
                                $"Cannot select {nameof(ThroughputHistoryStreamSelector)} " +
                                "for audio StreamGroup. " +
                                "Platform doesn't support it");
                        }
                    }

                    if (baseStreamSelector != null)
                        return baseStreamSelector;

                    if (supportsSeamlessAudioChange)
                        return new ThroughputHistoryStreamSelector(_throughputHistory);

                    var lastStreamIndex = streamGroup.Streams.Count - 1;
                    return new FixedStreamSelector(lastStreamIndex);
                }

                default:
                    throw new ArgumentException($"{contentType} is not supported");
            }
        }
    }
}