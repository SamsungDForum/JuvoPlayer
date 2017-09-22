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
using Tizen.Applications;

namespace JuvoPlayer.RTSP
{
    public class RTPDataProvider : IDataProvider
    {
        private ClipDefinition currentClip;
        public RTPDataProvider(ClipDefinition clip)
        {
            currentClip = clip ?? throw new ArgumentNullException("clip cannot be null");
        }

        public event DRMDataFound DRMDataFound;
        public event StreamConfigReady StreamConfigReady;
        public event StreamPacketReady StreamPacketReady;
        public event StreamsFound StreamsFound;

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
            var ffmpegPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Application.Current.ApplicationInfo.ExecutablePath)), "lib");
            if (!FFmpeg.FFmpeg.Initialized)
            {
                FFmpeg.FFmpeg.Initialize(ffmpegPath);
                FFmpeg.FFmpeg.avcodec_register_all();
            }
        }
    }
}