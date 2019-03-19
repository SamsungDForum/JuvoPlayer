/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

﻿using System;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;
using JuvoPlayer.Drms;

namespace JuvoPlayer.Player
{
    internal class PacketStream : IPacketStream
    {
        private readonly ICodecExtraDataHandler codecExtraDataHandler;
        private readonly IDrmManager drmManager;
        private readonly IPlayer player;
        private IDrmSession drmSession;
        private StreamConfig config;
        private readonly StreamType streamType;
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private bool forceDrmChange;

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

            if (packet is EncryptedPacket)
            {
                var encryptedPacket = (EncryptedPacket)packet;

                // Increment reference counter on DRM session
                //
                drmSession.Share();

                encryptedPacket.DrmSession = drmSession;
            }

            codecExtraDataHandler.OnAppendPacket(packet);

            player.AppendPacket(packet);
        }

        public void OnStreamConfigChanged(StreamConfig config)
        {
            Logger.Info($"{streamType}");

            if (config == null)
                throw new ArgumentNullException(nameof(config), "config cannot be null");

            if (config.StreamType() != streamType)
                throw new ArgumentException("config type doesn't match");

            if (this.config != null && this.config.Equals(config))
                return;

            forceDrmChange = (this.config != null);

            this.config = config;

            codecExtraDataHandler.OnStreamConfigChanged(config);

            player.SetStreamConfig(this.config);
        }

        public void OnClearStream()
        {
            Logger.Info($"{streamType}");

            // Remove reference count held by Packet Stream.
            // Player may still have packets and process them. Don't force remove
            //
            drmSession?.Release();

            drmSession = null;
            config = null;
        }

        public void OnDRMFound(DRMInitData data)
        {
            Logger.Info($"{streamType}");

            if (!forceDrmChange && drmSession != null)
                return;

            var newSession = drmManager.CreateDRMSession(data);

            // Do not reset wait for DRM event. If there is no valid session
            // do not want to append new data
            //
            if (newSession == null)
                return;

            Logger.Info($"{streamType}: New DRM session found");
            forceDrmChange = false;

            // Decrement use counter for packet stream on old DRM Session
            //
            drmSession?.Release();

            // Initialize reference counting and session.
            //
            newSession.InitializeReferenceCounting();
            newSession.Initialize();

            // Set new session as current & let data submitters run wild.
            // There is no need to store sessions. They live in player queue
            //
            drmSession = newSession;
        }

        public void Dispose()
        {
            OnClearStream();
        }
    }
}
