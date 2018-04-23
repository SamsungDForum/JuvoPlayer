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

using System;

namespace JuvoPlayer.Common
{
    [Serializable]
    public class Packet : IDisposable
    {
        public byte[] Data = null;
        public StreamType StreamType { get; set; }
        public TimeSpan Dts { get; set; }
        public TimeSpan Pts { get; set; }
        public bool IsEOS { get; set; }
        public bool IsKeyFrame { get; set; }

        public static Packet CreateEOS(StreamType streamType)
        {
            return new Packet
            {
                StreamType = streamType,
                Dts = TimeSpan.MaxValue,
                Pts = TimeSpan.MaxValue,
                IsEOS = true
            };
        }

        public virtual void Dispose() { }
    }
}
