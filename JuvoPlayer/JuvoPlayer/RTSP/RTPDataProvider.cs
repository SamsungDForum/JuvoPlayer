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
using System.IO;
using Tizen.Applications;

namespace JuvoPlayer.RTSP
{
    public class RTPDataProvider : IDataProvider
    {
        public RTPDataProvider(string url)
        {
            var ffmpegPath = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Application.Current.ApplicationInfo.ExecutablePath)), "lib");
            if (!FFmpeg.FFmpeg.Initialized)
            {
                FFmpeg.FFmpeg.Initialize(ffmpegPath);
            }
        }

        event DRMDataFound IDataProvider.DRMDataFound
        {
            add
            {
                throw new System.NotImplementedException();
            }

            remove
            {
                throw new System.NotImplementedException();
            }
        }

        event StreamConfigReady IDataProvider.StreamConfigReady
        {
            add
            {
                throw new System.NotImplementedException();
            }

            remove
            {
                throw new System.NotImplementedException();
            }
        }

        event StreamPacketReady IDataProvider.StreamPacketReady
        {
            add
            {
                throw new System.NotImplementedException();
            }

            remove
            {
                throw new System.NotImplementedException();
            }
        }

        event StreamsFound IDataProvider.StreamsFound
        {
            add
            {
                throw new System.NotImplementedException();
            }

            remove
            {
                throw new System.NotImplementedException();
            }
        }

        public void DRMDataFound(DRMData data)
        {

        }

        public void OnChangeRepresentation(int representationId)
        {

        }

        public void OnPlay(string url)
        {

        }

        public void OnSeek(double time)
        {

        }
    }
}