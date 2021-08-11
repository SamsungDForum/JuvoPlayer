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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using Nito.AsyncEx;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public class FFmpegDemuxer : IDemuxer
    {
        private readonly IFFmpegGlue _ffmpegGlue;
        private CancellationTokenSource _cancellationTokenSource;
        private TaskCompletionSource<bool> _completionSource;
        private IAvFormatContext _formatContext;
        private IAvioContext _ioContext;
        private AsyncContextThread _thread;
        private IDemuxerDataSource _demuxerDataSource;

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
            return _thread.Factory.StartNew(() => InitDemuxer(InitEs));
        }

        public async Task<Packet> NextPacket(TimeSpan? minPts = null)
        {
            if (!IsInitialized())
                throw new InvalidOperationException();

            var packet = await _thread.Factory.StartNew(() =>
            {
                Packet nextPacket;
                do
                {
                    nextPacket = _formatContext.NextPacket();
                } while (nextPacket != null &&
                         minPts.HasValue && nextPacket.Pts < minPts);

                return nextPacket;
            });
            if (packet == null)
                _completionSource?.TrySetResult(true);
            return packet;
        }

        public void SetClient(IDemuxerDataSource dataSource)
        {
            _demuxerDataSource = dataSource;
        }

        public Task EnableStreams(IList<StreamConfig> configs)
        {
            return _thread.Factory.StartNew(() =>
            {
                var indexes = configs.Select(c => c.GetIndex()).ToList();
                foreach (var index in indexes)
                {
                    Log.Info($"Setting index = {index}");
                }

                _formatContext.EnableStreams(indexes);
            });
        }

        public Task Completion => _completionSource?.Task;

        public void Complete()
        {
            _demuxerDataSource?.CompleteAdding();
        }

        public void Reset()
        {
            _completionSource?.TrySetResult(true);
            _cancellationTokenSource?.Cancel();
            _thread?.Factory?.Run(DeallocFFmpeg);
            _thread?.Join();
            _thread = null;
            _demuxerDataSource.Reset();
        }

        public Task<TimeSpan> Seek(TimeSpan time, CancellationToken token)
        {
            if (!IsInitialized())
                throw new InvalidOperationException();

            return _thread.Factory.StartNew(() =>
            {
                _formatContext.Seek(time);
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
                Log.Error(ex);
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

            for (var i = 0; i < _formatContext.NumberOfStreams; i++)
            {
                try
                {
                    var config = _formatContext.ReadConfig(i);
                    configs.Add(config);
                }
                catch (FFmpegException e)
                {
                    Log.Warn(e, $"Cannot read config with index {i}");
                }
            }

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
                Log.Error(ex);
                throw new DemuxerException("Cannot open formatContext", ex);
            }
        }

        private void FindStreamsInfo()
        {
            try
            {
                _formatContext.FindStreamInfo();
            }
            catch (FFmpegException ex)
            {
                Log.Error(ex);
                throw new DemuxerException("Cannot find streams info", ex);
            }
        }

        private ArraySegment<byte> ReadPacket(int size)
        {
            try
            {
                var token = _cancellationTokenSource.Token;
                var data = _demuxerDataSource.Read(size, token);
                return data;
            }
            catch (TaskCanceledException)
            {
                Log.Info("Take cancelled");
            }
            catch (InvalidOperationException ex)
            {
                Log.Warn(ex, ex.StackTrace);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unexpected exception: {ex.GetType()}");
            }

            return default;
        }

        private long SeekFun(long pos, int whence)
        {
            try
            {
                Log.Info($"Seek pos == {pos}, whence == {whence}");
                if ((whence & FFmpegBindings.Interop.FFmpeg.AVSEEK_SIZE) != 0)
                {
                    return Int32.MaxValue;
                }

                var token = _cancellationTokenSource.Token;
                _demuxerDataSource.Seek(pos, token);
                return 0;
            }
            catch (TaskCanceledException)
            {
                Log.Info("Take cancelled");
            }
            catch (InvalidOperationException ex)
            {
                Log.Warn(ex);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unexpected exception: {ex.GetType()}");
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