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
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Player.EsPlayer.Stream.Buffering;
using static JuvoPlayer.Tests.UnitTests.StreamBufferStreamSynchronizerHelpers;
using NUnit.Framework;


namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    public class TSStreamBufferController
    {
        [Test]
        public void DataEventsAfterSubscription()
        {
            Assert.DoesNotThrow(
                () =>
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


                        SpinWait.SpinUntil(() => eventCount == 2, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(
                            dataArgsHolder[(int)StreamType.Audio] != null &&
                            dataArgsHolder[(int)StreamType.Video] != null);
                    }
                });
        }


        [Test]
        public void NoBufferEventAfterSubscription()
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

                        Assert.IsTrue(eventCount == 0);
                    }
                });
        }

        [Test]
        public void DataEventAfterSingleConfigurationChange()
        {
            Assert.DoesNotThrow(
                () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataArgs[(int)StreamType.Count];

                        bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                dataArgsHolder[(int)a.StreamType] = a;

                            }, SynchronizationContext.Current);

                        var conf = new MetaDataStreamConfig
                        {
                            Bandwidth = 123456789,
                            BufferDuration = TimeSpan.FromSeconds(15),
                            Stream = StreamType.Audio
                        };
                        bufferController.SetMetaDataConfiguration(conf);

                        SpinWait.SpinUntil(() => dataArgsHolder[(int)StreamType.Audio].CompareMetaData(conf),
                            TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(
                            dataArgsHolder[(int)StreamType.Audio].CompareMetaData(conf));

                    }
                });
        }

        [Test]
        public void DataEventsOnMultipleConfigurationChange()
        {
            Assert.DoesNotThrow(
                () =>
                {
                    using (var bufferController = new StreamBufferController())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataArgs[(int)StreamType.Count];

                        var configsToSet = 0;

                        bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                dataArgsHolder[(int)a.StreamType] = a;
                                configsToSet++;

                            }, SynchronizationContext.Current);

                        var audio = new MetaDataStreamConfig
                        {
                            Bandwidth = 987654321,
                            BufferDuration = TimeSpan.FromSeconds(15),
                            Stream = StreamType.Audio
                        };

                        var video = new MetaDataStreamConfig
                        {
                            Bandwidth = 123456789,
                            BufferDuration = TimeSpan.FromSeconds(13),
                            Stream = StreamType.Video
                        };

                        bufferController.SetMetaDataConfiguration(audio);
                        bufferController.SetMetaDataConfiguration(video);

                        SpinWait.SpinUntil(() => configsToSet == 2, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(
                            dataArgsHolder[(int)StreamType.Audio].CompareMetaData(audio));

                        Assert.IsTrue(
                            dataArgsHolder[(int)StreamType.Video].CompareMetaData(video));
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

                        await bufferController.PushPackets(audioPackets);
                        await bufferController.PullPackets(audioPackets);

                        SpinWait.SpinUntil(() => eventOnCount == 1, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(eventCount == 1);
                        Assert.IsTrue(eventOnCount == 1);

                        await audioPackets.DisposePackets();
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


                        var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                        var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100);

                        await Task.WhenAll(
                            bufferController.PushPackets(audio),
                            bufferController.PushPackets(video)
                        );

                        await Task.WhenAll(
                            bufferController.PullPackets(audio),
                            bufferController.PullPackets(video)
                        );

                        SpinWait.SpinUntil(() => eventOnCount < 1, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(eventCount < 1);
                        Assert.IsTrue(eventOnCount < 1);

                        await Task.WhenAll(
                            audio.DisposePackets(),
                            video.DisposePackets()
                        );
                    }
                });
        }

        [Test]
        public void BufferOnOffEventFromSingleSource()
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
                        await bufferController.PushPackets(audioPackets);
                        await bufferController.PullPackets(audioPackets);

                        SpinWait.SpinUntil(() => eventOnCount == 1, TimeSpan.FromSeconds(1.5));

                        await bufferController.PushPackets(audioPackets);

                        SpinWait.SpinUntil(() => eventOnCount == 1 && eventOffCount == 1, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(eventOffCount == 1);
                        Assert.IsTrue(eventOnCount == 1);

                        await audioPackets.DisposePackets();

                    }
                });
        }

        [Test]
        public void BufferOnOffEventsFromMultipleSources()
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

                        var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                        var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100);

                        await Task.WhenAll(
                            bufferController.PushPackets(audio),
                            bufferController.PushPackets(video)
                        );

                        await Task.WhenAll(
                            bufferController.PullPackets(audio),
                            bufferController.PullPackets(video)
                        );

                        SpinWait.SpinUntil(() => eventOnCount == 1, TimeSpan.FromSeconds(1.5));
                        Assert.IsTrue(eventOnCount == 1);
                        Assert.IsTrue(eventOffCount == 0);
                        Assert.IsTrue(eventCount == 1);
                        Assert.IsTrue(bufferState == true);

                        await bufferController.PushPackets(video);

                        await Task.Delay(TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(eventOnCount == 1);
                        Assert.IsTrue(eventOffCount == 0);
                        Assert.IsTrue(eventCount == 1);
                        Assert.IsTrue(bufferState == true);

                        await bufferController.PushPackets(audio);

                        SpinWait.SpinUntil(() => eventOnCount == 1 && eventOffCount == 1, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(eventCount == 2);
                        Assert.IsTrue(eventOnCount == 1);
                        Assert.IsTrue(eventOffCount == 1);
                        Assert.IsTrue(bufferState == false);

                        await Task.WhenAll(
                            audio.DisposePackets(),
                            video.DisposePackets()
                        );
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
                        await bufferController.PushPackets(audioPackets);


                        SpinWait.SpinUntil(() => eventCount >= 2, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(eventCount >= 2);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].DurationRequired <= TimeSpan.Zero);

                        await bufferController.PullPackets(audioPackets);
                        SpinWait.SpinUntil(() => eventCount >= 3, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(eventCount >= 3);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].DurationRequired > TimeSpan.Zero);

                        await audioPackets.DisposePackets();

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

                        var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                        var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100);

                        await Task.WhenAll(
                            bufferController.PushPackets(audio),
                            bufferController.PushPackets(video));

                        SpinWait.SpinUntil(() => eventCount >= 2, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(eventCount >= 2);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].DurationRequired <= TimeSpan.Zero);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].DurationRequired <= TimeSpan.Zero);

                        await Task.WhenAll(
                            bufferController.PullPackets(audio),
                            bufferController.PullPackets(video));

                        SpinWait.SpinUntil(() => eventCount >= 4, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(eventCount >= 4);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].DurationRequired > TimeSpan.Zero);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].DurationRequired > TimeSpan.Zero);

                        await Task.WhenAll(
                            audio.DisposePackets(),
                            video.DisposePackets()
                        );
                    }
                });
        }

        [Test]
        public void NoEventsWhenEventsDisabled()
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

                        var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                        var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100);

                        await Task.WhenAll(
                            bufferController.PushPackets(audio),
                            bufferController.PushPackets(video));

                        SpinWait.SpinUntil(() => eventCount >= 2, TimeSpan.FromSeconds(1.5));

                        await Task.WhenAll(
                            bufferController.PullPackets(audio),
                            bufferController.PullPackets(video));

                        SpinWait.SpinUntil(() => eventCount >= 4, TimeSpan.FromSeconds(1.5));

                        bufferController.DisableEvents();
                        var currEventCount = eventCount;

                        await Task.WhenAll(
                            bufferController.PushPackets(audio),
                            bufferController.PushPackets(video));

                        await Task.Delay(500);

                        await Task.WhenAll(
                            bufferController.PullPackets(audio),
                            bufferController.PullPackets(video));

                        await Task.Delay(500);
                        Assert.IsTrue(eventCount == currEventCount);

                        await Task.WhenAll(
                            audio.DisposePackets(),
                            video.DisposePackets()
                        );
                    }
                });
        }

        [Test]
        public void BufferFullBufferActualReported()
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

                        var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(7), 100);
                        var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(7), 100);

                        await Task.WhenAll(
                            bufferController.PushPackets(audio),
                            bufferController.PushPackets(video));

                        SpinWait.SpinUntil(() => eventCount >= 2, TimeSpan.FromSeconds(1.5));

                        var audioLevel = dataArgsHolder[(int)StreamType.Audio].DurationRequired;
                        var videoLevel = dataArgsHolder[(int)StreamType.Audio].DurationRequired;

                        bufferController.ReportFullBuffer();
                        bufferController.PublishBufferState();

                        SpinWait.SpinUntil(() => eventCount >= 4, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].DurationRequired == TimeSpan.Zero);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].DurationRequired == TimeSpan.Zero);

                        bufferController.ReportActualBuffer();
                        bufferController.PublishBufferState();

                        SpinWait.SpinUntil(() => eventCount >= 6, TimeSpan.FromSeconds(1.5));

                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].DurationRequired == audioLevel);
                        Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].DurationRequired == videoLevel);

                        await Task.WhenAll(
                            audio.DisposePackets(),
                            video.DisposePackets()
                        );
                    }
                });
        }

    }
}
