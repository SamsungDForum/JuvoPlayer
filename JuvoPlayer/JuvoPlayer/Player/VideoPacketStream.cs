// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using JuvoPlayer.Common;
using JuvoPlayer.DRM;
using System;

namespace JuvoPlayer.Player
{
    public class VideoPacketStream : IPacketStream
    {
        private IDRMManager drmManager;
        private IDRMSession drmSession;
        private IPlayerAdapter playerAdapter;
        private VideoStreamConfig config;

        private bool forceDrmChange;

        public VideoPacketStream(IPlayerAdapter player, IDRMManager drmManager)
        {
            this.drmManager = drmManager ?? throw new ArgumentNullException("drmManager cannot be null");
            this.playerAdapter = player ?? throw new ArgumentNullException("player cannot be null");
        }

        public void OnAppendPacket(StreamPacket packet)
        {
            if (packet.StreamType != StreamType.Video)
                throw new ArgumentException("packet should be video");

            if (packet.IsEOS && config == null)
                return;

            if (drmSession != null && packet is EncryptedStreamPacket)
                packet = drmSession.DecryptPacket(packet);

            playerAdapter.AppendPacket(packet);
        }

        public void OnStreamConfigChanged(StreamConfig config)
        {
            if (!(config is VideoStreamConfig))
                throw new ArgumentException("config should be videoconfig");

            forceDrmChange = this.config != null && !this.config.Equals(config);

            this.config = config as VideoStreamConfig ?? throw new ArgumentNullException("config cannot be null");

            playerAdapter.SetVideoStreamConfig(this.config);
        }

        public void OnClearStream()
        {
            drmSession = null;
        }

        public void OnDRMFound(DRMInitData data)
        {
            if (!forceDrmChange && drmSession != null)
                return;

            forceDrmChange = false;
            drmSession = drmManager.CreateDRMSession(data);
            drmSession?.Start();
        }
    }
}