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
using System.Collections.Generic;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Player.EsPlayer.Stream.Buffering;

namespace JuvoPlayer.Tests.UnitTests
{
    internal class dummyStorage : IDataStorage
    {
        public int Length => 1;

        public void Dispose()
        {

        }
    }

    internal static class StreamBufferStreamSynchronizerHelpers
    {
        public static bool CompareDataArgs(this DataArgs a, DataArgs b)
        {
            return a.StreamType == b.StreamType &&
                   a.DurationRequired == b.DurationRequired &&
                   a.SequenceId == b.SequenceId;
        }

        public static bool CompareMetaData(this DataArgs a, MetaDataStreamConfig b)
        {
            return a.StreamType == b.StreamType() &&
                   a.DurationRequired == b.BufferDuration;
        }

        public static async Task PushPackets(this StreamBufferController streamBuffer, IEnumerable<Packet> source)
        {
            foreach (var packet in source)
            {
                streamBuffer.DataIn(packet);
                await Task.Yield();
            }
        }

        public static async Task PullPackets(this StreamBufferController streamBuffer, IEnumerable<Packet> source)
        {
            foreach (var packet in source)
            {
                streamBuffer.DataOut(packet);
                await Task.Yield();
            }
        }

        public static async Task DisposePackets(this IEnumerable<Packet> source)
        {
            foreach (var packet in source)
            {
                packet.Dispose();
            }

            await Task.Yield();
        }

        public static IEnumerable<Packet> BuildPacketList(StreamType type, TimeSpan duration, int maxPacketCount, TimeSpan? startTime = null)
        {
            var packetList = new List<Packet>();

            var packetCount = 0;
            var packetDtsDuration =
                duration.TotalMilliseconds / maxPacketCount;

            if (!startTime.HasValue)
                startTime = TimeSpan.Zero;

            while (packetCount <= maxPacketCount)
            {
                var packet = new Packet
                {
                    Dts = startTime.Value + TimeSpan.FromMilliseconds(packetDtsDuration * packetCount),
                    Storage = new dummyStorage(),
                    StreamType = type
                };

                packetList.Add(packet);
                packetCount++;
            }

            return packetList;
        }

    }
}
