using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Utils;
using JuvoPlayer.Demuxers.FFmpeg.Interop;
using JuvoPlayer.SharedBuffers;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public class FFmpegDemuxerRefactor : IDemuxer
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly Subject<TimeSpan> clipDurationSubject = new Subject<TimeSpan>();
        private readonly Subject<DRMInitData> drmInitDataSubject = new Subject<DRMInitData>();
        private readonly Subject<StreamConfig> streamConfigSubject = new Subject<StreamConfig>();
        private readonly Subject<Packet> packetReadySubject = new Subject<Packet>();
        private readonly Subject<string> demuxerErrorSubject = new Subject<string>();

        private const int BufferSize = 128 * 1024;
        private unsafe byte* buffer = null;
        private int audioIdx = -1;
        private int videoIdx = -1;
        private bool parse = true;

        private IAVFormatContext formatContext;
        private IAVIOContext ioContext;

        private Task demuxTask;

        private const int MicrosecondsPerSecond = 1000000;

        private readonly AVRational microsBase = new AVRational
        {
            num = 1,
            den = MicrosecondsPerSecond
        };

        private readonly ISharedBuffer dataBuffer;
        public bool IsPaused { get; private set; }
        private readonly AutoResetEvent pausedEvent = new AutoResetEvent(false);
        private bool isDisposed;
        private bool flushing;

        private readonly IFFmpegGlue ffmpegGlue;

        public FFmpegDemuxerRefactor(IFFmpegGlue ffmpegGlue, ISharedBuffer dataBuffer = null)
        {
            this.ffmpegGlue = ffmpegGlue;
            this.dataBuffer = dataBuffer;
        }

        public void StartForExternalSource(InitializationMode initMode)
        {
            Logger.Info("");
            if (dataBuffer == null)
                throw new InvalidOperationException("dataBuffer cannot be null");
            RunDemuxTask(InitEs, initMode);
        }

        public void StartForUrl(string url)
        {
            Logger.Info("");
            RunDemuxTask(() => InitUrl(url), InitializationMode.Full);
        }

        private void RunDemuxTask(Action initAction, InitializationMode initMode)
        {
            demuxTask = Task.Run(() => DemuxTask(initAction, initMode));
            demuxTask.ContinueWith(res => OnError(GetErrorMessage(res)), TaskContinuationOptions.OnlyOnFaulted);
        }

        private void InitEs()
        {
            try
            {
                ioContext = ffmpegGlue.AllocIOContext(BufferSize, ReadPacket);
                ioContext.Seekable = false;
                ioContext.WriteFlag = false;

                formatContext = ffmpegGlue.AllocFormatContext();
                formatContext.ProbeSize = 128 * 1024;
                formatContext.MaxAnalyzeDuration = TimeSpan.FromSeconds(10);
                formatContext.AVIOContext = ioContext;
                formatContext.Open();
            }
            catch (FFmpegException ex)
            {
                DeallocFFmpeg();
                Logger.Error(ex.Message);
                Logger.Error(ex.StackTrace);
                throw new DemuxerException("Cannot open formatContext", ex);
            }
        }

        private void InitUrl(string url)
        {
            try
            {
                formatContext = ffmpegGlue.AllocFormatContext();
                formatContext.ProbeSize = 128 * 1024;
                formatContext.MaxAnalyzeDuration = TimeSpan.FromSeconds(10);
                formatContext.Open(url);
            }
            catch (FFmpegException ex)
            {
                DeallocFFmpeg();
                Logger.Error(ex.Message);
                Logger.Error(ex.StackTrace);
                throw new DemuxerException("Cannot open formatContext", ex);
            }
        }

        private ArraySegment<byte>? ReadPacket(int size)
        {
            return dataBuffer.ReadData(size);
        }

        private void FindStreamsInfo()
        {
            try
            {
                formatContext.FindStreamInfo();
                if (formatContext.Duration > TimeSpan.Zero)
                    clipDurationSubject.OnNext(formatContext.Duration);

                SelectBestStreams();
            }
            catch (FFmpegException ex)
            {
                Logger.Error(ex.Message);
                Logger.Error(ex.StackTrace);
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

        private static string GetErrorMessage(Task response)
        {
            return response.Exception?.Flatten().InnerException?.Message;
        }

        private void OnError(string errorMessage)
        {
            // Have handler. Inform without exception throwup.
            demuxerErrorSubject.OnNext(errorMessage);
        }

        private void DemuxTask(Action initAction, InitializationMode initMode)
        {
            parse = true;

            try
            {
                InitializeDemuxer(initAction, initMode);
            }
            catch (Exception e)
            {
                Logger.Error("An error occured: " + e.Message);
                throw new DemuxerException("Couldn't initialize demuxer", e);
            }

            var streamIndexes = new[] {audioIdx, videoIdx};

            while (parse)
            {
                while (IsPaused)
                    pausedEvent.WaitOne();

                if (!parse)
                    return;

                var packet = formatContext.NextPacket(streamIndexes);
                if (packet != null)
                    packetReadySubject.OnNext(packet);
                else
                {
                    if (parse && !flushing)
                    {
                        // null means EOF
                        packetReadySubject.OnNext(null);
                    }
                    parse = false;
                }
            }
        }

        private void InitializeDemuxer(Action initAction, InitializationMode initMode)
        {
            ffmpegGlue.Initialize();

            // Finish more time-consuming init things
            initAction();

            if (initMode != InitializationMode.Full)
                return;

            FindStreamsInfo();
            ReadAudioConfig();
            ReadVideoConfig();
            UpdateContentProtectionConfig();
        }

        private void DeallocFFmpeg()
        {
            formatContext?.Dispose();
            ioContext?.Dispose();
        }

        private void UpdateContentProtectionConfig()
        {
            foreach (var drmInitData in formatContext.DRMInitData)
                drmInitDataSubject.OnNext(drmInitData);
        }

        private void ReadAudioConfig()
        {
            if (audioIdx < 0)
                return;

            var config = formatContext.ReadConfig(audioIdx);

            Logger.Info("Setting audio stream to " + audioIdx);
            Logger.Info(config.ToString());

            streamConfigSubject.OnNext(config);
        }

        private void ReadVideoConfig()
        {
            if (videoIdx < 0)
                return;

            var config = formatContext.ReadConfig(videoIdx);

            Logger.Info("Setting video stream to " + videoIdx);
            Logger.Info(config.ToString());

            streamConfigSubject.OnNext(config);
        }

        public void ChangePID(int pid)
        {
            // TODO(g.skowinski): Implement.
        }

        public void Reset()
        {
            parse = false;

            Resume();
            dataBuffer?.ClearData();
            dataBuffer?.WriteData(null, true);

            // If a task fails with an exception that's not caught anywhere,
            // calling
            try
            {
                demuxTask?.Wait();
            }
            catch (Exception ex)
            {
                Logger.Error($"Demuxer Status/Error: {demuxTask.Status} {ex.Message}");
            }

            // Clear EOS from buffer after demux task termination so there
            // will not be any data reads of EOS after restart as EOS is persistant in buffer
            dataBuffer?.ClearData();

            DeallocFFmpeg();
        }

        public void Flush()
        {
            flushing = true;

            Resume();
            dataBuffer?.WriteData(null, true);

            try
            {
                demuxTask?.Wait();
            }
            catch (Exception ex)
            {
                Logger.Error($"Demuxer Status/Error: {demuxTask.Status} {ex.Message}");
            }

            // Clear EOS from buffer after demux task termination so there
            // will not be any data reads of EOS after restart as EOS is persistant in buffer
            dataBuffer?.ClearData();

            DeallocFFmpeg();
            flushing = false;
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
            pausedEvent.Set();
        }

        public IObservable<TimeSpan> ClipDurationChanged()
        {
            return clipDurationSubject.AsObservable();
        }

        public IObservable<DRMInitData> DRMInitDataFound()
        {
            return drmInitDataSubject.AsObservable();
        }

        public IObservable<StreamConfig> StreamConfigReady()
        {
            return streamConfigSubject.AsObservable();
        }

        public IObservable<Packet> PacketReady()
        {
            return packetReadySubject.AsObservable();
        }

        public IObservable<string> DemuxerError()
        {
            return demuxerErrorSubject.AsObservable();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            if (parse)
                Reset();

            pausedEvent.Dispose();

            DisposeAllSubjects();

            GC.SuppressFinalize(this);

            isDisposed = true;
        }

        ~FFmpegDemuxerRefactor()
        {
            DeallocFFmpeg();
        }

        private void DisposeAllSubjects()
        {
            clipDurationSubject.Dispose();
            drmInitDataSubject.Dispose();
            streamConfigSubject.Dispose();
            packetReadySubject.Dispose();
            demuxerErrorSubject.Dispose();
        }
    }
}