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

namespace JuvoPlayer.Common
{
    public class StreamPacket
    {
        public byte[] Data = null;
        public StreamType StreamType { get; set; }
        public ulong Dts { get; set; }
        public ulong Pts { get; set; }
        public bool IsEOS { get; set; }
        public bool IsKeyFrame { get; set; }

        public static StreamPacket CreateEOS(StreamType streamType)
        {
            return new StreamPacket
            {
                StreamType = streamType,
                Dts = ulong.MaxValue,
                Pts = ulong.MaxValue,
                IsEOS = true
            };
        }
    }
}
