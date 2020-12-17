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
using FFmpegBindings.Interop;
using JuvoLogger;
using JuvoPlayer.Common;
using Nito.AsyncEx;
using static Configuration.FFmpegDemuxer;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public class FFmpegDemuxer : IDemuxer
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly IFFmpegGlue ffmpegGlue;
        private AsyncContextThread thread;
        private IAVFormatContext formatContext;
        private IAVIOContext ioContext;
        private int audioIdx = -1;
        private int videoIdx = -1;
        private CancellationTokenSource cancellationTokenSource;
        private ChunksBuffer buffer;
        private TaskCompletionSource<bool> completionSource;

        public FFmpegDemuxer(IFFmpegGlue ffmpegGlue)
        {
            this.ffmpegGlue = ffmpegGlue;
        }

        public bool IsInitialized()
        {
            return thread != null;
        }

        public Task<ClipConfiguration> InitForUrl(string url)
        {
            if (IsInitialized())
                throw new InvalidOperationException("Initialization already started");

            InitThreadingPrimitives();
            return thread.Factory.StartNew(() => InitDemuxer(() => InitUrl(url)));
        }

        private void InitThreadingPrimitives()
        {
            thread = new AsyncContextThread();
            cancellationTokenSource = new CancellationTokenSource();
            completionSource = new TaskCompletionSource<bool>();
        }

        private void InitUrl(string url)
        {
            try
            {
                formatContext = ffmpegGlue.AllocFormatContext();
                formatContext.ProbeSize = ProbeSize;
                formatContext.MaxAnalyzeDuration = TimeSpan.FromSeconds(10);
                formatContext.Open(url);
            }
            catch (FFmpegException ex)
            {
                DeallocFFmpeg();
                Logger.Error(ex);
                throw new DemuxerException("Cannot open formatContext", ex);
            }
        }

        public Task<ClipConfiguration> InitForEs()
        {
            if (IsInitialized())
                throw new InvalidOperationException("Initialization already started");

            InitThreadingPrimitives();
            buffer = new ChunksBuffer();
            return thread.Factory.StartNew(() => InitDemuxer(InitEs));
        }

        private ClipConfiguration InitDemuxer(Action initAction)
        {
            ffmpegGlue.Initialize();
            initAction();

            var clipConfiguration = new ClipConfiguration();

            FindStreamsInfo();
            ReadDuration(ref clipConfiguration);
            ReadStreamConfigs(ref clipConfiguration);
            ReadContentProtectionConfigs(ref clipConfiguration);

            return clipConfiguration;
        }

        private void ReadDuration(ref ClipConfiguration clipConfiguration)
        {
            if (formatContext.Duration > TimeSpan.Zero)
                clipConfiguration.Duration = formatContext.Duration;
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
            configuration.DrmInitDatas = formatContext.DRMInitData.ToList();
        }

        private void InitEs()
        {
            try
            {
                ioContext = ffmpegGlue.AllocIOContext(BufferSize, ReadPacket);
                ioContext.Seekable = false;
                ioContext.WriteFlag = false;

                formatContext = ffmpegGlue.AllocFormatContext();
                formatContext.ProbeSize = ProbeSize;
                formatContext.MaxAnalyzeDuration = MaxAnalyzeDuration;
                formatContext.AVIOContext = ioContext;
                formatContext.Open();
            }
            catch (FFmpegException ex)
            {
                DeallocFFmpeg();

                // Report init errors resulting from reset as cancellation
                if (cancellationTokenSource.IsCancellationRequested)
                    throw new TaskCanceledException();

                Logger.Error(ex);
                throw new DemuxerException("Cannot open formatContext", ex);
            }
        }

        private void FindStreamsInfo()
        {
            try
            {
                formatContext.FindStreamInfo();
                SelectBestStreams();
            }
            catch (FFmpegException ex)
            {
                Logger.Error(ex);
                DeallocFFmpeg();
                throw new DemuxerException("Cannot find streams info", ex);
            }
        }

        private void SelectBestStreams()
        {
            audioIdx = FindBestStream(AVMediaType.AVMEDIA_TYPE_AUDIO);
            videoIdx = FindBestStream(AVMediaType.AVMEDIA_TYPE_VIDEO);
            if (audioIdx < 0 && videoIdx < 0)
                throw new FFmpegException("Neither video nor audio stream found");
            formatContext.EnableStreams(audioIdx, videoIdx);
        }

        private int FindBestStream(AVMediaType mediaType)
        {
            var streamId = formatContext.FindBestBandwidthStream(mediaType);
            return streamId >= 0 ? streamId : formatContext.FindBestStream(mediaType);
        }

        public Task<Packet> NextPacket()
        {
            if (!IsInitialized())
                throw new InvalidOperationException();

            return thread.Factory.StartNew(() =>
            {
                var streamIndexes = new[] { audioIdx, videoIdx };
                var packet = formatContext.NextPacket(streamIndexes);
                if (packet == null)
                    completionSource?.SetResult(true);
                return packet;
            });
        }

        public void PushChunk(byte[] chunk)
        {
            if (buffer == null)
                throw new InvalidOperationException("Push chunk called before InitForEs() or after Reset()");
            buffer.Add(chunk);
        }

        public Task Completion => completionSource?.Task;

        public void Complete()
        {
            buffer?.CompleteAdding();
        }

        public void Reset()
        {
            cancellationTokenSource?.Cancel();
            thread?.Factory?.Run(() => DeallocFFmpeg());
            thread?.Join();
            thread = null;
            buffer = null;
        }

        public Task<TimeSpan> Seek(TimeSpan time, CancellationToken token)
        {
            if (!IsInitialized())
                throw new InvalidOperationException();

            return thread.Factory.StartNew(() =>
            {
                var index = audioIdx;
                if (videoIdx != -1)
                    index = videoIdx;
                formatContext.Seek(index, time);
                return time;
            }, token);
        }

        private void ReadAudioConfig(ICollection<StreamConfig> configs)
        {
            if (audioIdx < 0)
                return;

            var config = formatContext.ReadConfig(audioIdx);

            Logger.Info("Setting audio stream to " + audioIdx);
            Logger.Info(config.ToString());

            configs.Add(config);
        }

        private void ReadVideoConfig(ICollection<StreamConfig> configs)
        {
            if (videoIdx < 0)
                return;

            var config = formatContext.ReadConfig(videoIdx);

            Logger.Info("Setting video stream to " + videoIdx);
            Logger.Info(config.ToString());

            configs.Add(config);
        }

        private ArraySegment<byte> ReadPacket(int size)
        {
            try
            {
                var token = cancellationTokenSource.Token;
                var data = buffer.Take(size, token);
                return data;
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
            return new ArraySegment<byte>();
        }

        private void DeallocFFmpeg()
        {
            formatContext?.Dispose();
            ioContext?.Dispose();
        }

        public void Dispose()
        {
            Reset();
        }
    }
}
