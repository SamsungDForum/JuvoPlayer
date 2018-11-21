using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Subtitles;
using Tizen;


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

        private readonly DashManifestProvider manifestProvider;

        private bool disposed;
        private bool ignoreTimeUpdates;
        private readonly IDisposable manifestReadySub;

        public DashDataProvider(
            DashManifest manifest,
            DashMediaPipeline audioPipeline,
            DashMediaPipeline videoPipeline)
        {
            this.audioPipeline = audioPipeline ?? throw new ArgumentNullException(nameof(audioPipeline), "audioPipeline cannot be null");
            this.videoPipeline = videoPipeline ?? throw new ArgumentNullException(nameof(videoPipeline), "videoPipeline cannot be null");

            manifestProvider = new DashManifestProvider(manifest, audioPipeline, videoPipeline);
            manifestReadySub = manifestProvider.ManifestReady().Subscribe(unit => OnManifestReady(), SynchronizationContext.Current);
        }

        private void OnManifestReady()
        {
            Logger.Info("");
            audioPipeline.SwitchStreamIfNeeded();
            videoPipeline.SwitchStreamIfNeeded();
        }

        public IObservable<TimeSpan> ClipDurationChanged()
        {
            return manifestProvider.ClipDurationChanged();
        }

        public IObservable<DRMInitData> DRMInitDataFound()
        {
            return audioPipeline.OnDRMInitDataFound()
                .Merge(videoPipeline.OnDRMInitDataFound());
        }

        public IObservable<DRMDescription> SetDrmConfiguration()
        {
            return audioPipeline.SetDrmConfiguration()
                .Merge(videoPipeline.SetDrmConfiguration());
        }

        public IObservable<StreamConfig> StreamConfigReady()
        {
            return audioPipeline.StreamConfigReady()
                .Merge(videoPipeline.StreamConfigReady());
        }

        public IObservable<Packet> PacketReady()
        {
            return audioPipeline.PacketReady()
                .Merge(videoPipeline.PacketReady());
        }

        public IObservable<string> StreamError()
        {
            return manifestProvider.StreamError()
                .Merge(audioPipeline.StreamError())
                .Merge(videoPipeline.StreamError());
        }

        public IObservable<Unit> BufferingStarted()
        {
            return videoPipeline.BufferingStarted().Merge(audioPipeline.BufferingStarted());
        }

        public IObservable<Unit> BufferingCompleted()
        {
            return videoPipeline.BufferingCompleted().Merge(audioPipeline.BufferingCompleted());
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
            var subtitleInfo = manifestProvider.GetSubtitleInfo(description);
            cuesMap = new SubtitleFacade().LoadSubtitles(subtitleInfo);
        }

        public void OnPaused()
        {
            Logger.Info("");
        }

        public void OnPlayed()
        {
        }

        public void OnSeekStarted(TimeSpan time, uint seekId)
        {
            if (!IsSeekingSupported())
                return;

            ignoreTimeUpdates = true;

            var pipelines = new[] {videoPipeline, audioPipeline};
            foreach (var pipeline in pipelines)
            {
                pipeline.Pause();
                pipeline.Seek(time, seekId);
                pipeline.Resume();
            }
        }

        public void OnSeekCompleted()
        {
            ignoreTimeUpdates = false;
        }

        public void OnStopped()
        {
            Logger.Info("");

            manifestProvider.Stop();
            videoPipeline.Stop();
            audioPipeline.Stop();
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
                    return manifestProvider.GetSubtitlesDescription();
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
            if (disposed || ignoreTimeUpdates)
                return;

            currentTime = time;
            manifestProvider.CurrentTime = time;

            audioPipeline.OnTimeUpdated(time);
            videoPipeline.OnTimeUpdated(time);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            OnStopped();

            manifestReadySub.Dispose();
            manifestProvider.Dispose();

            audioPipeline?.Dispose();
            audioPipeline = null;

            videoPipeline?.Dispose();
            videoPipeline = null;

        }
    }
}

