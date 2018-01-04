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

            audioPipeline.StreamConfigReady += OnStreamConfigReady;
            audioPipeline.StreamPacketReady += OnStreamPacketReady;
            videoPipeline.StreamConfigReady += OnStreamConfigReady;
            videoPipeline.StreamPacketReady += OnStreamPacketReady;
        }

        public event ClipDurationChanged ClipDurationChanged;
        public event DRMDataFound DRMDataFound;
        public event StreamConfigReady StreamConfigReady;
        public event StreamPacketReady StreamPacketReady;
        public event StreamsFound StreamsFound;

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

        public void OnSeek(double time)
        {
            throw new NotImplementedException();
        }

        public void OnStopped()
        {
        }

        public void Start()
        {
            Tizen.Log.Info("JuvoPlayer", "Dash start.");

            Media audio = null;
            Media video = null;
            
            foreach (var period in manifest.Document.Periods)
            {
                Tizen.Log.Info("JuvoPlayer", period.ToString());

                if (audio == null)
                {
                    audio = Find(period, "en", MediaType.Audio) ??
                        Find(period, "und", MediaType.Audio);

                }
                if (video == null)
                {
                    video = Find(period, "en", MediaType.Video) ??
                        Find(period, "und", MediaType.Video);
                }
            }

            if (video != null)
            {
                Tizen.Log.Info("JuvoPlayer", "Video: " + video);
                videoPipeline.Start(video);
            }

            if (audio != null)
            {
                Tizen.Log.Info("JuvoPlayer", "Audio: " + audio);
                audioPipeline.Start(audio);
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

        public void OnTimeUpdated(double time)
        {
            audioPipeline.OnTimeUpdated(time);
            videoPipeline.OnTimeUpdated(time);
        }
    }
}
