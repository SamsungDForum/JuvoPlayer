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

namespace JuvoPlayer.HLS
{
    public class HLSDataProvider : IDataProvider
    {
        private IDemuxer demuxer;
        private ClipDefinition currentClip;
        public HLSDataProvider(IDemuxer demuxer, ClipDefinition currentClip)
        {
            this.demuxer = demuxer ?? throw new ArgumentNullException("demuxer cannot be null");
            this.currentClip = currentClip ?? throw new ArgumentNullException("clip cannot be null");

            this.demuxer.ClipDuration += OnClipDurationChanged;
            this.demuxer.StreamConfigReady += OnStreamConfigReady;
            this.demuxer.StreamPacketReady += OnStreamPacketReady;
        }

        public event ClipDurationChanged ClipDurationChanged;
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event StreamPacketReady StreamPacketReady;
        public event StreamsFound StreamsFound;

        private void OnClipDurationChanged(double clipDuration)
        {
            ClipDurationChanged?.Invoke(clipDuration);
        }

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
            // found empty packet which means EOS. We need to send two fake 
            // eos packets, one for audio and one for video
            StreamPacketReady?.Invoke(StreamPacket.CreateEOS(StreamType.Audio));
            StreamPacketReady?.Invoke(StreamPacket.CreateEOS(StreamType.Video));
        }

        public void OnChangeRepresentation(int representationId)
        {

        }

        public void OnPaused()
        {
            demuxer.Paused();
        }

        public void OnPlayed()
        {
            demuxer.Played();
        }

        public void OnSeek(double time)
        {

        }

        public void OnStopped()
        {
        }

        public void Start()
        {
            demuxer.StartForUrl(currentClip.Url);
        }

        public void OnTimeUpdated(double time)
        {
        }
    }
}