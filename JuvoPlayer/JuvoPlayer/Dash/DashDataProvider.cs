using System;
using JuvoPlayer.Common;

namespace JuvoPlayer.Dash
{
    class DashDataProvider : IDataProvider
    {
        private IDemuxer _demuxer;
        private IDashClient _dashClient;
        private ClipDefinition _currentClip;
        private DashManifest _manifest;

        public DashDataProvider(
            IDashClient dashClient,
            IDemuxer demuxer,
            ClipDefinition currentClip,
            DashManifest manifest)
        {
            _manifest = manifest;

            _currentClip = currentClip ?? throw new ArgumentNullException(nameof(currentClip), "Clip cannot be null.");
            _dashClient = dashClient ?? throw new ArgumentNullException(nameof(dashClient), "dashClient cannot be null");
            _demuxer = demuxer ?? throw new ArgumentNullException(nameof(dashClient), "demuxer cannot be null");

            _demuxer.StreamConfigReady += OnStreamConfigReady;
            _demuxer.StreamPacketReady += OnStreamPacketReady;
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
            if (packet != null)
            {
                StreamPacketReady?.Invoke(packet);
                return;
            }
            // TODO(p.galiszewsk): Here we need two demuxers, so 
            // this should be reworked later
            StreamPacketReady?.Invoke(StreamPacket.CreateEOS(StreamType.Audio));
            StreamPacketReady?.Invoke(StreamPacket.CreateEOS(StreamType.Video));

        }

        public void OnChangeRepresentation(int representationId)
        {
            throw new NotImplementedException();
        }

        public void OnPlay()
        {
        }

        public void OnSeek(double time)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            Tizen.Log.Info("JuvoPlayer", "Dash start.");
            _demuxer.StartForExternalSource();
        }

        public void OnTimeUpdated(double time)
        {
        }
    }
}
