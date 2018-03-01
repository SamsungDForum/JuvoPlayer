using System;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Logging;
using MpdParser;

namespace JuvoPlayer.Dash
{
    internal class DashDataProvider : IDataProvider
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private readonly DashManifest manifest;
        private DashMediaPipeline audioPipeline;
        private DashMediaPipeline videoPipeline;

        public DashDataProvider(
            DashManifest manifest,
            DashMediaPipeline audioPipeline,
            DashMediaPipeline videoPipeline)
        {
            this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest), "manifest cannot be null");
            this.audioPipeline = audioPipeline ?? throw new ArgumentNullException(nameof(audioPipeline), "audioPipeline cannot be null");
            this.videoPipeline = videoPipeline ?? throw new ArgumentNullException(nameof(videoPipeline), "videoPipeline cannot be null");

            audioPipeline.DRMInitDataFound += OnDRMInitDataFound;
            audioPipeline.SetDrmConfiguration += OnSetDrmConfiguration;
            audioPipeline.StreamConfigReady += OnStreamConfigReady;
            audioPipeline.StreamPacketReady += OnStreamPacketReady;
            videoPipeline.DRMInitDataFound += OnDRMInitDataFound;
            videoPipeline.SetDrmConfiguration += OnSetDrmConfiguration;
            videoPipeline.StreamConfigReady += OnStreamConfigReady;
            videoPipeline.StreamPacketReady += OnStreamPacketReady;
        }

        public event ClipDurationChanged ClipDurationChanged;
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event StreamPacketReady StreamPacketReady;
        public event StreamsFound StreamsFound;

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

        private void OnStreamPacketReady(StreamPacket packet)
        {
            StreamPacketReady?.Invoke(packet);
        }

        public void OnChangeRepresentation(int representationId)
        {
        }

        public void OnPaused()
        {
        }

        public void OnPlayed()
        {
        }

        public void OnSeek(TimeSpan time)
        {
            if (!IsSeekingSupported())
                return;

            audioPipeline.Seek(time);
            videoPipeline.Seek(time);
        }

        public void OnStopped()
        {
            audioPipeline.Stop();
            videoPipeline.Stop();
        }

        public bool IsSeekingSupported()
        {
            // check for live content
            return audioPipeline.IsSeekingSupported() && videoPipeline.IsSeekingSupported();
        }

        public void Start()
        {
            Logger.Info("Dash start.");
            
            foreach (var period in manifest.Document.Periods)
            {
                Logger.Info(period.ToString());

                Media audio = Find(period, "en", MediaType.Audio) ??
                        Find(period, "und", MediaType.Audio)??
                        Find(period, null, MediaType.Audio);

                Media video = Find(period, "en", MediaType.Video) ??
                        Find(period, "und", MediaType.Video)??
                        Find(period, null, MediaType.Video);

                // TODO(p.galiszewsk): is it possible to have period without audio/video?
                if (audio != null && video != null)
                {
                    Logger.Info("Video: " + video);
                    videoPipeline.Start(video);

                    Logger.Info("Audio: " + audio);
                    audioPipeline.Start(audio);

                    // TODO(p.galiszewsk): unify time management
                    if (period.Duration.HasValue)
                        ClipDurationChanged?.Invoke(period.Duration.Value);

                    return;
                }
            }
        }

        private static Media Find(MpdParser.Period p, string language, MediaType type, MediaRole role = MediaRole.Main)
        {
            Media res = null;
            for(int i=0;i<p.Sets.Length;i++)
            {
                if (p.Sets[i].Type.Value != type)
                {
                    continue;
                }

                if (language != null)
                {
                    if (p.Sets[i].Lang != language)
                    {
                        continue;
                    }
                }

                if (p.Sets[i].HasRole(role))
                {
                    res = p.Sets[i];
                    break;
                }

                if (p.Sets[i].Roles.Length == 0)
                {
                    res =  p.Sets[i];
                    break;
                }
            }

            return res;
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            audioPipeline.OnTimeUpdated(time);
            videoPipeline.OnTimeUpdated(time);
        }

        public void Dispose()
        {
            OnStopped();

            audioPipeline = null;
            videoPipeline = null;
        }
    }
}
