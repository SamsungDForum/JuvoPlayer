using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Subtitles;


namespace JuvoPlayer.DataProviders.Dash
{

    internal class DashDataProvider : IDataProvider
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);


        private DashMediaPipeline audioPipeline;
        private DashMediaPipeline videoPipeline;


        private CuesMap cuesMap;
        private TimeSpan currentTime = TimeSpan.Zero;

        public event ClipDurationChanged ClipDurationChanged;
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event PacketReady PacketReady;
        public event StreamError StreamError;
        private readonly List<SubtitleInfo> subtitleInfos = new List<SubtitleInfo>();
        private readonly DashManifestProvider manifestProvider;

        //private readonly DashManifest manifest;

        private bool disposed;

        public DashDataProvider(
            DashManifest manifest,
            DashMediaPipeline audioPipeline,
            DashMediaPipeline videoPipeline)
        {

            this.audioPipeline = audioPipeline ?? throw new ArgumentNullException(nameof(audioPipeline), "audioPipeline cannot be null");
            this.videoPipeline = videoPipeline ?? throw new ArgumentNullException(nameof(videoPipeline), "videoPipeline cannot be null");

            manifestProvider = new DashManifestProvider(manifest, audioPipeline, videoPipeline);
            manifestProvider.StreamError += OnStreamError;
            manifestProvider.ClipDurationChanged += ClipDurationChanged;
            manifestProvider.Play += OnPlayed;

            audioPipeline.DRMInitDataFound += OnDRMInitDataFound;
            audioPipeline.SetDrmConfiguration += OnSetDrmConfiguration;
            audioPipeline.StreamConfigReady += OnStreamConfigReady;
            audioPipeline.PacketReady += OnPacketReady;
            audioPipeline.StreamError += OnStreamError;

            videoPipeline.DRMInitDataFound += OnDRMInitDataFound;
            videoPipeline.SetDrmConfiguration += OnSetDrmConfiguration;
            videoPipeline.StreamConfigReady += OnStreamConfigReady;
            videoPipeline.PacketReady += OnPacketReady;
            videoPipeline.StreamError += OnStreamError;
        }

        private void OnDRMInitDataFound(DRMInitData drmData)
        {
            DRMInitDataFound?.Invoke(drmData);
        }

        private void OnSetDrmConfiguration(DRMDescription description)
        {
            SetDrmConfiguration?.Invoke(description);
        }

        private void OnStreamConfigReady(StreamConfig config)
        {
            StreamConfigReady?.Invoke(config);
        }

        private void OnPacketReady(Packet packet)
        {
            PacketReady?.Invoke(packet);
        }

        public void OnChangeActiveStream(StreamDescription stream)
        {
            switch (stream.StreamType)
            {
                case StreamType.Audio:
                    audioPipeline.ChangeStream(stream);
                    break;
                case StreamType.Video:
                    videoPipeline.ChangeStream(stream);
                    break;
                case StreamType.Subtitle:
                    OnChangeActiveSubtitleStream(stream);
                    break;
                default:
                    break;
            }
        }

        public void OnDeactivateStream(StreamType streamType)
        {
            if (streamType == StreamType.Subtitle)
            {
                OnDeactivateSubtitleStream();
                return;
            }
            throw new NotImplementedException();
        }

        private void OnDeactivateSubtitleStream()
        {
            cuesMap = null;
        }

        private void OnChangeActiveSubtitleStream(StreamDescription description)
        {


            var subtitleInfo = subtitleInfos[description.Id];
            cuesMap = new SubtitleFacade().LoadSubtitles(subtitleInfo);
        }

        public void OnPaused()
        {
            Logger.Info("");
        }

        public void OnPlayed()
        {
            Logger.Info("");
            Parallel.Invoke(() => audioPipeline.SwitchStreamIfNeeded(),
                            () => videoPipeline.SwitchStreamIfNeeded());
        }

        public void OnSeek(TimeSpan time)
        {
            if (!IsSeekingSupported())
                return;

            Parallel.Invoke(() => videoPipeline.Pause(), () => audioPipeline.Pause());
            Parallel.Invoke(() => videoPipeline.Seek(time), () => audioPipeline.Seek(time));
            Parallel.Invoke(() => videoPipeline.Resume(), () => audioPipeline.Resume());
        }

        public void OnStopped()
        {
            Logger.Info("");

            manifestProvider.Stop();

            Parallel.Invoke(() => videoPipeline.Stop(), () => audioPipeline.Stop());
        }

        public bool IsSeekingSupported()
        {
            // If there is no current document (not downloaded yet), prevent seeking.
            // as manifest may be dynamic.
            return !manifestProvider.Manifest.CurrentDocument?.IsDynamic ?? false;
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            switch (streamType)
            {
                case StreamType.Audio:
                    return audioPipeline.GetStreamsDescription();
                case StreamType.Video:
                    return videoPipeline.GetStreamsDescription();
                case StreamType.Subtitle:
                    return subtitleInfos.Select(info => info.ToStreamDescription()).ToList();
                default:
                    return new List<StreamDescription>();
            }
        }

        public void Start()
        {
            Logger.Info("Dash Data Provider start");

            manifestProvider.Start();
        }

        public Cue CurrentCue => cuesMap?.Get(currentTime);

        public void OnTimeUpdated(TimeSpan time)
        {
            if (disposed)
                return;

            currentTime = time;
            manifestProvider.CurrentTime = time;

            Parallel.Invoke(
                () =>
                {
                    audioPipeline.OnTimeUpdated(time);

                    audioPipeline.AdaptToNetConditions();
                    audioPipeline.SwitchStreamIfNeeded();

                },
                () =>
                {
                    videoPipeline.OnTimeUpdated(time);

                    videoPipeline.AdaptToNetConditions();
                    videoPipeline.SwitchStreamIfNeeded();

                });
        }

        private void OnStreamError(string errorMessage)
        {
            Logger.Error($"Stream Error: {errorMessage}. Terminating pipelines.");

            // Bubble up stream error info up to PlayerController which will shut down
            // underlying player
            StreamError?.Invoke(errorMessage);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            OnStopped();

            // Detach event handlers from manifest provider before disposing
            manifestProvider.StreamError -= OnStreamError;
            manifestProvider.ClipDurationChanged -= ClipDurationChanged;
            manifestProvider.Play -= OnPlayed;

            manifestProvider.Dispose();

            audioPipeline?.Dispose();
            audioPipeline = null;

            videoPipeline?.Dispose();
            videoPipeline = null;

        }
    }
}

