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
        public override string ToString()
        {
            return $"PTS: {Pts} DTS: {Dts}";
        }
    }
}