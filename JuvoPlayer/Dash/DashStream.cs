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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Dash.MPD;
using JuvoPlayer.Demuxers;

namespace JuvoPlayer.Dash
{
    public class DashStream : IStream
    {
        private readonly IClock _clock;
        private readonly IDemuxer _demuxer;
        private readonly IDownloader _downloader;
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly IThroughputHistory _throughputHistory;
        private AdaptationSet _adaptationSet;
        private TimeSpan? _periodDuration;
        private TimeSpan _bufferPosition;
        private ClipConfiguration _clipConfiguration;
        private RepresentationWrapper _currentRepresentation;
        private Task _demuxPacketsLoopTask;
        private TaskCompletionSource<StreamConfig> _getStreamConfigTaskCompletionSource;
        private TimeSpan _maxBufferTime;
        private long? _previousSegmentNum;
        private IStreamRenderer _renderer;
        private RepresentationWrapper[] _representations;
        private Segment _segment;

        public DashStream(
            IThroughputHistory throughputHistory,
            IDownloader downloader,
            IClock clock,
            IDemuxer demuxer,
            IStreamSelector streamSelector)
        {
            _throughputHistory = throughputHistory;
            _downloader = downloader;
            _clock = clock;
            _demuxer = demuxer;
            StreamSelector = streamSelector;
            _maxBufferTime = TimeSpan.FromSeconds(8);
        }

        public StreamGroup StreamGroup { get; private set; }

        public IStreamSelector StreamSelector { get; private set; }

        public async Task Prepare()
        {
            if (_representations == null
                || _representations.Length == 0)
                return;

            if (_representations.Any(
                repr =>
                    repr.SegmentIndex != null))
                return;

            var representation = _representations.First();
            var indexChunk = CreateIndexChunk(
                representation,
                CancellationToken.None);
            await indexChunk.Load();
        }

        public Task LoadChunks(
            Segment segment,
            IStreamRenderer renderer,
            CancellationToken token)
        {
            _logger.Info();
            _segment = segment;
            _renderer = renderer;
            _bufferPosition = segment.Start;
            _previousSegmentNum = null;
            _currentRepresentation = null;
            return RunLoadChunksLoop(token);
        }

        public async Task<StreamConfig> GetStreamConfig(CancellationToken cancellationToken)
        {
            _logger.Info();
            var streamConfigs = _clipConfiguration.StreamConfigs;
            if (streamConfigs != null)
            {
                var streamConfig = streamConfigs.Single();
                return streamConfig;
            }

            _getStreamConfigTaskCompletionSource?.SetCanceled();
            _getStreamConfigTaskCompletionSource =
                new TaskCompletionSource<StreamConfig>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => { _getStreamConfigTaskCompletionSource.TrySetCanceled(); }))
            {
                var getStreamConfigTask =
                    _getStreamConfigTaskCompletionSource.Task;
                var result = await getStreamConfigTask;
                return result;
            }
        }

        public TimeSpan GetAdjustedSeekPosition(TimeSpan position)
        {
            foreach (var representation in _representations)
            {
                if (representation.SegmentIndex == null)
                    continue;
                var segmentIndex = representation.SegmentIndex;
                var periodDuration = representation.PeriodDuration;
                var segmentNum = segmentIndex.GetSegmentNum(
                    position,
                    periodDuration);
                return segmentIndex.GetStartTime(segmentNum);
            }

            return position;
        }

        public void Dispose()
        {
        }

        internal void SetAdaptationSet(
            StreamGroup streamGroup,
            AdaptationSet adaptationSet,
            TimeSpan? periodDuration)
        {
            StreamGroup = streamGroup;
            _adaptationSet = adaptationSet;
            _periodDuration = periodDuration;
            var representations =
                _adaptationSet.Representations;
            _representations = representations.Select(
                    repr =>
                        new RepresentationWrapper
                        {
                            Representation = repr,
                            SegmentIndex = repr.GetIndex(),
                            PeriodDuration = periodDuration
                        })
                .ToArray();
        }

        internal void SetStreamSelector(IStreamSelector selector)
        {
            StreamSelector = selector;
        }

        private async Task RunLoadChunksLoop(CancellationToken cancellationToken)
        {
            var contentType = _adaptationSet.ContentType;
            _logger.Info($"{contentType}");
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var bufferPosition = _bufferPosition;
                    var playbackPosition = _segment.ToPlaybackTime(_clock.Elapsed);
                    var bufferedDuration = bufferPosition - playbackPosition;
                    _logger.Info($"{contentType}: {bufferedDuration} = {bufferPosition} - {playbackPosition}");
                    if (bufferedDuration <= _maxBufferTime)
                    {
                        if (!await LoadNextChunk(cancellationToken))
                            break;
                        continue;
                    }

                    var halfBufferTime = TimeSpan.FromTicks(_maxBufferTime.Ticks / 2);
                    var delayTime = bufferedDuration - halfBufferTime;
                    await Task.Delay(delayTime, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"{contentType}");
            }

            _logger.Info($"{contentType}: Load loop finished");

            try
            {
                if (_demuxPacketsLoopTask != null)
                {
                    _logger.Info($"{contentType}: Awaiting Demux loop");
                    await _demuxPacketsLoopTask;
                }
            }
            catch (Exception)
            {
                // ignored
            }
            finally
            {
                if (_demuxPacketsLoopTask != null)
                    _logger.Info($"{contentType}: Demux loop finished");
            }
        }

        private async Task RunDemuxPacketsLoop(CancellationToken cancellationToken)
        {
            var contentType = _adaptationSet.ContentType;
            _logger.Info($"{contentType}");
            try
            {
                var taskCompletionSource = new TaskCompletionSource<bool>();
                using (cancellationToken.Register(() => taskCompletionSource.SetCanceled()))
                {
                    var cancellationTask = taskCompletionSource.Task;
                    var minPts =
                        _adaptationSet.ContentType == ContentType.Audio ? (TimeSpan?) _segment.Start : null;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var completedTask = await Task.WhenAny(
                            _demuxer.NextPacket(minPts),
                            cancellationTask);
                        if (completedTask == cancellationTask)
                            return;
                        var packet = await (Task<Packet>) completedTask;
                        if (packet == null)
                            return;
                        if (packet.Pts > _periodDuration)
                            return;
                        _logger.Info($"{contentType}: Got {packet.StreamType} {packet.Pts}");
                        _renderer.HandlePacket(packet);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"{contentType}");
            }
            finally
            {
                _demuxer.Reset();
            }
        }

        private async Task<bool> LoadNextChunk(CancellationToken cancellationToken)
        {
            var contentType = _adaptationSet.ContentType;
            _logger.Info($"{contentType}");
            var previousRepresentation = _currentRepresentation;
            var representation = SelectRepresentation();
            Task<ClipConfiguration> initDemuxerTask = null;
            if (previousRepresentation != representation)
            {
                _logger.Info();
                await ResetDemuxer();
                cancellationToken.ThrowIfCancellationRequested();
                _currentRepresentation = representation;
                initDemuxerTask = InitDemuxer(
                    representation.InitData,
                    cancellationToken);
            }

            var chunk = GetNextChunk(
                representation,
                cancellationToken,
                _bufferPosition,
                _previousSegmentNum);
            try
            {
                if (chunk != null)
                {
                    _logger.Info();
                    await chunk.Load();
                    cancellationToken.ThrowIfCancellationRequested();
                    await OnChunkLoaded(chunk,
                        representation,
                        initDemuxerTask);
                    return true;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex);
                // Send EOS if we got exception for the last segment
                if (!_previousSegmentNum.HasValue)
                    throw;
                var lastSegmentNum =
                    GetLastSegmentNum(representation);
                if (_previousSegmentNum.Value + 1 != lastSegmentNum)
                    throw;
            }

            await ResetDemuxer();
            cancellationToken.ThrowIfCancellationRequested();
            _logger.Info("Sending EOS packet");
            var streamType = contentType.ToStreamType();
            var eosPacket = new EosPacket(streamType);
            _renderer.HandlePacket(eosPacket);
            return false;
        }

        private RepresentationWrapper SelectRepresentation()
        {
            var selectedIndex = StreamSelector.Select(StreamGroup);
            return _representations[selectedIndex];
        }

        private Task<ClipConfiguration> InitDemuxer(
            IList<byte[]> initData,
            CancellationToken cancellationToken)
        {
            _logger.Info();
            var initTask = _demuxer.InitForEs();
            if (initData != null)
            {
                foreach (var bytes in initData)
                    _demuxer.PushChunk(bytes);
            }

            _demuxPacketsLoopTask =
                RunDemuxPacketsLoop(cancellationToken);
            return initTask;
        }

        private async ValueTask ResetDemuxer()
        {
            _logger.Info();
            if (!_demuxer.IsInitialized())
                return;
            _demuxer.Complete();
            await _demuxer.Completion;
            if (_demuxPacketsLoopTask != null)
                await _demuxPacketsLoopTask;
        }

        private IChunk GetNextChunk(
            RepresentationWrapper representationWrapper,
            CancellationToken cancellationToken,
            TimeSpan bufferPosition,
            long? previousSegmentNum)
        {
            if (representationWrapper.InitData == null)
            {
                return CreateInitializationChunk(
                    representationWrapper,
                    cancellationToken);
            }

            if (representationWrapper.SegmentIndex == null)
            {
                return CreateIndexChunk(
                    representationWrapper,
                    cancellationToken);
            }

            return CreateDataChunk(
                representationWrapper,
                bufferPosition,
                previousSegmentNum,
                cancellationToken);
        }

        private IChunk CreateInitializationChunk(
            RepresentationWrapper representationWrapper,
            CancellationToken cancellationToken)
        {
            var representation = representationWrapper.Representation;
            var baseUrl = representation.BaseUrl;
            var initializationUri = representation.GetInitializationUri();
            var finalUrl = initializationUri.ResolveUri(baseUrl);
            return new InitializationChunk(
                finalUrl,
                initializationUri.Start,
                initializationUri.Length,
                representationWrapper,
                _downloader,
                _throughputHistory,
                _demuxer,
                cancellationToken);
        }

        private IChunk CreateIndexChunk(RepresentationWrapper representationWrapper,
            CancellationToken cancellationToken)
        {
            var representation = representationWrapper.Representation;
            var baseUrl = representation.BaseUrl;
            var indexUri = representation.GetIndexUri();
            var finalUrl = indexUri.ResolveUri(baseUrl);
            return new IndexChunk(
                finalUrl,
                indexUri.Start,
                indexUri.Length,
                representationWrapper,
                _downloader,
                _throughputHistory,
                cancellationToken);
        }

        private IChunk CreateDataChunk(RepresentationWrapper representationWrapper,
            TimeSpan bufferPosition,
            long? previousSegmentNum, CancellationToken cancellationToken)
        {
            var segmentNum = GetNextSegmentNum(
                representationWrapper,
                bufferPosition,
                previousSegmentNum);
            var lastSegmentNum = GetLastSegmentNum(representationWrapper);
            if (segmentNum > lastSegmentNum)
                return null;
            var segmentIndex = representationWrapper.SegmentIndex;
            var segmentUrl =
                segmentIndex.GetSegmentUrl(segmentNum);
            var representation = representationWrapper.Representation;
            var baseUrl = representation.BaseUrl;
            var finalUri = segmentUrl.ResolveUri(baseUrl);
            return new DataChunk(
                finalUri,
                segmentUrl.Start,
                segmentUrl.Length,
                segmentNum,
                _downloader,
                _throughputHistory,
                _demuxer,
                cancellationToken);
        }

        private long GetNextSegmentNum(
            RepresentationWrapper representationWrapper,
            TimeSpan bufferPosition,
            long? previousSegmentNum)
        {
            var segmentIndex = representationWrapper.SegmentIndex;
            if (previousSegmentNum.HasValue)
                return previousSegmentNum.Value + 1;
            return segmentIndex.GetSegmentNum(
                bufferPosition,
                representationWrapper.PeriodDuration);
        }

        private long GetLastSegmentNum(RepresentationWrapper representationWrapper)
        {
            var segmentIndex = representationWrapper.SegmentIndex;
            var periodDuration = representationWrapper.PeriodDuration;
            var firstSegmentNum = segmentIndex.GetFirstSegmentNum();
            var segmentCount = segmentIndex.GetSegmentCount(periodDuration);
            if (!segmentCount.HasValue)
                throw new NotImplementedException("Dynamic manifests are not supported yet");
            return firstSegmentNum + segmentCount.Value - 1;
        }

        private async Task OnChunkLoaded(
            IChunk chunk,
            RepresentationWrapper representationWrapper,
            Task<ClipConfiguration> initDemuxerTask)
        {
            switch (chunk)
            {
                case InitializationChunk _:
                {
                    _logger.Info();
                    // TODO: Handle cases when init is cancelled
                    initDemuxerTask.ContinueWith(clipConfigurationTask =>
                        {
                            _clipConfiguration = clipConfigurationTask.Result;
                            var streamConfigs = _clipConfiguration.StreamConfigs;
                            var streamConfig = streamConfigs.Single();
                            _getStreamConfigTaskCompletionSource?.TrySetResult(streamConfig);
                            var drmInitData = _clipConfiguration.DrmInitData;
                            if (drmInitData != null)
                                _renderer.HandleDrmInitData(_clipConfiguration.DrmInitData);
                        }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnRanToCompletion,
                        TaskScheduler.FromCurrentSynchronizationContext());
                    break;
                }

                case DataChunk dataChunk:
                {
                    var segmentNum = dataChunk.SegmentNum;
                    var segmentIndex = representationWrapper.SegmentIndex;
                    var periodDuration = representationWrapper.PeriodDuration;
                    var segmentStartTime = segmentIndex.GetStartTime(segmentNum);
                    var segmentDuration =
                        segmentIndex.GetDuration(segmentNum, periodDuration);
                    _previousSegmentNum = segmentNum;
                    _bufferPosition = segmentStartTime + segmentDuration.Value;
                    break;
                }
            }
        }
    }
}