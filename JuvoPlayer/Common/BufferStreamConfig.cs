/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2019, Samsung Electronics Co., Ltd
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
    public sealed class BufferStreamConfig : StreamConfig, IEquatable<BufferStreamConfig>
    {
        public StreamType Stream { get; internal set; }
        public uint? Bandwidth { get; internal set; }
        public TimeSpan? MinBufferTime { get; internal set; }
        public TimeSpan BufferDuration { get; internal set; }

        public bool Equals(BufferStreamConfig other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Bandwidth == other.Bandwidth &&
                   MinBufferTime.Equals(other.MinBufferTime) &&
                   Stream.Equals(other.Stream);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is BufferStreamConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Bandwidth.GetHashCode();
                hashCode = (hashCode * 397) ^ MinBufferTime.GetHashCode();
                hashCode = (hashCode * 397) ^ Stream.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(BufferStreamConfig left, BufferStreamConfig right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(BufferStreamConfig left, BufferStreamConfig right)
        {
            return !Equals(left, right);
        }

        public override StreamType StreamType()
        {
            return Stream;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("\nMetaData Configuration:");
            sb.AppendLine("\tStream         = " + Stream);
            sb.AppendLine("\tBandwidth      = " + (Bandwidth.HasValue ? Bandwidth.ToString() : "N/A"));
            sb.AppendLine("\tMinBufferTime  = " + (MinBufferTime.HasValue ? MinBufferTime.ToString() : "N/A"));
            sb.AppendLine("\tBufferDuration = " + BufferDuration);

            return sb.ToString();
        }
    }
}
