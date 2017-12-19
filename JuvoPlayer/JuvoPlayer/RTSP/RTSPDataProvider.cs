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
using System.IO;
using Tizen;
using Tizen.Applications;

namespace JuvoPlayer.RTSP
{
    public class RTSPDataProvider : IDataProvider
    {
        private IDemuxer demuxer;
        private IRTSPClient rtpClient;
        private ClipDefinition currentClip;
        public RTSPDataProvider(IDemuxer demuxer, IRTSPClient rtpClient, ClipDefinition currentClip)
        {
            this.demuxer = demuxer ?? throw new ArgumentNullException("demuxer cannot be null");
            this.rtpClient = rtpClient ?? throw new ArgumentNullException("rtpClient cannot be null");
            this.currentClip = currentClip ?? throw new ArgumentNullException("clip cannot be null");

            this.demuxer.StreamConfigReady += OnStreamConfigReady;
            this.demuxer.StreamPacketReady += OnStreamPacketReady;
        }

        ~RTSPDataProvider()
        {
            rtpClient?.Stop();
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

        }

        public void OnPlay()
        {

        }

        public void OnSeek(double time)
        {

        }

        public void Start()
        {
            if (rtpClient == null)
                return;

            rtpClient.Start(currentClip);
            demuxer.Start();
        }
    }
}