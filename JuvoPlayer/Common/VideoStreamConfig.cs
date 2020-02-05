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

        public int CodecProfile { get; set; }
        public Size Size { get; set; }
        public int FrameRateNum { get; set; }
        public int FrameRateDen { get; set; }
        public long BitRate { get; set; }
        public int FrameRate => FrameRateNum / (FrameRateDen == 0 ? 1 : FrameRateDen);

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Video Configuration:");
            sb.AppendLine("\tCodec     = " + Codec);
            sb.AppendLine("\tSize      = " + Size);
            sb.AppendLine("\tFrameRate = (" + FrameRateNum + "/" + FrameRateDen + ") " + FrameRate);
            sb.AppendLine("\tBitRate   = " + BitRate);

            return sb.ToString();
        }
    }
}
