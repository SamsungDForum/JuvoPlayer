using System;
using JuvoPlayer.Common;
using JuvoLogger;
using MpdParser;
using System.Collections.Generic;
using System.Linq;

namespace JuvoPlayer.DataProviders.Dash
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
            audioPipeline.PacketReady += OnPacketReady;
            videoPipeline.DRMInitDataFound += OnDRMInitDataFound;
            videoPipeline.SetDrmConfiguration += OnSetDrmConfiguration;
            videoPipeline.StreamConfigReady += OnStreamConfigReady;
            videoPipeline.PacketReady += OnPacketReady;
        }

        public event ClipDurationChanged ClipDurationChanged;
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event PacketReady PacketReady;
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

        private void OnPacketReady(Packet packet)
        {
            PacketReady?.Invoke(packet);
        }

        public void OnChangeRepresentation(StreamDefinition stream)
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
            return manifest.Document.Type != DocumentType.Dynamic;
        }

        public void Start()
        {
            Logger.Info("Dash start.");
            
            foreach (var period in manifest.Document.Periods)
            {
                Logger.Info(period.ToString());

                var audios = period.Sets.Where(o => o.Type.Value == MediaType.Audio).ToList();
                var audio = GetDefaultMedia(audios);

                var videos = period.Sets.Where(o => o.Type.Value == MediaType.Video).ToList();
                var video = GetDefaultMedia(videos);

                if (audio != null && video != null)
                {
                    Logger.Info("Video: " + video);
                    videoPipeline.Start(video);

                    Logger.Info("Audio: " + audio);
                    audioPipeline.Start(audio);

                    if (period.Duration.HasValue)
                        ClipDurationChanged?.Invoke(period.Duration.Value);

                    return;
                }
            }
        }

        private static Media GetDefaultMedia(IEnumerable<Media> medias)
        {
            Media media = null;
            if (medias.Count() == 1)
                media = medias.First();
            if (media == null)
                media = medias.FirstOrDefault(o => o.HasRole(MediaRole.Main));
            if (media == null)
                media = medias.FirstOrDefault(o => o.Lang == "en");
            if (media == null)
                media = medias.FirstOrDefault();
            return media;
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
