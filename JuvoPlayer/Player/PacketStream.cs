using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;

namespace JuvoPlayer.Player
{
    internal class PacketStream : IPacketStream
    {
        protected ICodecExtraDataHandler codecExtraDataHandler;
        protected IDrmManager drmManager;
        protected IPlayer player;
        private IDrmSession drmSession;
        private StreamConfig config;
        private bool forceDrmChange;
        private Task drmSessionInitializeTask;
        private readonly StreamType streamType;

        public PacketStream(StreamType streamType, IPlayer player, IDrmManager drmManager, ICodecExtraDataHandler codecExtraDataHandler)
        {
            this.streamType = streamType;
            this.drmManager = drmManager ??
                              throw new ArgumentNullException(nameof(drmManager), "drmManager cannot be null");
            this.player = player ?? throw new ArgumentNullException(nameof(player), "player cannot be null");
            this.codecExtraDataHandler = codecExtraDataHandler ??
                              throw new ArgumentNullException(nameof(codecExtraDataHandler), "codecExtraDataHandler cannot be null");
        }

        public void OnAppendPacket(Packet packet)
        {
            if (packet.StreamType != streamType)
                throw new ArgumentException("packet type doesn't match");

            if (config == null)
                throw new InvalidOperationException("Packet stream is not configured");

            if (drmSessionInitializeTask != null && packet is EncryptedPacket)
            {
                drmSessionInitializeTask.Wait();
                drmSessionInitializeTask = null;
            }

            if (drmSession != null && packet is EncryptedPacket)
            {
                var encryptedPacket = (EncryptedPacket)packet;
                encryptedPacket.DrmSession = drmSession;
            }

            codecExtraDataHandler.OnAppendPacket(packet);

            player.AppendPacket(packet);
        }

        public void OnStreamConfigChanged(StreamConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "config cannot be null");

            if (config.StreamType() != streamType)
                throw new ArgumentException("config type doesn't match");

            if (this.config != null && this.config.Equals(config))
                return;

            forceDrmChange = this.config != null;

            this.config = config;

            codecExtraDataHandler.OnStreamConfigChanged(config);

            player.SetStreamConfig(this.config);
        }

        public void OnClearStream()
        {
            drmSession?.Dispose();
            drmSession = null;
            config = null;
        }

        public void OnDRMFound(DRMInitData data)
        {
            if (!forceDrmChange && drmSession != null)
                return;

            forceDrmChange = false;
            drmSession?.Dispose();
            drmSession = drmManager.CreateDRMSession(data);
            drmSessionInitializeTask = drmSession?.Initialize();
        }

        public void Dispose()
        {
            OnClearStream();
        }
    }
}
