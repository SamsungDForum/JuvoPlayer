/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
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
using System.Threading;
using System.Threading.Tasks;
using FFmpegBindings.Interop;
using JuvoLogger;
using JuvoPlayer.Common;
using Nito.AsyncEx;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public class FFmpegDemuxer : IDemuxer
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly IFFmpegGlue _ffmpegGlue;
        private int _audioIdx = -1;
        private CancellationTokenSource _cancellationTokenSource;
        private TaskCompletionSource<bool> _completionSource;
        private IAvFormatContext _formatContext;
        private IAvioContext _ioContext;
        private bool _nextPacketPending;
        private AsyncContextThread _thread;
        private int _videoIdx = -1;
        private IDemuxerClient _demuxerClient;

        public FFmpegDemuxer(IFFmpegGlue ffmpegGlue)
        {
            _ffmpegGlue = ffmpegGlue;
        }

        private static ulong BufferSize { get; } =
            64 * 1024; // 32kB seems to be "low level standard", but content downloading pipeline works better for 64kB

        private static int ProbeSize { get; } =
            32 * 1024; // higher values may cause problems when probing certain kinds of content (assert "len >= s->orig_buffer_size" in aviobuf)

        private static TimeSpan MaxAnalyzeDuration { get; } = TimeSpan.FromSeconds(10);

        public bool IsInitialized()
        {
            return _thread != null;
        }

        public Task<ClipConfiguration> InitForUrl(string url)
        {
            if (IsInitialized())
                throw new InvalidOperationException("Initialization already started");

            InitThreadingPrimitives();
            return _thread.Factory.StartNew(() => InitDemuxer(() => InitUrl(url)));
        }

        public Task<ClipConfiguration> InitForEs()
        {
            if (IsInitialized())
                throw new InvalidOperationException("Initialization already started");

            InitThreadingPrimitives();
            _demuxerClient.Initialize();
            return _thread.Factory.StartNew(() => InitDemuxer(InitEs));
        }

        public async Task<Packet> NextPacket(TimeSpan? minPts = null)
        {
            if (!IsInitialized())
                throw new InvalidOperationException();

            _nextPacketPending = true;
            var packet = await _thread.Factory.StartNew(() =>
            {
                var streamIndexes = new[] {_audioIdx, _videoIdx};
                Packet nextPacket;
                do
                {
                    nextPacket = _formatContext.NextPacket(streamIndexes);
                } while (nextPacket != null &&
                         minPts.HasValue && nextPacket.Pts < minPts);

                return nextPacket;
            });
            if (packet == null)
                _completionSource?.TrySetResult(true);
            _nextPacketPending = false;
            return packet;
        }

        public void SetClient(IDemuxerClient client)
        {
            _demuxerClient = client;
        }

        public Task Completion => _completionSource?.Task;

        public void Complete()
        {
            _demuxerClient?.CompleteAdding();
            if (_demuxerClient?.GetSegmentBuffers() != null && !_nextPacketPending)
                _completionSource?.TrySetResult(true);
        }

        public void Reset()
        {
            _completionSource?.TrySetResult(true);
            _cancellationTokenSource?.Cancel();
            _thread?.Factory?.Run(DeallocFFmpeg);
            _thread?.Join();
            _thread = null;
            _demuxerClient.Reset();
        }

        public Task<TimeSpan> Seek(TimeSpan time, CancellationToken token)
        {
            if (!IsInitialized())
                throw new InvalidOperationException();

            return _thread.Factory.StartNew(() =>
            {
                var index = _audioIdx;
                if (_videoIdx != -1)
                    index = _videoIdx;
                _formatContext.Seek(index, time);
                return time;
            }, token);
        }

        public void Dispose()
        {
            Reset();
        }

        private void InitThreadingPrimitives()
        {
            _thread = new AsyncContextThread();
            _cancellationTokenSource = new CancellationTokenSource();
            _completionSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private void InitUrl(string url)
        {
            try
            {
                _formatContext = _ffmpegGlue.AllocFormatContext();
                _formatContext.ProbeSize = ProbeSize;
                _formatContext.MaxAnalyzeDuration = TimeSpan.FromSeconds(10);
                _formatContext.Open(url);
            }
            catch (FFmpegException ex)
            {
                Logger.Error(ex);
                throw new DemuxerException("Cannot open formatContext", ex);
            }
        }

        private ClipConfiguration InitDemuxer(Action initAction)
        {
            _ffmpegGlue.Initialize();

            initAction();

            var clipConfiguration = default(ClipConfiguration);

            FindStreamsInfo();
            ReadDuration(ref clipConfiguration);
            ReadStreamConfigs(ref clipConfiguration);
            ReadContentProtectionConfigs(ref clipConfiguration);

            return clipConfiguration;
        }

        private void ReadDuration(ref ClipConfiguration clipConfiguration)
        {
            if (_formatContext.Duration > TimeSpan.Zero)
                clipConfiguration.Duration = _formatContext.Duration;
        }

        private void ReadStreamConfigs(ref ClipConfiguration clipConfiguration)
        {
            var configs = new List<StreamConfig>();
            ReadAudioConfig(configs);
            ReadVideoConfig(configs);
            clipConfiguration.StreamConfigs = configs;
        }

        private void ReadContentProtectionConfigs(ref ClipConfiguration configuration)
        {
            configuration.DrmInitData = _formatContext.DrmInitData;
        }

        private void InitEs()
        {
            try
            {
                _ioContext = _ffmpegGlue.AllocIoContext(BufferSize, ReadPacket, SeekFun);
                _ioContext.Seekable = true;
                _ioContext.WriteFlag = false;

                _formatContext = _ffmpegGlue.AllocFormatContext();
                _formatContext.ProbeSize = ProbeSize;
                _formatContext.MaxAnalyzeDuration = MaxAnalyzeDuration;
                _formatContext.AvioContext = _ioContext;
                _formatContext.Open();
            }
            catch (FFmpegException ex)
            {
                Logger.Error(ex);
                throw new DemuxerException("Cannot open formatContext", ex);
            }
        }

        private void FindStreamsInfo()
        {
            try
            {
                _formatContext.FindStreamInfo();
                SelectBestStreams();
            }
            catch (FFmpegException ex)
            {
                Logger.Error(ex);
                throw new DemuxerException("Cannot find streams info", ex);
            }
        }

        private void SelectBestStreams()
        {
            _audioIdx = FindBestStream(AVMediaType.AVMEDIA_TYPE_AUDIO);
            _videoIdx = FindBestStream(AVMediaType.AVMEDIA_TYPE_VIDEO);
            if (_audioIdx < 0 && _videoIdx < 0)
                throw new FFmpegException("Neither video nor audio stream found");
            _formatContext.EnableStreams(_audioIdx, _videoIdx);
        }

        private int FindBestStream(AVMediaType mediaType)
        {
            var streamId = _formatContext.FindBestBandwidthStream(mediaType);
            return streamId >= 0 ? streamId : _formatContext.FindBestStream(mediaType);
        }

        private void ReadAudioConfig(ICollection<StreamConfig> configs)
        {
            if (_audioIdx < 0)
                return;

            var config = _formatContext.ReadConfig(_audioIdx);

            Logger.Info("Setting audio stream to " + _audioIdx);
            Logger.Info(config.ToString());

            configs.Add(config);
        }

        private void ReadVideoConfig(ICollection<StreamConfig> configs)
        {
            if (_videoIdx < 0)
                return;

            var config = _formatContext.ReadConfig(_videoIdx);

            Logger.Info("Setting video stream to " + _videoIdx);
            Logger.Info(config.ToString());

            configs.Add(config);
        }

        private ArraySegment<byte> ReadPacket(int size)
        {
            try
            {
                var token = _cancellationTokenSource.Token;
                var data = _demuxerClient.Read(size, token);
                return data;
            }
            catch (TaskCanceledException)
            {
                Logger.Info("Take cancelled");
            }
            catch (InvalidOperationException ex)
            {
                Logger.Warn(ex, ex.StackTrace);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unexpected exception: {ex.GetType()}");
            }

            return default;
        }

        private long SeekFun(long pos, int whence)
        {
            try
            {
                Logger.Info($"Seek pos == {pos}, whence == {whence}");
                if ((whence & FFmpegBindings.Interop.FFmpeg.AVSEEK_SIZE) != 0)
                {
                    return Int32.MaxValue;
                }

                var token = _cancellationTokenSource.Token;
                _demuxerClient.Seek(pos, token);
                return 0;
            }
            catch (TaskCanceledException)
            {
                Logger.Info("Take cancelled");
            }
            catch (InvalidOperationException ex)
            {
                Logger.Warn(ex);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unexpected exception: {ex.GetType()}");
            }

            return default;
        }

        private void DeallocFFmpeg()
        {
            _formatContext?.Dispose();
            _ioContext?.Dispose();
        }
    }
}
