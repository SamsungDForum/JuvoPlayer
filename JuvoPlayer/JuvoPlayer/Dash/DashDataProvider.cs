using System;
using System.Collections.Generic;
using JuvoPlayer.Common;
using MpdParser;

namespace JuvoPlayer.Dash
{
    internal class DashDataProvider : IDataProvider
    {
        private readonly DashManifest manifest;
        private readonly DashMediaPipeline audioPipeline;
        private readonly DashMediaPipeline videoPipeline;

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
            throw new NotImplementedException();
        }

        public void OnStopped()
        {
        }

        public void Start()
        {
            Tizen.Log.Info("JuvoPlayer", "Dash start.");
            
            foreach (var period in manifest.Document.Periods)
            {
                Tizen.Log.Info("JuvoPlayer", period.ToString());

                Media audio = Find(period, "en", MediaType.Audio) ??
                        Find(period, "und", MediaType.Audio);

                Media video = Find(period, "en", MediaType.Video) ??
                        Find(period, "und", MediaType.Video);

                // TODO(p.galiszewsk): is it possible to have period without audio/video?
                if (audio != null && video != null)
                {
                    Tizen.Log.Info("JuvoPlayer", "Video: " + video);
                    videoPipeline.Start(video);

                    Tizen.Log.Info("JuvoPlayer", "Audio: " + audio);
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
            Media missingRole = null;
            foreach (var set in p.Sets)
            {
                if (set.Type.Value != type || set.Lang != language)
                    continue;

                if (set.HasRole(role))
                {
                    return set;
                }

                if (set.Roles.Length == 0)
                {
                    missingRole = set;
                }
            }
            return missingRole;
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            audioPipeline.OnTimeUpdated(time);
            videoPipeline.OnTimeUpdated(time);
        }
    }
}
