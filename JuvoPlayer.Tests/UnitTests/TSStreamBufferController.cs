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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Player.EsPlayer;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    internal class dummyStorage : IDataStorage
    {
        public int Length => 1;

        public void Dispose()
        {

        }
    }

    [TestFixture]
    public class TSStreamBufferController
    {
        private IList<Packet> BuildPacketList(StreamType type, TimeSpan duration, int maxPacketCount, TimeSpan? startTime=null)
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
        /*
        [Test]
        public void DataEventsAfterSubscription()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataArgs[(int)StreamType.Count];

                        var eventCount = 0;
                        bufferController.DataNeededStateChanged().Subscribe(
                            a =>
                            {
                                dataArgsHolder[(int)a.StreamType] = a;
                                eventCount++;
                            }, SynchronizationContext.Current);


                        if (eventCount != 2)
                            await Task.Delay(TimeSpan.FromSeconds(1));


                        Assert.IsTrue(
                            dataArgsHolder[(int)StreamType.Audio] != null &&
                            dataArgsHolder[(int)StreamType.Video] != null);
                    }
                });
        }

        
        [Test]
        public void BufferEventAfterSubscription()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);

                        var eventCount = 0;

                        bufferController.BufferingStateChanged()
                            .Subscribe(a => eventCount++
                                , SynchronizationContext.Current);

                        if (eventCount == 0)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 1);
                    }
                });
        }

        [Test]
        public void DataEventOnSingleConfigurationChange()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataArgs[(int)StreamType.Count];

                        var eventCount = 0;

                        bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                dataArgsHolder[(int)a.StreamType] = a;
                                eventCount++;
                            }, SynchronizationContext.Current);


                        if (eventCount != 2)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 2);
                        dataArgsHolder = new DataArgs[(int)StreamType.Count];


                        bufferController.SetMetaDataConfiguration(new MetaDataStreamConfig
                        {
                            Bandwidth = 0,
                            BufferDuration = TimeSpan.FromSeconds(10),
                            Stream = StreamType.Audio
                        });

                        if (eventCount != 3)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 3);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio] != null);

                    }
                });
        }

        [Test]
        public void DataEventsOnMultipleConfigurationChange()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataArgs[(int)StreamType.Count];

                        // 2 Configs coming from Subscribe,
                        // 2 configs coming from Set Configuration
                        var configsToSet = 0;

                        bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                dataArgsHolder[(int)a.StreamType] = a;
                                configsToSet++;

                            }, SynchronizationContext.Current);

                        bufferController.SetMetaDataConfiguration(new MetaDataStreamConfig
                        {
                            Bandwidth = 0,
                            BufferDuration = TimeSpan.FromSeconds(10),
                            Stream = StreamType.Audio
                        });

                        bufferController.SetMetaDataConfiguration(new MetaDataStreamConfig
                        {
                            Bandwidth = 0,
                            BufferDuration = TimeSpan.FromSeconds(10),
                            Stream = StreamType.Video
                        });

                        if (configsToSet != 4)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        var r = dataArgsHolder.Count(a => a == null);
                        Assert.IsTrue(r == dataArgsHolder.Length - 2);
                    }
                });
        }

        [Test]
        public void NoDuplicateDataEventsOnSameConfigurationChange()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataArgs[(int)StreamType.Count];

                        var eventCount = 0;

                        bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                dataArgsHolder[(int)a.StreamType] = a;
                                eventCount++;

                            }, SynchronizationContext.Current);

                        bufferController.SetMetaDataConfiguration(new MetaDataStreamConfig
                        {
                            Bandwidth = 0,
                            BufferDuration = TimeSpan.FromSeconds(10),
                            Stream = StreamType.Video
                        });

                        bufferController.SetMetaDataConfiguration(new MetaDataStreamConfig
                        {
                            Bandwidth = 0,
                            BufferDuration = TimeSpan.FromSeconds(10),
                            Stream = StreamType.Video
                        });

                        if (eventCount != 3)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 3);
                    }
                });
        }

        [Test]
        public void MultipleEventsOnSameStreamConfigurationChange()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataArgs[(int)StreamType.Count];

                        var eventCount = 0;

                        bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                dataArgsHolder[(int)a.StreamType] = a;
                                eventCount++;

                            }, SynchronizationContext.Current);

                        for (var i = 0; i < 10; i++)
                        {
                            bufferController.SetMetaDataConfiguration(new MetaDataStreamConfig
                            {
                                Bandwidth = (uint?)i,
                                BufferDuration = TimeSpan.FromSeconds(i),
                                Stream = StreamType.Video
                            });
                        }

                        if (eventCount != 12)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 12);
                    }
                });
        }

        [Test]
        public void BufferOnEventFromSingleSource()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);

                        var eventCount = 0;
                        var eventOnCount = 0;

                        bufferController.BufferingStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                if (a)
                                    eventOnCount++;
                            }, SynchronizationContext.Current);

                        var audioPackets = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                        foreach (var packet in audioPackets)
                            bufferController.DataIn(packet);

                        foreach (var packet in audioPackets)
                            bufferController.DataOut(packet);

                        if (eventCount != 2 || eventOnCount != 1)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 2);
                        Assert.IsTrue(eventOnCount == 1);

                        foreach (var packet in audioPackets)
                            packet.Dispose();
                    }
                });
        }

        [Test]
        public void BufferOnEventFromMultipleSources()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);

                        var eventCount = 0;
                        var eventOnCount = 0;

                        bufferController.BufferingStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                if (a)
                                    eventOnCount++;
                            }, SynchronizationContext.Current);

                        IList<Packet>[] avPackets = {
                            BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100),
                            BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100)
                        };

                        avPackets.AsParallel().ForAll(packetList =>
                            {
                                foreach (var packet in packetList)
                                    bufferController.DataIn(packet);
                            }
                        );

                        avPackets.AsParallel().ForAll(packetList =>
                            {
                                foreach (var packet in packetList)
                                    bufferController.DataOut(packet);
                            }
                        );

                        if (eventCount != 2 || eventOnCount != 1)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 2);
                        Assert.IsTrue(eventOnCount == 1);

                        avPackets.AsParallel().ForAll(packetList =>
                            {
                                foreach (var packet in packetList)
                                    packet.Dispose();
                            }
                        );
                    }
                });
        }

        [Test]
        public void BufferOffEventFromSingleSource()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);

                        var eventCount = 0;
                        var eventOnCount = 0;
                        var eventOffCount = 0;

                        bufferController.BufferingStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                if (a)
                                    eventOnCount++;
                                else
                                    eventOffCount++;

                            }, SynchronizationContext.Current);

                        var audioPackets = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                        foreach (var packet in audioPackets)
                            bufferController.DataIn(packet);

                        foreach (var packet in audioPackets)
                            bufferController.DataOut(packet);

                        if (eventCount != 2 || eventOnCount != 1)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 2);
                        Assert.IsTrue(eventOnCount == 1);
                        Assert.IsTrue(eventOffCount == 1);

                        bufferController.ResetBuffers();

                        foreach (var packet in audioPackets)
                        {
                            bufferController.DataIn(packet);
                        }

                        if (eventOffCount != 2)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 3);
                        Assert.IsTrue(eventOnCount == 1);
                        Assert.IsTrue(eventOffCount == 2);

                        foreach (var packet in audioPackets)
                            packet.Dispose();

                    }
                });
        }

        [Test]
        public void BufferOffEventsFromMultipleSources()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);

                        var eventCount = 0;
                        var eventOnCount = 0;
                        var eventOffCount = 0;
                        var bufferState = false;

                        bufferController.BufferingStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                if (a)
                                    eventOnCount++;
                                else
                                    eventOffCount++;

                                bufferState = a;

                            }, SynchronizationContext.Current);



                        IList<Packet>[] avPackets = {
                            BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100),
                            BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100)
                        };

                        if (eventCount != 1 || eventOffCount != 1 || bufferState == true)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 1);
                        Assert.IsTrue(eventOnCount == 0);
                        Assert.IsTrue(eventOffCount == 1);
                        Assert.IsTrue(bufferState == false);

                        avPackets.AsParallel().ForAll(packetList =>
                            {
                                foreach (var packet in packetList)
                                    bufferController.DataIn(packet);
                            }
                        );

                        avPackets.AsParallel().ForAll(packetList =>
                            {
                                foreach (var packet in packetList)
                                    bufferController.DataOut(packet);
                            }
                        );

                        if (eventCount != 2 || eventOnCount != 1)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 2);
                        Assert.IsTrue(eventOnCount == 1);
                        Assert.IsTrue(eventOffCount == 1);
                        Assert.IsTrue(bufferState == true);

                        avPackets.AsParallel().ForAll(packetList =>
                        {
                            foreach (var packet in packetList)
                                packet.Dispose();
                        });

                        avPackets = new IList<Packet>[] {
                            BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100, TimeSpan.FromSeconds(15.5)),
                            BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100,TimeSpan.FromSeconds(15.5))
                        };

                        var vPackets = avPackets[0];
                        foreach (var packet in vPackets)
                            bufferController.DataIn(packet);

                        await Task.Delay(TimeSpan.FromSeconds(0.5));

                        Assert.IsTrue(eventCount == 2);
                        Assert.IsTrue(eventOnCount == 1);
                        Assert.IsTrue(eventOffCount == 1);
                        Assert.IsTrue(bufferState == true);

                        var aPackets = avPackets[1];
                        foreach (var packet in aPackets)
                            bufferController.DataIn(packet);

                        if (eventCount != 3 || eventOffCount !=2 || bufferState != false)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 3);
                        Assert.IsTrue(eventOnCount == 1);
                        Assert.IsTrue(eventOffCount == 2);
                        Assert.IsTrue(bufferState == false);

                        avPackets.AsParallel().ForAll(packetList =>
                            {
                                foreach (var packet in packetList)
                                    packet.Dispose();
                            });
                    }
                });
        }

        [Test]
        public void DataOffOnEventFromSingleSource()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataArgs[(int)StreamType.Count];

                        var eventCount = 0;


                        bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                dataArgsHolder[(int)a.StreamType] = a;

                            }, SynchronizationContext.Current);

                        var audioPackets = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                        foreach (var packet in audioPackets)
                            bufferController.DataIn(packet);


                        if (eventCount != 3)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 3);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].DurationRequired <= TimeSpan.Zero);

                        foreach (var packet in audioPackets)
                            bufferController.DataOut(packet);


                        if (eventCount != 4)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 4);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].DurationRequired > TimeSpan.Zero);

                        foreach (var packet in audioPackets)
                            packet.Dispose();

                    }
                });
        }

        [Test]
        public void DataOffOnEventFromMultipleSources()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataArgs[(int)StreamType.Count];

                        var eventCount = 0;


                        bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                dataArgsHolder[(int)a.StreamType] = a;

                            }, SynchronizationContext.Current);

                        IList<Packet>[] avPackets = {
                            BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100),
                            BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100)
                        };

                        avPackets.AsParallel().ForAll(packetList =>
                            {
                                foreach (var packet in packetList)
                                    bufferController.DataIn(packet);
                            }
                        );

                        if (eventCount != 4 ||
                            dataArgsHolder[(int)StreamType.Video].DurationRequired > TimeSpan.Zero ||
                            dataArgsHolder[(int)StreamType.Audio].DurationRequired > TimeSpan.Zero )
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 4);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].DurationRequired <= TimeSpan.Zero);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].DurationRequired <= TimeSpan.Zero);

                        avPackets.AsParallel().ForAll(packetList =>
                            {
                                foreach (var packet in packetList)
                                    bufferController.DataOut(packet);
                            }
                        );

                        if (eventCount != 6||
                            dataArgsHolder[(int)StreamType.Video].DurationRequired <= TimeSpan.Zero ||
                            dataArgsHolder[(int)StreamType.Audio].DurationRequired <= TimeSpan.Zero )
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 6);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].DurationRequired > TimeSpan.Zero);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].DurationRequired > TimeSpan.Zero);

                        avPackets.AsParallel().ForAll(packetList =>
                            {
                                foreach (var packet in packetList)
                                    packet.Dispose();
                            }
                        );
                    }
                });
        }
        */

    }
}
