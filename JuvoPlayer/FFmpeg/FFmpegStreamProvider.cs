/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2021, Samsung Electronics Co., Ltd
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
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Demuxers.FFmpeg;

namespace JuvoPlayer.FFmpeg
{
    public class FFmpegStreamProvider : IStreamProvider
    {
        private readonly IClock _clock;
        private readonly string _uri;
        private FFmpegDemuxer _demuxer;
        private ClipConfiguration _clipConfiguration;
        private readonly List<FFmpegStream> _streams;
        private FFmpegStreamHandler _streamHandler;

        public FFmpegStreamProvider(
            IClock clock,
            string uri)
        {
            _clock = clock;
            _uri = uri;
            _streams = new List<FFmpegStream>();
        }

        public async Task<Timeline> Prepare()
        {
            var ffmpegGlue = new FFmpegGlue();
            _demuxer = new FFmpegDemuxer(ffmpegGlue);

            _clipConfiguration = await _demuxer.InitForUrl(_uri);
            var period = new Period(
                0,
                TimeSpan.Zero,
                _clipConfiguration.Duration);

            var timeline = new Timeline(
                new[] { period },
                false);

            _streamHandler = new FFmpegStreamHandler(_demuxer);

            return timeline;
        }

        public StreamGroup[] GetStreamGroups(Period period)
        {
            var streamConfigs = _clipConfiguration.StreamConfigs;
            var streamGroupsCount = streamConfigs.Count;
            var streamGroups = new StreamGroup[streamGroupsCount];
            for (var index = 0; index < streamGroupsCount; index++)
            {
                var streamConfig = streamConfigs[index];
                var streamGroup = CreateStreamGroup(streamConfig);
                streamGroups[index] = streamGroup;
            }

            return streamGroups;
        }

        public Format CreateFormat(StreamConfig streamConfig)
        {
            switch (streamConfig.StreamType())
            {
                case StreamType.Audio:
                    return CreateAudioFormat((FFmpegAudioStreamConfig) streamConfig);
                case StreamType.Video:
                    return CreateVideoFormat((FFmpegVideoStreamConfig) streamConfig);
                case StreamType.Unknown:
                case StreamType.Subtitle:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private StreamGroup CreateStreamGroup(StreamConfig streamConfig)
        {
            var contentType = streamConfig
                .StreamType()
                .ToContentType();
            var format = CreateFormat(streamConfig);
            var streamInfo = new StreamInfo(format);
            var streamInfos = new List<StreamInfo> { streamInfo };
            return new StreamGroup(
                contentType,
                streamInfos);
        }

        private Format CreateAudioFormat(FFmpegAudioStreamConfig streamConfig)
        {
            return Format.CreateAudioSampleFormat(
                streamConfig.Index.ToString(),
                streamConfig.MimeType,
                null,
                (int) streamConfig.BitRate,
                streamConfig.ChannelLayout,
                streamConfig.SampleRate,
                streamConfig.Language);
        }

        private Format CreateVideoFormat(FFmpegVideoStreamConfig streamConfig)
        {
            return Format.CreateVideoSampleFormat(
                streamConfig.Index.ToString(),
                streamConfig.MimeType,
                null,
                (int) streamConfig.BitRate,
                streamConfig.Size.Width,
                streamConfig.Size.Height,
                streamConfig.FrameRate);
        }

        public (StreamGroup[], IStreamSelector[]) GetSelectedStreamGroups()
        {
            var streamCount = _streams.Count;
            var streamGroups = new StreamGroup[streamCount];
            var selectors = new IStreamSelector[streamCount];
            for (var index = 0; index < streamCount; ++index)
            {
                var stream = _streams[index];
                var streamConfig = stream.StreamConfig;
                streamGroups[index] = CreateStreamGroup(streamConfig);
                selectors[index] = stream.StreamSelector;
            }

            return (streamGroups, selectors);
        }

        public IStream CreateStream(
            Period period,
            StreamGroup streamGroup,
            IStreamSelector streamSelector)
        {
            var streamInfo = streamGroup.Streams.Single();
            var format = streamInfo.Format;
            var index = int.Parse(format.Id);
            var streamConfigs =
                _clipConfiguration.StreamConfigs;
            var streamConfig = streamConfigs.FirstOrDefault(
                stream => stream.GetIndex() == index);
            if (streamConfig == null)
                return null;
            var ffmpegStream = new FFmpegStream(
                _clock,
                _streamHandler,
                streamConfig,
                streamSelector);
            _streams.Add(ffmpegStream);
            _streamHandler.TotalNumberOfStreams =
                _streams.Count;
            return ffmpegStream;
        }

        public void UpdateStream(
            IStream stream,
            IStreamSelector streamSelector)
        {
            if (stream is FFmpegStream ffmpegStream)
                ffmpegStream.StreamSelector = streamSelector;
        }

        public void ReleaseStream(IStream stream)
        {
            if (stream is FFmpegStream ffmpegStream)
            {
                _streams.Remove(ffmpegStream);
                ffmpegStream.Dispose();
                _streamHandler.TotalNumberOfStreams =
                    _streams.Count;
            }
        }

        public TimeSpan? GetDuration()
        {
            return _clipConfiguration.Duration;
        }

        public void Dispose()
        {
            _streamHandler?.Dispose();
            _demuxer?.Dispose();
        }
    }
}