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
using System.Text;

namespace JuvoPlayer.Common
{
    public sealed class AudioStreamConfig : StreamConfig, IEquatable<AudioStreamConfig>
    {
        public override StreamType StreamType()
        {
            return Common.StreamType.Audio;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AudioStreamConfig);
        }

        public bool Equals(AudioStreamConfig other)
        {
            return other != null &&
                   Codec == other.Codec &&
                   ChannelLayout == other.ChannelLayout &&
                   SampleRate == other.SampleRate &&
                   BitsPerChannel == other.BitsPerChannel &&
                   BitRate == other.BitRate;
        }

        public override int GetHashCode()
        {
            var hashCode = 1182625657;
            hashCode = hashCode * -1521134295 + Codec.GetHashCode();
            hashCode = hashCode * -1521134295 + ChannelLayout.GetHashCode();
            hashCode = hashCode * -1521134295 + SampleRate.GetHashCode();
            hashCode = hashCode * -1521134295 + BitsPerChannel.GetHashCode();
            hashCode = hashCode * -1521134295 + BitRate.GetHashCode();
            return hashCode;
        }

        public AudioCodec Codec { get; set; }
        public int ChannelLayout { get; set; }
        public int SampleRate { get; set; }
        public int BitsPerChannel { get; set; }
        public int BitRate { get; set; }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Audio Configuration:");
            sb.AppendLine("\tCodec = " + Codec);
            sb.AppendLine("\tBitsPerChannel =" + BitsPerChannel);
            sb.AppendLine("\tChannelLayout = " + ChannelLayout);
            sb.AppendLine("\tSampleRate = " + SampleRate);

            return sb.ToString();
        }

        // Audio compatibility criteria: 
        // All parameters must match
        public bool Compatible(AudioStreamConfig other) => Equals(other);
    }
}
