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
using System.Reactive.Linq;
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
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];
                        var eventCount = 0;

                        using (bufferController.DataNeededStateChanged().Subscribe(
                            a =>
                            {
                                dataArgsHolder[(int)a.StreamType] = a;
                                eventCount++;
                            }, SynchronizationContext.Current))
                        {


                            SpinWait.SpinUntil(() => eventCount == 2, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio] != null, "dataArgsHolder[(int)StreamType.Audio] != null");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video] != null, "dataArgsHolder[(int)StreamType.Video] != null");
                            ;
                        }
                    }
                });
        }


        [Test]
        public void NoBufferEventAfterSubscription()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);

                        var eventCount = 0;

                        bufferController.BufferingStateChanged()
                            .Subscribe(a => eventCount++
                                , SynchronizationContext.Current);

                        if (eventCount == 0)
                            await Task.Delay(TimeSpan.FromSeconds(1));

                        Assert.IsTrue(eventCount == 0, $"Expected: eventCount == 0 Result: eventCount == {eventCount}");
                    }
                });
        }

        [Test]
        public void DataEventAfterSingleConfigurationChange()
        {
            Assert.DoesNotThrow(
                () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];

                        using (bufferController.DataNeededStateChanged()
                            .Subscribe(a => { dataArgsHolder[(int)a.StreamType] = a; },
                                SynchronizationContext.Current))
                        {

                            var conf = new MetaDataStreamConfig
                            {
                                Bandwidth = 123456789,
                                BufferDuration = TimeSpan.FromSeconds(15),
                                Stream = StreamType.Audio
                            };
                            bufferController.SetMetaDataConfiguration(conf);

                            SpinWait.SpinUntil(() => dataArgsHolder[(int)StreamType.Audio] != null,
                                TimeSpan.FromSeconds(2));

                            Assert.IsTrue(
                                dataArgsHolder[(int)StreamType.Audio].CompareMetaData(conf), $"Expected: Same Result: {conf} {dataArgsHolder[(int)StreamType.Audio]}");
                        }

                    }
                });
        }

        [Test]
        public void DataEventsOnMultipleConfigurationChange()
        {
            Assert.DoesNotThrow(
                () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];

                        var configsToSet = 0;

                        using (bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                dataArgsHolder[(int)a.StreamType] = a;
                                configsToSet++;

                            }, SynchronizationContext.Current))
                        {

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

                            SpinWait.SpinUntil(() => dataArgsHolder[(int)StreamType.Audio] != null && dataArgsHolder[(int)StreamType.Video] != null, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(
                                dataArgsHolder[(int)StreamType.Audio].CompareMetaData(audio), $"Expected: Same Result: {audio} {dataArgsHolder[(int)StreamType.Audio]}");

                            Assert.IsTrue(
                                dataArgsHolder[(int)StreamType.Video].CompareMetaData(video), $"Expected: Same Result: {video} {dataArgsHolder[(int)StreamType.Audio]}");
                        }
                    }
                });
        }

        [Test]
        public void BufferOnEventFromSingleSource()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);

                        var eventCount = 0;
                        var eventOnCount = 0;

                        using (bufferController.BufferingStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                if (a)
                                    eventOnCount++;
                            }, SynchronizationContext.Current))
                        {

                            var audioPackets = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);

                            await bufferController.PushPackets(audioPackets);
                            await bufferController.PullPackets(audioPackets);

                            SpinWait.SpinUntil(() => eventOnCount > 0 && eventCount > 0, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(eventCount == 1, $"Expected: EventCount==1 Result: EventCount=={eventCount}");
                            Assert.IsTrue(eventOnCount == 1, $"Expected: EventOnCount==1 Result: EventOnCount={eventOnCount}");

                            await audioPackets.DisposePackets();
                        }
                    }
                });
        }

        [Test]
        public void BufferOnEventFromMultipleSources()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);

                        var eventCount = 0;
                        var eventOnCount = 0;

                        using (bufferController.BufferingStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                if (a)
                                    eventOnCount++;
                            }, SynchronizationContext.Current))
                        {


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

                            SpinWait.SpinUntil(() => eventOnCount > 0 && eventCount > 0, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(eventCount > 0, $"Expected: eventCount > 0 Result: eventCount=={eventCount}");
                            Assert.IsTrue(eventOnCount > 0, $"Expected: eventOnCount > 0 Result: eventOnCount=={eventOnCount}");

                            await Task.WhenAll(
                                audio.DisposePackets(),
                                video.DisposePackets()
                            );
                        }
                    }
                });
        }

        [Test]
        public void BufferOnOffEventFromSingleSource()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);

                        var eventCount = 0;
                        var eventOnCount = 0;
                        var eventOffCount = 0;

                        using (bufferController.BufferingStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                if (a)
                                    eventOnCount++;
                                else
                                    eventOffCount++;

                            }, SynchronizationContext.Current))
                        {

                            var audioPackets1 = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                            var audioPackets2 = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100,
                                TimeSpan.FromSeconds(15));

                            await bufferController.PushPackets(audioPackets1);
                            await bufferController.PullPackets(audioPackets1);

                            SpinWait.SpinUntil(() => eventOnCount == 1, TimeSpan.FromSeconds(2));

                            await bufferController.PushPackets(audioPackets2);

                            SpinWait.SpinUntil(() => eventOnCount > 0 && eventOffCount > 0, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(eventOffCount == 1, $"Expected: eventOffCount==1 Result eventOffCount=={eventOffCount}");
                            Assert.IsTrue(eventOnCount == 1, $"Expected: eventOnCount==1 Result eventOnCount=={eventOnCount}");

                            await audioPackets1.DisposePackets();
                            await audioPackets2.DisposePackets();
                        }

                    }
                });
        }

        [Test]
        public void BufferOnOffEventsFromMultipleSources()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);

                        var eventCount = 0;
                        var eventOnCount = 0;
                        var eventOffCount = 0;
                        var bufferState = false;

                        using (bufferController.BufferingStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                if (a)
                                    eventOnCount++;
                                else
                                    eventOffCount++;

                                bufferState = a;

                            }, SynchronizationContext.Current))
                        {

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

                            SpinWait.SpinUntil(() => eventOnCount > 0, TimeSpan.FromSeconds(2));
                            Assert.IsTrue(eventOnCount == 1, $"Expected: eventOnCount==1 Result: eventOnCount=={eventOnCount}");
                            Assert.IsTrue(eventOffCount == 0, $"Expected: eventOffCount==0 Result: eventOffCount=={eventOffCount}");
                            Assert.IsTrue(bufferState, $"Expected: bufferState==true Result bufferState=={bufferState}");

                            bufferController.EnableEvents(StreamBufferEvents.StreamBufferEvent.None);
                            bufferController.ResetBuffers();
                            bufferController.EnableEvents(StreamBufferEvents.StreamBufferEvent.All);

                            await bufferController.PushPackets(video);

                            await Task.Delay(TimeSpan.FromSeconds(1.5));

                            Assert.IsTrue(eventOnCount == 1, $"Expected: eventOnCount==1 Result: eventOnCount=={eventOnCount}");
                            Assert.IsTrue(eventOffCount == 0, $"Expected: eventOffCount==0 Result: eventOffCount=={eventOffCount}");
                            Assert.IsTrue(bufferState, $"Expected: bufferState==true Result bufferState=={bufferState}");


                            await bufferController.PushPackets(audio);

                            SpinWait.SpinUntil(() => eventOnCount > 0 && eventOffCount > 0, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(eventOnCount == 1, $"Expected: eventOnCount==1 Result: eventOnCount=={eventOnCount}");
                            Assert.IsTrue(eventOffCount == 1, $"Expected: eventOffCount==1 Result: eventOffCount=={eventOffCount}");
                            Assert.IsTrue(!bufferState, $"Expected: bufferState==false Result bufferState=={bufferState}");


                            await Task.WhenAll(
                                audio.DisposePackets(),
                                video.DisposePackets()
                            );
                        }
                    }
                });
        }

        [Test]
        public void DataOffOnEventFromSingleSource()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];

                        var eventCount = 0;


                        using (bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                dataArgsHolder[(int)a.StreamType] = a;

                            }, SynchronizationContext.Current))
                        {

                            var audioPackets = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                            await bufferController.PushPackets(audioPackets);


                            SpinWait.SpinUntil(() => eventCount >= 2, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(eventCount >= 2, $"Expected: eventCount >=2 Result: eventCount={eventCount}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].Duration <= TimeSpan.Zero, $"Expected: Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].Duration}");

                            await bufferController.PullPackets(audioPackets);
                            SpinWait.SpinUntil(() => eventCount >= 3, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(eventCount >= 3, $"Expected: eventCount >=3 Result: eventCount={eventCount}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].Duration > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].Duration}");

                            await audioPackets.DisposePackets();
                        }

                    }
                });
        }

        [Test]
        public void DataOffOnEventFromMultipleSources()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];


                        var eventCount = 0;


                        using (bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                dataArgsHolder[(int)a.StreamType] = a;

                            }, SynchronizationContext.Current))
                        {

                            var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                            var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100);

                            await Task.WhenAll(
                                bufferController.PushPackets(audio),
                                bufferController.PushPackets(video));

                            SpinWait.SpinUntil(() => eventCount >= 2, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(eventCount >= 2, $"Expected: eventCount >=2 Result: eventCount={eventCount}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].Duration <= TimeSpan.Zero, $"Expected: audio Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].Duration}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].Duration <= TimeSpan.Zero, $"Expected: video Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].Duration}");

                            await Task.WhenAll(
                                bufferController.PullPackets(audio),
                                bufferController.PullPackets(video));

                            SpinWait.SpinUntil(() => eventCount >= 4, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(eventCount >= 4, $"Expected: eventCount >=4 Result: eventCount={eventCount}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].Duration > TimeSpan.Zero, $"Expected: audio Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].Duration}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].Duration > TimeSpan.Zero, $"Expected: video Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].Duration}");

                            await Task.WhenAll(
                                audio.DisposePackets(),
                                video.DisposePackets()
                            );
                        }
                    }
                });
        }

        [Test]
        public void NoEventsWhenEventsDisabled()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];

                        var eventCount = 0;


                        using (bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                dataArgsHolder[(int)a.StreamType] = a;

                            }, SynchronizationContext.Current))
                        {

                            var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                            var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100);

                            await Task.WhenAll(
                                bufferController.PushPackets(audio),
                                bufferController.PushPackets(video));

                            SpinWait.SpinUntil(() => eventCount >= 2, TimeSpan.FromSeconds(2));

                            await Task.WhenAll(
                                bufferController.PullPackets(audio),
                                bufferController.PullPackets(video));

                            SpinWait.SpinUntil(() => eventCount >= 4, TimeSpan.FromSeconds(2));

                            bufferController.EnableEvents(StreamBufferEvents.StreamBufferEvent.None);
                            var currEventCount = eventCount;

                            await Task.WhenAll(
                                bufferController.PushPackets(audio),
                                bufferController.PushPackets(video));

                            await Task.Delay(1500);

                            await Task.WhenAll(
                                bufferController.PullPackets(audio),
                                bufferController.PullPackets(video));

                            await Task.Delay(1500);
                            Assert.IsTrue(eventCount == currEventCount, $"Expected: eventCount==currEventCount Result {eventCount}!={currEventCount}");

                            await Task.WhenAll(
                                audio.DisposePackets(),
                                video.DisposePackets()
                            );
                        }
                    }
                });
        }

        [Test]
        public void BufferFullBufferActualReported()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new StreamBufferController(Observable.Return<PlayerState>(PlayerState.Playing)))
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];

                        var eventCount = 0;


                        using (bufferController.DataNeededStateChanged()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                dataArgsHolder[(int)a.StreamType] = a;

                            }, SynchronizationContext.Current))
                        {

                            var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(7), 100);
                            var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(7), 100);

                            await Task.WhenAll(
                                bufferController.PushPackets(audio),
                                bufferController.PushPackets(video));

                            SpinWait.SpinUntil(() => eventCount >= 2, TimeSpan.FromSeconds(2));

                            var audioLevel = dataArgsHolder[(int)StreamType.Audio].Duration;
                            var videoLevel = dataArgsHolder[(int)StreamType.Video].Duration;

                            bufferController.ReportFullBuffer();
                            bufferController.PublishBufferState();

                            SpinWait.SpinUntil(() => eventCount >= 4, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].Duration == TimeSpan.Zero, $"Expected: audio Duration == 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].Duration}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].Duration == TimeSpan.Zero, $"Expected: video Duration == 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].Duration}");

                            bufferController.ReportActualBuffer();
                            bufferController.PublishBufferState();

                            SpinWait.SpinUntil(() => eventCount >= 6, TimeSpan.FromSeconds(2));

                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].Duration == audioLevel);
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].Duration == videoLevel);

                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].Duration == audioLevel, $"Expected: audio Duration == {audioLevel} Result: Duration=={dataArgsHolder[(int)StreamType.Audio].Duration}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].Duration == videoLevel, $"Expected: video Duration == {videoLevel} Result: Duration=={dataArgsHolder[(int)StreamType.Video].Duration}");

                            await Task.WhenAll(
                                audio.DisposePackets(),
                                video.DisposePackets()
                            );
                        }
                    }
                });
        }

    }
}
