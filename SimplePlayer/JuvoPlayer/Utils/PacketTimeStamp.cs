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

namespace JuvoPlayer.Common.Utils
{
    public struct PacketTimeStamp
    {
        public TimeSpan Pts;
        public TimeSpan Dts;

        public PacketTimeStamp(Packet packet)
        {
            var offset = TimeSpan.FromTicks(Math.Min(packet.Pts.Ticks, packet.Dts.Ticks));
            Pts = offset;
            Dts = offset;
        }

        public static PacketTimeStamp operator +(PacketTimeStamp clock, TimeSpan time)
        {
            clock.Pts += time;
            clock.Dts += time;
            return clock;
        }

        public static PacketTimeStamp operator -(PacketTimeStamp clock, TimeSpan time)
        {
            clock.Pts -= time;
            clock.Dts -= time;
            return clock;
        }

        public static Packet operator +(Packet packet, PacketTimeStamp clock)
        {
            packet.Pts += clock.Pts;
            packet.Dts += clock.Dts;
            return packet;
        }

        public static Packet operator -(Packet packet, PacketTimeStamp clock)
        {
            packet.Pts -= clock.Pts;
            packet.Dts -= clock.Dts;
            return packet;
        }

        public static explicit operator PacketTimeStamp(Packet packet)
        {
            return new PacketTimeStamp
            {
                Pts = packet.Pts,
                Dts = packet.Dts
            };
        }

        public void SetClock(Packet packet)
        {
            Pts = packet.Pts;
            Dts = packet.Dts;
        }

        public void SetClock(TimeSpan time)
        {
            Pts = time;
            Dts = time;
        }

        public void Reset()
        {
            SetClock(TimeSpan.Zero);
        }

        public override string ToString()
        {
            return $"PTS: {Pts} DTS: {Dts}";
        }
    }
}