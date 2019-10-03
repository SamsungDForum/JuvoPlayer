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
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Player.EsPlayer;
using static JuvoPlayer.Tests.IntegrationTests.TsEsBufferHelpers;
using static Configuration.DataBufferConfig;
using NUnit.Framework;

namespace JuvoPlayer.Tests.IntegrationTests
{
    [TestFixture]
    public class TSEsBuffer
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");

        [Test]
        public void BasicEventsAreGenerated()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

                // Increase buffer event frequency
                DataStatePublishInterval = TimeSpan.FromSeconds(0.25);
                var maxTimeout = DataStatePublishInterval + DataStatePublishInterval + DataStatePublishInterval;

                using (var buffer = new EsBuffer())
                {
                    buffer.Initialize(StreamType.Audio);
                    buffer.Initialize(StreamType.Video);

                    using (buffer.DataRequestObservable().Subscribe(dr => _logger.Info($"Request: {dr}")))
                    using (buffer.BufferingRequestObservable().Subscribe(b => _logger.Info($"Buffering: {b}")))
                    {
                        // No events expected
                        using (var cts = new CancellationTokenSource())
                        {
                            var dataRequestTask = buffer.DataRequestObservable().FirstAsync().ToTask(cts.Token);
                            var bufferingRequestTask = buffer.BufferingRequestObservable().FirstAsync().ToTask(cts.Token);
                            await Task.Delay(maxTimeout);
                            Assert.IsFalse(dataRequestTask.IsCompleted);
                            Assert.IsFalse(bufferingRequestTask.IsCompleted);
                            cts.Cancel();
                        }

                        // data only event expected
                        using (var cts = new CancellationTokenSource())
                        {
                            var dataRequestTask = buffer.DataRequestObservable().FirstAsync().ToTask(cts.Token);
                            var bufferingRequestTask = buffer.BufferingRequestObservable().FirstAsync().ToTask(cts.Token);
                            buffer.SetAllowedEvents(EsBuffer.DataEvent.DataRequest);
                            await Task.WhenAny(dataRequestTask, bufferingRequestTask, Task.Delay(maxTimeout));

                            Assert.IsTrue(dataRequestTask.IsCompleted);
                            Assert.IsTrue(!bufferingRequestTask.IsCompleted);
                            cts.Cancel();
                        }

                        // data & buffering event expected.
                        using (var cts = new CancellationTokenSource())
                        using (var packet = new Packet { Storage = new DummyStorage() })
                        {
                            var dataRequestTask = buffer.DataRequestObservable().FirstAsync().ToTask(cts.Token);
                            var bufferingRequestTask = buffer.BufferingRequestObservable().FirstAsync().ToTask(cts.Token);
                            buffer.SetAllowedEvents(EsBuffer.DataEvent.All);

                            // buffer events are generated only on buffer that has seen packets.
                            packet.Dts = TimeSpan.FromSeconds(0);
                            buffer.DataIn(packet);
                            buffer.DataOut(packet);
                            packet.Dts = TimeSpan.FromSeconds(0.05);
                            buffer.DataIn(packet);
                            buffer.DataOut(packet);

                            await Task.WhenAny(Task.WhenAll(dataRequestTask, bufferingRequestTask), Task.Delay(maxTimeout));

                            Assert.IsTrue(dataRequestTask.IsCompleted);
                            Assert.IsTrue(bufferingRequestTask.IsCompleted);
                            cts.Cancel();
                        }
                    }
                }
            });
        }

        [Test]
        public void BasicReportedValuesAreCorrect()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                // Increase buffer event frequency
                DataStatePublishInterval = TimeSpan.FromSeconds(0.25);
                var maxTimeout = DataStatePublishInterval + DataStatePublishInterval + DataStatePublishInterval;

                using (var buffer = new EsBuffer())
                using (var audioZeroPacket = new Packet { Dts = TimeSpan.Zero, StreamType = StreamType.Audio, Storage = new DummyStorage() })
                using (var videoZeroPacket = new Packet { Dts = TimeSpan.Zero, StreamType = StreamType.Video, Storage = new DummyStorage() })
                {
                    buffer.Initialize(StreamType.Audio);
                    buffer.Initialize(StreamType.Video);
                    buffer.SetAllowedEvents(EsBuffer.DataEvent.All);

                    using (buffer.DataRequestObservable().Subscribe(dr => _logger.Info($"Request: {dr}")))
                    using (buffer.BufferingRequestObservable().Subscribe(b => _logger.Info($"Buffering: {b}")))
                    {
                        // Zero level reported
                        using (var cts = new CancellationTokenSource())
                        {
                            var dataRequestTask = buffer.DataRequestObservable()
                                .Buffer(2, 1)
                                .FirstAsync(requestList =>
                                    requestList.All(request => request.RequestPeriod == request.MaxBufferDuration))
                                .ToTask(cts.Token);

                            var bufferingRequestTask = buffer.BufferingRequestObservable()
                                .FirstAsync()
                                .ToTask(cts.Token);

                            cts.CancelAfter(maxTimeout);
                            await dataRequestTask;

                            // buffering request should not produce data
                            Assert.IsFalse(bufferingRequestTask.IsCompleted);
                            Assert.IsTrue(dataRequestTask.IsCompleted && !dataRequestTask.IsCanceled);
                        }

                        var packetCount = (int)(TimeSpan.FromSeconds(1).TotalMilliseconds /
                                                StreamClockDiscontinuityThreshold.TotalMilliseconds);
                        packetCount += 2;

                        var audioPackets = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(1), packetCount);
                        var videoPackets = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(1), packetCount);

                        // Non zero level
                        using (var cts = new CancellationTokenSource())
                        {

                            buffer.DataIn(audioZeroPacket);
                            buffer.DataIn(videoZeroPacket);
                            buffer.DataIn(audioPackets);
                            buffer.DataIn(videoPackets);

                            var targetRequestPeriod =
                                TimeBufferDepthDefault - audioPackets[audioPackets.Count - 1].Dts;

                            var dataRequestTask = buffer.DataRequestObservable()
                                .Buffer(2, 1)
                                .FirstAsync(requestList =>
                                    requestList.All(request => request.RequestPeriod == targetRequestPeriod))
                                .ToTask(cts.Token);

                            var bufferingRequestTask = buffer.BufferingRequestObservable()
                                .FirstAsync(bufferingNeeded => bufferingNeeded == false)
                                .ToTask(cts.Token);

                            cts.CancelAfter(maxTimeout);
                            await Task.WhenAll(dataRequestTask, bufferingRequestTask);

                            Assert.IsTrue(bufferingRequestTask.IsCompleted && !dataRequestTask.IsCanceled);
                            Assert.IsTrue(dataRequestTask.IsCompleted && !dataRequestTask.IsCanceled);
                        }

                        // Zero level
                        using (var cts = new CancellationTokenSource())
                        {
                            buffer.DataOut(audioPackets);
                            buffer.DataOut(videoPackets);

                            var dataRequestTask = buffer.DataRequestObservable()
                                .Buffer(2, 1)
                                .FirstAsync(requestList =>
                                    requestList.All(request => request.RequestPeriod == request.MaxBufferDuration))
                                .ToTask(cts.Token);

                            var bufferingRequestTask = buffer.BufferingRequestObservable()
                                .FirstAsync(bufferingNeeded => bufferingNeeded)
                                .ToTask(cts.Token);

                            cts.CancelAfter(maxTimeout);
                            cts.CancelAfter(maxTimeout);
                            await Task.WhenAll(dataRequestTask, bufferingRequestTask);

                            Assert.IsTrue(bufferingRequestTask.IsCompleted && !dataRequestTask.IsCanceled);
                            Assert.IsTrue(dataRequestTask.IsCompleted && !dataRequestTask.IsCanceled);
                        }

                        audioPackets.DisposePackets();
                        videoPackets.DisposePackets();
                    }
                }
            });
        }

        [Test]
        public void ClockDiscontinuitiesAreIgnored()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                // Increase buffer event frequency
                DataStatePublishInterval = TimeSpan.FromSeconds(0.25);
                var maxTimeout = DataStatePublishInterval + DataStatePublishInterval + DataStatePublishInterval;

                var packetCount = (int)(TimeSpan.FromSeconds(1).TotalMilliseconds /
                                        StreamClockDiscontinuityThreshold.TotalMilliseconds);
                packetCount += 2;

                var audioPackets = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(1), packetCount)
                    .Prepend(new Packet { Dts = TimeSpan.Zero, StreamType = StreamType.Audio, Storage = new DummyStorage() })
                    .Concat(BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(1), packetCount, TimeSpan.FromSeconds(1) + StreamClockDiscontinuityThreshold))
                    .ToList();

                var videoPackets = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(1), packetCount)
                    .Prepend(new Packet { Dts = TimeSpan.Zero, StreamType = StreamType.Video, Storage = new DummyStorage() })
                    .Concat(BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(1), packetCount, TimeSpan.FromSeconds(1) + StreamClockDiscontinuityThreshold))
                    .ToList();

                using (var buffer = new EsBuffer())
                using (var cts = new CancellationTokenSource())
                {
                    buffer.Initialize(StreamType.Audio);
                    buffer.Initialize(StreamType.Video);
                    buffer.SetAllowedEvents(EsBuffer.DataEvent.All);

                    using (buffer.DataRequestObservable().Subscribe(dr => _logger.Info($"Request: {dr}")))
                    using (buffer.BufferingRequestObservable().Subscribe(b => _logger.Info($"Buffering: {b}")))
                    {

                        buffer.DataIn(audioPackets);
                        buffer.DataIn(videoPackets);

                        var targetRequestPeriod =
                            TimeBufferDepthDefault - audioPackets[audioPackets.Count - 1].Dts;

                        var dataRequestTask = buffer.DataRequestObservable()
                            .Buffer(2, 1)
                            .FirstAsync(requestList =>
                                requestList.All(request => request.RequestPeriod == targetRequestPeriod))
                            .ToTask(cts.Token);

                        cts.CancelAfter(maxTimeout);
                        await dataRequestTask;

                        Assert.IsTrue(dataRequestTask.IsCompleted && !dataRequestTask.IsCanceled);
                    }
                }

                audioPackets.DisposePackets();
                videoPackets.DisposePackets();
            });
        }

        [Test]
        public void DataRequestOutputThresholdIsRespected()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                // Increase buffer event frequency
                DataStatePublishInterval = TimeSpan.FromSeconds(0.25);
                var maxTimeout = DataStatePublishInterval + DataStatePublishInterval + DataStatePublishInterval;

                var streamDuration = DataRequestOutputThreshold + TimeSpan.FromSeconds(1);
                var packetCount = (int)(streamDuration.TotalMilliseconds /
                                        StreamClockDiscontinuityThreshold.TotalMilliseconds);
                packetCount += 2;

                var audioPackets = BuildPacketList(StreamType.Audio, TimeSpan.FromSeconds(1), packetCount);
                var videoPackets = BuildPacketList(StreamType.Video, TimeSpan.FromSeconds(1), packetCount);

                using (var buffer = new EsBuffer())
                using (var cts = new CancellationTokenSource())
                {
                    buffer.Initialize(StreamType.Audio);
                    buffer.Initialize(StreamType.Video);
                    buffer.SetAllowedEvents(EsBuffer.DataEvent.All);

                    using (buffer.DataRequestObservable().Subscribe(dr => _logger.Info($"Request: {dr}")))
                    using (buffer.BufferingRequestObservable().Subscribe(b => _logger.Info($"Buffering: {b}")))
                    {
                        var targetRequestPerdiod = TimeBufferDepthDefault - DataRequestOutputThreshold;
                        var dataRequestTask = buffer.DataRequestObservable()
                            .Buffer(2, 1)
                            .FirstAsync(requestList =>
                                requestList.All(request => request.RequestPeriod < targetRequestPerdiod))
                            .ToTask(cts.Token);

                        buffer.DataIn(audioPackets);
                        buffer.DataIn(videoPackets);

                        await Task.Delay(maxTimeout);

                        Assert.IsFalse(dataRequestTask.IsCompleted);
                        cts.Cancel();
                    }
                }

                audioPackets.DisposePackets();
                videoPackets.DisposePackets();
            });
        }

        [Test]
        public void DataInjectionWorks()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                // Increase buffer event frequency
                DataStatePublishInterval = TimeSpan.FromSeconds(0.25);
                var maxTimeout = DataStatePublishInterval + DataStatePublishInterval + DataStatePublishInterval;

                // Keep stream duration below publish threshold to be able to early exit data
                // collection before checking injection.
                var streamDuration = DataRequestOutputThreshold - TimeSpan.FromSeconds(0.5);
                var targetRequestPerdiod = TimeBufferDepthDefault - streamDuration;
                var packetCount = (int)(streamDuration.TotalMilliseconds /
                                         StreamClockDiscontinuityThreshold.TotalMilliseconds);
                packetCount += 2;

                var audioPackets = BuildPacketList(StreamType.Audio, streamDuration, packetCount);
                var videoPackets = BuildPacketList(StreamType.Video, streamDuration, packetCount);

                using (var buffer = new EsBuffer())
                {
                    buffer.Initialize(StreamType.Audio);
                    buffer.Initialize(StreamType.Video);
                    buffer.SetAllowedEvents(EsBuffer.DataEvent.All);

                    using (buffer.DataRequestObservable().Subscribe(dr => _logger.Info($"Request: {dr}")))
                    using (buffer.BufferingRequestObservable().Subscribe(b => _logger.Info($"Buffering: {b}")))
                    {
                        buffer.DataIn(audioPackets);
                        buffer.DataIn(videoPackets);

                        using (var cts = new CancellationTokenSource())
                        {
                            var dataRequestTask = buffer.DataRequestObservable()
                                .Buffer(2, 1)
                                .FirstAsync(requestList =>
                                    requestList.All(request => request.RequestPeriod == targetRequestPerdiod))
                                .ToTask(cts.Token);

                            cts.CancelAfter(maxTimeout);
                            await dataRequestTask;

                            Assert.IsTrue(dataRequestTask.IsCompleted);
                        }

                        // Injection is a "one shot" event. If buffer output threshold is below limit,
                        // next update will procude correct data. It is intended to use this API when
                        // buffering events are disabled.
                        buffer.SetAllowedEvents(EsBuffer.DataEvent.None);

                        using (var cts = new CancellationTokenSource())
                        {
                            var dataRequestTask = buffer.DataRequestObservable()
                                .Buffer(2, 1)
                                .FirstAsync(requestList =>
                                    requestList.All(request => request.RequestPeriod == TimeSpan.Zero))
                                .ToTask(cts.Token);

                            var bufferingRequestTask = buffer.BufferingRequestObservable()
                                .FirstAsync(bufferingNeeded => bufferingNeeded)
                                .ToTask(cts.Token);

                            buffer.RequestBuffering(true);
                            buffer.SendBufferFullDataRequest(StreamType.Audio);
                            buffer.SendBufferFullDataRequest(StreamType.Video);

                            cts.CancelAfter(maxTimeout);
                            await Task.WhenAll(dataRequestTask, bufferingRequestTask);
                            Assert.IsTrue(dataRequestTask.IsCompleted && !dataRequestTask.IsCanceled);
                            Assert.IsTrue(bufferingRequestTask.IsCompleted && !bufferingRequestTask.IsCanceled);

                        }
                    }
                }

                audioPackets.DisposePackets();
                videoPackets.DisposePackets();
            });
        }

        [Test]
        public void BufferResets()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                // Increase buffer event frequency
                DataStatePublishInterval = TimeSpan.FromSeconds(0.25);
                var maxTimeout = DataStatePublishInterval + DataStatePublishInterval + DataStatePublishInterval;

                // Keep stream duration below publish threshold to be able to early exit data
                // collection before checking injection.
                var streamDuration = DataRequestOutputThreshold - TimeSpan.FromSeconds(0.5);
                var targetRequestPerdiod = TimeBufferDepthDefault - streamDuration;
                var packetCount = (int)(streamDuration.TotalMilliseconds /
                                         StreamClockDiscontinuityThreshold.TotalMilliseconds);
                packetCount += 2;

                var audioPackets = BuildPacketList(StreamType.Audio, streamDuration, packetCount);
                var videoPackets = BuildPacketList(StreamType.Video, streamDuration, packetCount);

                using (var buffer = new EsBuffer())
                {
                    buffer.Initialize(StreamType.Audio);
                    buffer.Initialize(StreamType.Video);
                    buffer.SetAllowedEvents(EsBuffer.DataEvent.All);

                    using (buffer.DataRequestObservable().Subscribe(dr => _logger.Info($"Request: {dr}")))
                    using (buffer.BufferingRequestObservable().Subscribe(b => _logger.Info($"Buffering: {b}")))
                    {
                        buffer.DataIn(audioPackets);
                        buffer.DataIn(videoPackets);

                        using (var cts = new CancellationTokenSource())
                        {
                            var dataRequestTask = buffer.DataRequestObservable()
                                .Buffer(2, 1)
                                .FirstAsync(requestList =>
                                    requestList.All(request => request.RequestPeriod == targetRequestPerdiod))
                                .ToTask(cts.Token);

                            cts.CancelAfter(maxTimeout);
                            await dataRequestTask;

                            Assert.IsTrue(dataRequestTask.IsCompleted && !dataRequestTask.IsCanceled);
                        }

                        using (var cts = new CancellationTokenSource())
                        {
                            var dataRequestTask = buffer.DataRequestObservable()
                                .Buffer(2, 1)
                                .FirstAsync(requestList =>
                                    requestList.All(request => request.RequestPeriod == TimeBufferDepthDefault))
                                .ToTask(cts.Token);

                            var bufferingRequestTask = buffer.BufferingRequestObservable()
                                .FirstAsync(bufferingNeeded => bufferingNeeded == false)
                                .ToTask(cts.Token);

                            var resetTask = buffer.Reset();
                            cts.CancelAfter(maxTimeout);
                            await Task.WhenAll(resetTask, dataRequestTask, bufferingRequestTask);

                            Assert.IsTrue(dataRequestTask.IsCompleted && !dataRequestTask.IsCanceled);
                            Assert.IsTrue(bufferingRequestTask.IsCompleted && !bufferingRequestTask.IsCanceled);

                        }
                    }
                }
            });
        }

        [Test]
        public void StreamStateReportIsValid()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                // Increase buffer event frequency
                DataStatePublishInterval = TimeSpan.FromSeconds(0.25);
                var maxTimeout = DataStatePublishInterval + DataStatePublishInterval + DataStatePublishInterval;

                // Keep stream duration below publish threshold to be able to early exit data
                // collection before checking injection.
                var streamDuration = DataRequestOutputThreshold - TimeSpan.FromSeconds(0.5);
                var targetRequestPerdiod = TimeBufferDepthDefault - streamDuration;
                var packetCount = (int)(streamDuration.TotalMilliseconds /
                                         StreamClockDiscontinuityThreshold.TotalMilliseconds);
                packetCount += 2;

                var audioPackets = BuildPacketList(StreamType.Audio, streamDuration, packetCount);
                var videoPackets = BuildPacketList(StreamType.Video, streamDuration, packetCount);

                using (var buffer = new EsBuffer())
                {
                    buffer.Initialize(StreamType.Audio);
                    buffer.Initialize(StreamType.Video);
                    buffer.SetAllowedEvents(EsBuffer.DataEvent.All);

                    using (buffer.DataRequestObservable().Subscribe(dr => _logger.Info($"Request: {dr}")))
                    using (buffer.BufferingRequestObservable().Subscribe(b => _logger.Info($"Buffering: {b}")))
                    {
                        buffer.DataIn(audioPackets);
                        buffer.DataIn(videoPackets);

                        using (var cts = new CancellationTokenSource())
                        {
                            // StreamStateReport are published on SynchronizationContext.Current.
                            // Observe on same conext to be in sync with internal publication.
                            var dataRequestTask = buffer.DataRequestObservable()
                                .ObserveOn(SynchronizationContext.Current)
                                .Buffer(2, 1)
                                .FirstAsync(requestList =>
                                    requestList.All(request => request.RequestPeriod == targetRequestPerdiod))
                                .ToTask(cts.Token);

                            cts.CancelAfter(maxTimeout);
                            var requestResult = await dataRequestTask;

                            Assert.IsTrue(dataRequestTask.IsCompleted && !dataRequestTask.IsCanceled);

                            // Yield allowing sync context to complete any pending state report publications
                            await Task.Yield();

                            foreach (var request in requestResult)
                            {
                                var streamReport = buffer.GetStreamStateReport(request.StreamType);
                                Assert.AreEqual(request.RequestPeriod, streamReport.Duration - streamReport.BufferedPeriod);
                            }
                        }
                    }
                }
            });
        }

        [Test]
        public void BufferConfigurationIsChangable()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                // Increase buffer event frequency
                DataStatePublishInterval = TimeSpan.FromSeconds(0.25);
                var maxTimeout = DataStatePublishInterval + DataStatePublishInterval + DataStatePublishInterval;

                var newBufferDuration = TimeSpan.FromSeconds(5);
                var audioConfig = new MetaDataStreamConfig
                {
                    BufferDuration = newBufferDuration,
                    Stream = StreamType.Audio
                };

                var videoConfig = new MetaDataStreamConfig
                {
                    BufferDuration = newBufferDuration,
                    Stream = StreamType.Video
                };

                using (var buffer = new EsBuffer())
                {
                    buffer.Initialize(StreamType.Audio);
                    buffer.Initialize(StreamType.Video);
                    buffer.SetAllowedEvents(EsBuffer.DataEvent.All);

                    using (buffer.DataRequestObservable().Subscribe(dr => _logger.Info($"Request: {dr}")))
                    using (buffer.BufferingRequestObservable().Subscribe(b => _logger.Info($"Buffering: {b}")))
                    {
                        using (var cts = new CancellationTokenSource())
                        {
                            var dataRequestTask = buffer.DataRequestObservable()
                                .Buffer(2, 1)
                                .FirstAsync(requestList =>
                                    requestList.All(request => request.MaxBufferDuration == newBufferDuration))
                                .ToTask(cts.Token);

                            buffer.UpdateBufferConfiguration(audioConfig);
                            buffer.UpdateBufferConfiguration(videoConfig);

                            cts.CancelAfter(maxTimeout);
                            await dataRequestTask;

                            Assert.IsTrue(dataRequestTask.IsCompleted && !dataRequestTask.IsCanceled);
                        }
                    }
                }
            });
        }
    }
}
