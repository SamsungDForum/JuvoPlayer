/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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
        public long BitRate { get; set; }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Audio Configuration:");
            sb.AppendLine("\tCodec          = " + Codec);
            sb.AppendLine("\tBitsPerChannel = " + BitsPerChannel);
            sb.AppendLine("\tChannelLayout  = " + ChannelLayout);
            sb.AppendLine("\tSampleRate     = " + SampleRate);
            sb.AppendLine("\tBitRate        = " + BitRate);

            return sb.ToString();
        }
    }
}
