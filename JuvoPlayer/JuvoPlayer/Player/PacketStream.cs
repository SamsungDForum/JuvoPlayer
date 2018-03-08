using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;

namespace JuvoPlayer.Player
{
    public class PacketStream : IPacketStream
    {
        protected IDRMManager drmManager;
        protected IPlayerAdapter playerAdapter;
        private IDRMSession drmSession;
        private StreamConfig config;
        private bool forceDrmChange;
        private Task drmSessionInitializeTask;
        private readonly StreamType streamType;

        public PacketStream(StreamType streamType, IPlayerAdapter player, IDRMManager drmManager)
        {
            this.streamType = streamType;
            this.drmManager = drmManager ??
                              throw new ArgumentNullException(nameof(drmManager), "drmManager cannot be null");
            playerAdapter = player ?? throw new ArgumentNullException(nameof(player), "player cannot be null");
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
                packet = drmSession.DecryptPacket(packet as EncryptedPacket).Result;

            playerAdapter.AppendPacket(packet);
        }

        public void OnStreamConfigChanged(StreamConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "config cannot be null");

            if (config.StreamType() != streamType)
                throw new ArgumentException("config type doesn't match");

            if (this.config != null && this.config.Equals(config))
                return;

            forceDrmChange = true;

            this.config = config;

            switch (this.config.StreamType())
            {
                case StreamType.Audio:
                    playerAdapter.SetAudioStreamConfig(this.config as AudioStreamConfig);
                    break;
                case StreamType.Video:
                    playerAdapter.SetVideoStreamConfig(this.config as VideoStreamConfig);
                    break;
                case StreamType.Subtitle:
                case StreamType.Teletext:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void OnClearStream()
        {
            drmSession?.Dispose();
            drmSession = null;
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
