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
using System;

namespace JuvoPlayer.Player
{
    public class VideoPacketStream : IPacketStream
    {
        private IPlayerAdapter playerAdapter;
        private StreamConfig config;

        public VideoPacketStream(IPlayerAdapter player, StreamConfig config)
        {
            playerAdapter = player ?? throw new ArgumentNullException("player cannot be null");
            this.config = config ?? throw new ArgumentNullException("config cannot be null");

            if (!(config is VideoStreamConfig))
                throw new ArgumentException("config should be videoconfig");

            playerAdapter.SetVideoStreamConfig(config as VideoStreamConfig);
        }

        public void OnAppendPacket(StreamPacket packet)
        {
            if (packet.StreamType != StreamType.Video)
                throw new ArgumentException("packet should be video");

            playerAdapter.AppendPacket(packet);
        }

        public void OnClearStream()
        {

        }

        public void OnDRMFound(DRMData data)
        {

        }
    }
}