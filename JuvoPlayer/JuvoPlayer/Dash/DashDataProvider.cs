using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.Dash
{
    class DashDataProvider : IDataProvider
    {
        private IDemuxer demuxer;
        private IDashClient dashClient;
        private ClipDefinition currentClip;
        // TODO change manifest to private
        public DashManifest manifest { get; set; }
        public DashDataProvider(
            IDashClient dashClient,
            IDemuxer demuxer,
            ClipDefinition currentClip)
        {
            this.demuxer =
                demuxer ??
                throw new ArgumentNullException(
                    "Demuxer",
                    "Demuxer cannot be null.");
            this.currentClip =
                currentClip ??
                throw new ArgumentNullException(
                    "Clip",
                    "Clip cannot be null.");
            this.dashClient =
                dashClient ??
                throw new ArgumentNullException(
                    "dashClient",
                    "dashClient cannot be null");
            this.manifest = new DashManifest(this.currentClip.Url);

            this.demuxer.StreamConfigReady += OnStreamConfigReady;
            this.demuxer.StreamPacketReady += OnStreamPacketReady;
        }

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
            demuxer.Start();
        }
    }
}
