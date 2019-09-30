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
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Player.EsPlayer;
using static JuvoPlayer.Tests.IntegrationTests.TSEsBufferHelpers;
using NUnit.Framework;

namespace JuvoPlayer.Tests.IntegrationTests
{
    [TestFixture]
    public class TSEsBuffer
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");

        private async Task Result(Func<bool> fn, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                var token = cts.Token;
                cts.CancelAfter(timeout);

                try
                {
                    while (!fn())
                    {
                        await Task.Delay(250, token);
                    }
                }
                catch (TaskCanceledException)
                {
                    if (!token.IsCancellationRequested)
                        throw;
                }

            }
        }
        [Test]
        [Ignore("To be reworked https://cam.sprc.samsung.pl/browse/PRJLTVA-399")]
        public void DataEventsAfterSubscription()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new EsBuffer())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];
                        var eventCount = 0;

                        using (bufferController.DataRequestObservable().Subscribe(
                            a =>
                            {
                                dataArgsHolder[(int)a.StreamType] = a;
                                eventCount++;
                            }, SynchronizationContext.Current))
                        {
                            await Result(() => eventCount == 2, Timeout);

                            Assert.IsFalse(dataArgsHolder[(int)StreamType.Audio].CompareDataArgs(default(DataRequest)), "dataArgsHolder[(int)StreamType.Audio] != null");
                            Assert.IsFalse(dataArgsHolder[(int)StreamType.Video].CompareDataArgs(default(DataRequest)), "dataArgsHolder[(int)StreamType.Video] != null");
                        }
                    }
                });
        }


        [Test]
        [Ignore("To be reworked https://cam.sprc.samsung.pl/browse/PRJLTVA-399")]
        public void NoBufferEventAfterSubscription()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new EsBuffer())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);

                        var eventCount = 0;
                        var flag = true;
                        using (bufferController.BufferingRequestObservable()
                            .Subscribe(a =>
                            {
                                flag = a;
                                eventCount++;
                            }, SynchronizationContext.Current))
                        {

                            await Task.Delay(2000);

                            Assert.IsTrue(eventCount == 0 && flag,
                                $"Expected: eventCount == 0 Flag == true Result: eventCount == {eventCount} flag == {flag}");
                        }
                    }
                });
        }

        [Test]
        [Ignore("To be reworked https://cam.sprc.samsung.pl/browse/PRJLTVA-399")]
        public void DataEventAfterSingleConfigurationChange()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new EsBuffer())
                    {
                        bufferController.Initialize(StreamType.Audio);

                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];

                        using (bufferController.DataRequestObservable()
                            .Subscribe(a => { dataArgsHolder[(int)a.StreamType] = a; },
                                SynchronizationContext.Current))
                        {

                            var conf = new MetaDataStreamConfig
                            {
                                Bandwidth = 123456789,
                                BufferDuration = TimeSpan.FromSeconds(15),
                                Stream = StreamType.Audio
                            };
                            bufferController.UpdateBufferConfiguration(conf);

                            await Result(() => !dataArgsHolder[(int)StreamType.Audio].CompareDataArgs(default(DataRequest)), Timeout);

                            Assert.IsTrue(
                                dataArgsHolder[(int)StreamType.Audio].CompareMetaData(conf), $"Expected: Same Result: {conf} {dataArgsHolder[(int)StreamType.Audio]}");
                        }
                    }
                });
        }

        [Test]
        [Ignore("To be reworked https://cam.sprc.samsung.pl/browse/PRJLTVA-399")]
        public void DataEventsOnMultipleConfigurationChange()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var bufferController = new EsBuffer())
                    {
                        bufferController.Initialize(StreamType.Audio);
                        bufferController.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];

                        var configsToSet = 0;

                        using (bufferController.DataRequestObservable()
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

                            bufferController.UpdateBufferConfiguration(audio);
                            bufferController.UpdateBufferConfiguration(video);

                            await Result(() => !dataArgsHolder[(int)StreamType.Audio].CompareDataArgs(default(DataRequest)) &&
                                               !dataArgsHolder[(int)StreamType.Video].CompareDataArgs(default(DataRequest)), Timeout);

                            Assert.IsTrue(
                                dataArgsHolder[(int)StreamType.Audio].CompareMetaData(audio), $"Expected: Same Result: {audio} {dataArgsHolder[(int)StreamType.Audio]}");

                            Assert.IsTrue(
                                dataArgsHolder[(int)StreamType.Video].CompareMetaData(video), $"Expected: Same Result: {video} {dataArgsHolder[(int)StreamType.Audio]}");
                        }
                    }
                });
        }

        [Test]
        [Ignore("To be reworked https://cam.sprc.samsung.pl/browse/PRJLTVA-399")]
        public void BufferOnOffEventFromSingleSource()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var dataBuffer = new EsBuffer())
                    {
                        dataBuffer.Initialize(StreamType.Audio);

                        var bufferState = true;

                        using (dataBuffer.BufferingRequestObservable()
                            .Subscribe(a =>
                            {
                                bufferState = a;
                            }, SynchronizationContext.Current))
                        {

                            var audioPackets1 = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);

                            await Task.Delay(2000);
                            Assert.IsTrue(bufferState, $"Expected: bufferState==true Result bufferState=={bufferState}");

                            await dataBuffer.PushPackets(audioPackets1);
                            await Result(() => !bufferState, Timeout);
                            Assert.IsTrue(!bufferState, $"Expected: bufferState==false Result bufferState=={bufferState}");

                            await dataBuffer.PullPackets(audioPackets1);
                            await Result(() => bufferState, Timeout);
                            Assert.IsTrue(bufferState, $"Expected: bufferState==true Result bufferState=={bufferState}");

                            await audioPackets1.DisposePackets();
                        }
                    }
                });
        }

        [Test]
        [Ignore("To be reworked https://cam.sprc.samsung.pl/browse/PRJLTVA-399")]
        public void BufferOnOffEventsFromMultipleSources()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var dataBuffer = new EsBuffer())
                    {
                        dataBuffer.Initialize(StreamType.Audio);
                        dataBuffer.Initialize(StreamType.Video);

                        var bufferState = true;

                        using (dataBuffer.BufferingRequestObservable()
                            .Subscribe(a =>
                            {
                                bufferState = a;

                            }, SynchronizationContext.Current))
                        {

                            var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                            var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100);

                            await Task.Delay(2000);
                            Assert.IsTrue(bufferState, $"Expected: bufferState==true Result bufferState=={bufferState}");

                            await dataBuffer.PushPackets(audio);
                            await Task.Delay(2000);
                            Assert.IsTrue(bufferState, $"Expected: bufferState==true Result bufferState=={bufferState}");

                            await dataBuffer.PushPackets(video);
                            await Result(() => !bufferState, Timeout);
                            Assert.IsTrue(!bufferState, $"Expected: bufferState==false Result bufferState=={bufferState}");

                            await Task.WhenAll(
                                dataBuffer.PullPackets(audio),
                                dataBuffer.PullPackets(video)
                            );

                            await Result(() => bufferState, Timeout);

                            Assert.IsTrue(bufferState, $"Expected: bufferState==true Result bufferState=={bufferState}");


                            await Task.WhenAll(
                                audio.DisposePackets(),
                                video.DisposePackets()
                            );
                        }
                    }
                });
        }

        [Test]
        [Ignore("To be reworked https://cam.sprc.samsung.pl/browse/PRJLTVA-399")]
        public void DataOffOnEventFromSingleSource()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var dataBuffer = new EsBuffer())
                    {
                        dataBuffer.Initialize(StreamType.Audio);

                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];

                        var eventCount = 0;


                        using (dataBuffer.DataRequestObservable()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                dataArgsHolder[(int)a.StreamType] = a;

                            }, SynchronizationContext.Current))
                        {

                            await Result(() => eventCount > 0, Timeout);
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");

                            var audioPackets = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                            await dataBuffer.PushPackets(audioPackets);

                            await Result(() => dataArgsHolder[(int)StreamType.Audio].RequestPeriod <= TimeSpan.Zero, Timeout);
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod <= TimeSpan.Zero, $"Expected: Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");

                            await dataBuffer.PullPackets(audioPackets);
                            await Result(() => dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero, Timeout);

                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");

                            await audioPackets.DisposePackets();
                        }
                    }
                });
        }

        [Test]
        [Ignore("To be reworked https://cam.sprc.samsung.pl/browse/PRJLTVA-399")]
        public void DataOffOnEventFromMultipleSources()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var dataBuffer = new EsBuffer())
                    {
                        dataBuffer.Initialize(StreamType.Audio);
                        dataBuffer.Initialize(StreamType.Video);
                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];

                        var eventCount = 0;

                        using (dataBuffer.DataRequestObservable()
                            .Subscribe(a =>
                            {
                                eventCount++;
                                dataArgsHolder[(int)a.StreamType] = a;

                            }, SynchronizationContext.Current))
                        {

                            var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                            var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100);

                            await Result(() => eventCount >= 2, Timeout);
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].RequestPeriod > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");

                            await Task.WhenAll(
                                dataBuffer.PushPackets(audio),
                                dataBuffer.PushPackets(video));

                            await Result(() => dataArgsHolder[(int)StreamType.Audio].RequestPeriod <= TimeSpan.Zero &&
                                                     dataArgsHolder[(int)StreamType.Video].RequestPeriod <= TimeSpan.Zero, Timeout);

                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod <= TimeSpan.Zero, $"Expected: audio Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].RequestPeriod <= TimeSpan.Zero, $"Expected: video Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");

                            await Task.WhenAll(
                                dataBuffer.PullPackets(audio),
                                dataBuffer.PullPackets(video));

                            await Result(() => dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero &&
                                                     dataArgsHolder[(int)StreamType.Video].RequestPeriod > TimeSpan.Zero, Timeout);

                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero, $"Expected: audio Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].RequestPeriod > TimeSpan.Zero, $"Expected: video Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");

                            await Task.WhenAll(
                                audio.DisposePackets(),
                                video.DisposePackets()
                            );
                        }
                    }
                });
        }

        [Test]
        [Ignore("To be reworked https://cam.sprc.samsung.pl/browse/PRJLTVA-399")]
        public void NoEventsWhenEventsDisabled()
        {
            Assert.DoesNotThrowAsync(
                async () =>
                {
                    using (var dataBuffer = new EsBuffer())
                    {
                        dataBuffer.Initialize(StreamType.Audio);
                        dataBuffer.Initialize(StreamType.Video);

                        var dataArgsHolder = new DataRequest[(int)StreamType.Count];
                        var dataEventCount = 0;
                        var bufferEventCount = 0;
                        var bufferState = true;

                        using (dataBuffer.DataRequestObservable()
                            .Subscribe(a =>
                            {
                                dataEventCount++;
                                dataArgsHolder[(int)a.StreamType] = a;

                            }, SynchronizationContext.Current))
                        using (dataBuffer.BufferingRequestObservable()
                           .Subscribe(b =>
                            {
                                bufferEventCount++;
                                bufferState = b;
                            }))
                        {

                            var audio = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100);
                            var video = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100);
                            var audio2 = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(15), 100, TimeSpan.FromSeconds(15.1));
                            var video2 = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(15), 100, TimeSpan.FromSeconds(15.1));

                            await Result(() => !dataArgsHolder[(int)StreamType.Audio].CompareDataArgs(default(DataRequest)) &&
                                               !dataArgsHolder[(int)StreamType.Video].CompareDataArgs(default(DataRequest)), Timeout);
                            Assert.IsTrue(bufferEventCount == 0, $"Expected: bufferEventCount == 0 Result: {bufferEventCount} > 0");
                            Assert.IsTrue(bufferState, $"Expected: bufferState = true Result: {bufferState}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].CompareDataArgs(default(DataRequest)), $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].CompareDataArgs(default(DataRequest)), $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");

                            await Task.WhenAll(
                                dataBuffer.PushPackets(audio),
                                dataBuffer.PushPackets(video));

                            await Result(() => dataArgsHolder[(int)StreamType.Audio].RequestPeriod <= TimeSpan.Zero &&
                                                     dataArgsHolder[(int)StreamType.Video].RequestPeriod <= TimeSpan.Zero &&
                                                     !bufferState, Timeout);
                            Assert.IsTrue(!bufferState, $"Expected: bufferState == false Result: {bufferState}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod <= TimeSpan.Zero, $"Expected: Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].RequestPeriod <= TimeSpan.Zero, $"Expected: Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");
                            Assert.IsTrue(bufferEventCount == 1, $"Expected: bufferEventCount == 1 Result: {bufferEventCount} != 1");

                            dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.None);

                            await Task.WhenAll(
                                dataBuffer.PullPackets(audio),
                                dataBuffer.PullPackets(video));

                            await Task.Delay(3000);
                            Assert.IsTrue(!bufferState, $"Expected: bufferState == false Result: {bufferState}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod <= TimeSpan.Zero, $"Expected: Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].RequestPeriod <= TimeSpan.Zero, $"Expected: Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");
                            Assert.IsTrue(bufferEventCount == 1, $"Expected: bufferEventCount == 1 Result: {bufferEventCount} != 1");

                            dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.DataRequest);
                            await Result(() => dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero &&
                                                     dataArgsHolder[(int)StreamType.Video].RequestPeriod > TimeSpan.Zero,
                                Timeout);
                            Assert.IsTrue(bufferState, $"Expected: bufferState == true Result: {bufferState}");
                            Assert.IsTrue(bufferEventCount == 2, $"Expected: bufferEventCount == 2 Result: {bufferEventCount} != 2");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].RequestPeriod > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");

                            dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.None);


                            await Task.WhenAll(
                                dataBuffer.PushPackets(audio2),
                                dataBuffer.PushPackets(video2));

                            await Task.Delay(3000);
                            Assert.IsTrue(bufferState, $"Expected: bufferState == true Result: {bufferState}");
                            Assert.IsTrue(bufferEventCount == 2, $"Expected: bufferEventCount == 2 Result: {bufferEventCount} != 2");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].RequestPeriod > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");


                            dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.All);
                            await Result(() => dataArgsHolder[(int)StreamType.Audio].RequestPeriod <= TimeSpan.Zero &&
                                                     dataArgsHolder[(int)StreamType.Video].RequestPeriod <= TimeSpan.Zero &&
                                                     !bufferState, Timeout);
                            Assert.IsTrue(!bufferState, $"Expected: bufferState == false Result: {bufferState}");
                            Assert.IsTrue(bufferEventCount == 3, $"Expected: bufferEventCount == 3 Result: {bufferEventCount} != 3");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod <= TimeSpan.Zero, $"Expected: Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].RequestPeriod <= TimeSpan.Zero, $"Expected: Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");

                            dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.None);

                            await Task.WhenAll(
                                dataBuffer.PullPackets(audio2),
                                dataBuffer.PullPackets(video2));

                            var cleaner = Task.WhenAll(
                                audio.DisposePackets(),
                                video.DisposePackets(),
                                audio2.DisposePackets(),
                                video2.DisposePackets());

                            await Task.Delay(3000);
                            Assert.IsTrue(!bufferState, $"Expected: bufferState == false Result: {bufferState}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod <= TimeSpan.Zero, $"Expected: Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].RequestPeriod <= TimeSpan.Zero, $"Expected: Duration <= 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");
                            Assert.IsTrue(bufferEventCount == 3, $"Expected: bufferEventCount == 3 Result: {bufferEventCount} != 3");


                            dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.All);
                            await Result(() => dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero &&
                                                     dataArgsHolder[(int)StreamType.Video].RequestPeriod > TimeSpan.Zero &&
                                                     bufferState, Timeout);
                            Assert.IsTrue(bufferState, $"Expected: bufferState == true Result: {bufferState}");
                            Assert.IsTrue(bufferEventCount == 4, $"Expected: bufferEventCount == 4 Result: {bufferEventCount} != 4");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Audio].RequestPeriod > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Audio].RequestPeriod}");
                            Assert.IsTrue(dataArgsHolder[(int)StreamType.Video].RequestPeriod > TimeSpan.Zero, $"Expected: Duration > 0 Result: Duration=={dataArgsHolder[(int)StreamType.Video].RequestPeriod}");

                            await cleaner;

                        }
                    }
                });
        }

    }
}
