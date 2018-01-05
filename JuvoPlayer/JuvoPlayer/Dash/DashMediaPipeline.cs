using JuvoPlayer.Common;
using MpdParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JuvoPlayer.Dash
{
    class DashMediaPipeline
    {
        public event StreamConfigReady StreamConfigReady;
        public event StreamPacketReady StreamPacketReady;
        public event DRMDataFound DRMDataFound;

        private IDashClient dashClient;
        private IDemuxer demuxer;
        private StreamType streamType;

        public DashMediaPipeline(IDashClient dashClient, IDemuxer demuxer, StreamType streamType)
        {
            this.dashClient = dashClient ?? throw new ArgumentNullException(nameof(dashClient), "dashClient cannot be null");
            this.demuxer = demuxer ?? throw new ArgumentNullException(nameof(dashClient), "demuxer cannot be null");
            this.streamType = streamType;

            demuxer.DRMDataFound += OnDrmDataFound;
            demuxer.StreamConfigReady += OnStreamConfigReady;
            demuxer.StreamPacketReady += OnStreamPacketReady;
        }

        public void Start(Media newMedia)
        {
            if (newMedia == null)
                throw new ArgumentNullException(nameof(newMedia), "newMedia cannot be null");

            Tizen.Log.Info("JuvoPlayer", "Dash start.");

            dashClient.UpdateMedia(newMedia);
            dashClient.Start();
            demuxer.StartForExternalSource();
        }

        public void OnTimeUpdated(double time)
        {
            dashClient.OnTimeUpdated(time);
        }

        private void OnDrmDataFound(DRMData drmData)
        {
            drmData.streamType = streamType;
            DRMDataFound?.Invoke(drmData);
        }

        private void OnStreamPacketReady(StreamPacket packet)
        {
            if (packet != null)
            {
                StreamPacketReady?.Invoke(packet);
                return;
            }

            StreamPacketReady?.Invoke(StreamPacket.CreateEOS(streamType));
        }

        private void OnStreamConfigReady(StreamConfig config)
        {
            StreamConfigReady?.Invoke(config);
        }
    }
}
