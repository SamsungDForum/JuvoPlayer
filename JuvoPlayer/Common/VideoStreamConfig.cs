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
using System.Drawing;
using System.Text;

namespace JuvoPlayer.Common
{
    public sealed class VideoStreamConfig : StreamConfig, IEquatable<VideoStreamConfig>
    {
        public override StreamType StreamType()
        {
            return Common.StreamType.Video;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as VideoStreamConfig);
        }

        public bool Equals(VideoStreamConfig other)
        {
            return other != null &&
                   Codec == other.Codec &&
                   CodecProfile == other.CodecProfile &&
                   Size == other.Size &&
                   FrameRateNum == other.FrameRateNum &&
                   FrameRateDen == other.FrameRateDen &&
                   BitRate == other.BitRate;
        }

        public override int GetHashCode()
        {
            var hashCode = -1742922824;
            hashCode = hashCode * -1521134295 + Codec.GetHashCode();
            hashCode = hashCode * -1521134295 + CodecProfile.GetHashCode();
            hashCode = hashCode * -1521134295 + Size.GetHashCode();
            hashCode = hashCode * -1521134295 + FrameRateNum.GetHashCode();
            hashCode = hashCode * -1521134295 + FrameRateDen.GetHashCode();
            hashCode = hashCode * -1521134295 + BitRate.GetHashCode();
            return hashCode;
        }

        public VideoCodec Codec { get; set; }
        //TODO(p.galiszewsk)
        public int CodecProfile { get; set; }
        public Size Size { get; set; }
        public int FrameRateNum { get; set; }
        public int FrameRateDen { get; set; }
        public int BitRate { get; set; }
        public int FrameRate => FrameRateNum / (FrameRateDen == 0 ? 1 : FrameRateDen);
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Video Configuration:");
            sb.AppendLine("\tCodec = " + Codec);
            sb.AppendLine("\tSize    " + Size);
            sb.AppendLine("\tFrameRate = (" + FrameRateNum + "/" + FrameRateDen + ") " + FrameRate);

            return sb.ToString();
        }

        public bool Compatible(VideoStreamConfig other)
        {
            return other != null &&
                   Codec == other.Codec &&
                   FrameRate == other.FrameRate;
        }
    }
}