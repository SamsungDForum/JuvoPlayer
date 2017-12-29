using System;
using JuvoPlayer.Common;

namespace JuvoPlayer.Dash
{
    internal class DashDataProvider : IDataProvider
    {
        private readonly IDemuxer _demuxer;
        private readonly IDashClient _dashClient;

        public DashDataProvider(
            IDashClient dashClient,
            IDemuxer demuxer)
        {
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

        public void OnPaused()
        {
            throw new NotImplementedException();
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
            _dashClient.Start();
            _demuxer.StartForExternalSource();
        }

        public void OnTimeUpdated(double time)
        {
            _dashClient.OnTimeUpdated(time);
        }
    }
}
