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
    public class AudioStreamConfig : StreamConfig, IEquatable<AudioStreamConfig>
    {
        public string MimeType { get; }

        public int ChannelLayout { get; }

        public int SampleRate { get; }

        public int BitsPerChannel { get; }

        public long BitRate { get; }

        public string Language { get; }

        public AudioStreamConfig(
            byte[] codecExtraData,
            string mimeType,
            int channelLayout,
            int sampleRate,
            int bitsPerChannel,
            long bitRate,
            string language) : base(codecExtraData)
        {
            MimeType = mimeType;
            ChannelLayout = channelLayout;
            SampleRate = sampleRate;
            BitsPerChannel = bitsPerChannel;
            BitRate = bitRate;
            Language = language;
        }

        public bool Equals(AudioStreamConfig other)
        {
            return other != null &&
                   base.Equals(other) &&
                   string.Equals(MimeType, other.MimeType) &&
                   ChannelLayout == other.ChannelLayout &&
                   SampleRate == other.SampleRate &&
                   BitsPerChannel == other.BitsPerChannel &&
                   BitRate == other.BitRate &&
                   string.Equals(Language, other.Language);
        }

        public override StreamType StreamType()
        {
            return Common.StreamType.Audio;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AudioStreamConfig);
        }

        public override int GetHashCode()
        {
            var hashCode = 1182625657;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + MimeType.GetHashCode();
            hashCode = hashCode * -1521134295 + ChannelLayout.GetHashCode();
            hashCode = hashCode * -1521134295 + SampleRate.GetHashCode();
            hashCode = hashCode * -1521134295 + BitsPerChannel.GetHashCode();
            hashCode = hashCode * -1521134295 + BitRate.GetHashCode();
            hashCode = hashCode * -1521134295 + Language.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Audio Configuration:");
            sb.AppendLine("\tMimeType       = " + MimeType);
            sb.AppendLine("\tBitsPerChannel = " + BitsPerChannel);
            sb.AppendLine("\tChannelLayout  = " + ChannelLayout);
            sb.AppendLine("\tSampleRate     = " + SampleRate);
            sb.AppendLine("\tBitRate        = " + BitRate);
            sb.AppendLine("\tLanguage       = " + Language);

            return sb.ToString();
        }
    }
}
