using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;

namespace JuvoPlayer.Player
{
    internal class PacketStream : IPacketStream
    {
        protected ICodecExtraDataHandler codecExtraDataHandler;
        protected IDrmManager drmManager;
        protected IPlayer player;
        private IDrmSession drmSession = null;
        private StreamConfig config;
        private readonly StreamType streamType;
        private ManualResetEventSlim waitForDRMSession = new ManualResetEventSlim(true);
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

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

            // Wait for new DRM Session. If not obtained within 
            // 10 seconds... die...
            if (!waitForDRMSession.Wait(TimeSpan.FromSeconds(10)))
            {
                Logger.Error("No supported DRM Found");
                throw new InvalidOperationException("No supported DRM Found");
            }

            if (packet is EncryptedPacket)
            {
                var encryptedPacket = (EncryptedPacket)packet;
                encryptedPacket.DrmSession = drmSession.GetInstance();
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

            this.config = config;

            codecExtraDataHandler.OnStreamConfigChanged(config);

            player.SetStreamConfig(this.config);
        }

        public void OnClearStream()
        {
            Logger.Info($"{streamType}");
            drmSession?.FreeInstance();
            drmSession = null;
            config = null;
        }

        public void OnDRMFound(DRMInitData data)
        {
            Logger.Info($"{streamType}");

            // Prevent packets from being appended till we find supported DRM Session
            // Do so ONLY if there is no current session. Existing session will be used.
            // If no new session will be found, DRM Decode errors will be raised by player.
            //
            if (drmSession == null)
                waitForDRMSession.Reset();

            var newSession = drmManager.CreateDRMSession(data);

            // Do not reset wait for DRM event. If there is no valid session
            // do not want to append new data
            //
            if (newSession == null)
                return;

            Logger.Info($"{streamType}: New DRM session found");

            // New valid DRM Session. If exists, mark current session as removable
            drmSession?.AllowRemoval();

            // Set new session as current & let data submitters run wild.
            // There is no need to store sessions. They live in player queue
            //
            drmSession = newSession;

            waitForDRMSession.Set();
            drmSession.Initialize();
        }

        public void Dispose()
        {
            OnClearStream();
        }
    }
}
