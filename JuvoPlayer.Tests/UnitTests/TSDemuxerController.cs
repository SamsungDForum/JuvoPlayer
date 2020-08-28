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
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;
using Nito.AsyncEx;
using NSubstitute;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    public class TSDemuxerController
    {
        [TestCase(StartType.StartForEs)]
        [TestCase(StartType.StartForUrl)]
        [Category("Positive")]
        public void Start_ClipDurationFound_PublishesClipDuration(StartType startType)
        {
            AsyncContext.Run(async () =>
            {
                var expectedDuration = TimeSpan.FromSeconds(10);
                var demuxerStub = CreateDemuxerStub(new ClipConfiguration {Duration = expectedDuration}, startType);

                using (var controller = new DemuxerController(demuxerStub))
                {
                    var clipDurationTask = controller.ClipDurationFound().FirstAsync().ToTask();

                    StartController(controller, startType);

                    var duration = await clipDurationTask;
                    Assert.That(duration, Is.EqualTo(expectedDuration));
                }
            });
        }

        [TestCase(StartType.StartForEs)]
        [TestCase(StartType.StartForUrl)]
        [Category("Positive")]
        public void Start_StreamConfigFound_PublishesStreamConfig(StartType startType)
        {
            AsyncContext.Run(async () =>
            {
                var videoConfig = new VideoStreamConfig();
                var demuxerStub = CreateDemuxerStub(new ClipConfiguration
                    {StreamConfigs = new List<StreamConfig> {videoConfig}}, startType);

                using (var controller = new DemuxerController(demuxerStub))
                {
                    var streamConfigTask = controller.StreamConfigReady().FirstAsync().ToTask();

                    StartController(controller, startType);

                    var receivedConfig = await streamConfigTask;
                    Assert.That(receivedConfig, Is.EqualTo(videoConfig));
                }
            });
        }

        [TestCase(StartType.StartForEs)]
        [TestCase(StartType.StartForUrl)]
        [Category("Positive")]
        public void Start_DRMInitDataFound_PublishesDRMInitData(StartType startType)
        {
            AsyncContext.Run(async () =>
            {
                var initData = new DrmInitData
                {
                    StreamType = StreamType.Video,
                    InitData = new byte[1]
                };
                var demuxerStub = CreateDemuxerStub(new ClipConfiguration
                    {DrmInitDatas = new List<DrmInitData> {initData}}, startType);

                using (var controller = new DemuxerController(demuxerStub))
                {
                    var initDataTask = controller.DrmInitDataFound().FirstAsync().ToTask();

                    StartController(controller, startType);

                    var receivedInitData = await initDataTask;
                    Assert.That(receivedInitData, Is.EqualTo(initData));
                }
            });
        }

        [TestCase(StartType.StartForEs)]
        [TestCase(StartType.StartForUrl)]
        [Category("Positive")]
        public void Start_PacketDemuxed_PublishesPacket(StartType startType)
        {
            AsyncContext.Run(async () =>
            {
                var demuxerStub = Substitute.For<IDemuxer>();
                var packet = new Packet {StreamType = StreamType.Video, IsKeyFrame = true};
                demuxerStub.NextPacket().Returns(Task.FromResult(packet));
                demuxerStub.IsInitialized().Returns(true);
                demuxerStub.Completion.Returns(Task.Delay(500));

                using (var controller = new DemuxerController(demuxerStub))
                {
                    var packetReadyTask = controller.PacketReady()
                        .FirstAsync()
                        .ToTask();

                    StartController(controller, startType);

                    var receivedPacket = await packetReadyTask;
                    Assert.That(receivedPacket, Is.EqualTo(packet));
                }
            });
        }

        [TestCase(StartType.StartForEs)]
        [TestCase(StartType.StartForUrl)]
        [Category("Positive")]
        public void Start_PacketsDemuxed_PublishesPackets(StartType startType)
        {
            AsyncContext.Run(async () =>
            {
                var demuxerStub = Substitute.For<IDemuxer>();
                var packet = new Packet {StreamType = StreamType.Video, IsKeyFrame = true};
                demuxerStub.NextPacket().Returns(Task.FromResult(packet));
                demuxerStub.IsInitialized().Returns(true);
                demuxerStub.Completion.Returns(Task.Delay(500));

                using (var controller = new DemuxerController(demuxerStub))
                {
                    var packetReadyTask = controller.PacketReady()
                        .Take(2)
                        .Count()
                        .FirstAsync()
                        .ToTask();

                    StartController(controller, startType);

                    var receivedCount = await packetReadyTask;
                    Assert.That(receivedCount, Is.EqualTo(2));
                }
            });
        }

        [TestCase(StartType.StartForEs)]
        [TestCase(StartType.StartForUrl)]
        [Category("Negative")]
        public void Start_DemuxerInitFails_PublishesDemuxerError(StartType startType)
        {
            AsyncContext.Run(async () =>
            {
                var demuxerStub = CreateFailingDemuxerStub(startType);
                using (var controller = new DemuxerController(demuxerStub))
                {
                    var demuxerErrorTask = controller.DemuxerError().FirstAsync().ToTask();

                    StartController(controller, startType);

                    var error = await demuxerErrorTask;

                    Assert.That(error, Is.Not.Null);
                }
            });
        }

        [Test]
        [Category("Positive")]
        public void Reset_Called_ResetsDemuxer()
        {
            var demuxerMock = Substitute.For<IDemuxer>();
            using (var controller = new DemuxerController(demuxerMock))
            {
                controller.Reset();

                demuxerMock.Received().Reset();
            }
        }

        [Test]
        [Category("Positive")]
        public void SetDataSource_DataSourceCompletes_SignalsEos()
        {
            AsyncContext.Run(async () =>
            {
                var demuxerStub = Substitute.For<IDemuxer>();
                var dataSource = new Subject<byte[]>();
                using (var controller = new DemuxerController(demuxerStub))
                {
                    controller.SetDataSource(dataSource);
                    var packetReadyTask = controller.PacketReady()
                        .FirstAsync()
                        .ToTask();

                    dataSource.OnNext(null);

                    var isEmpty = await packetReadyTask;
                    Assert.That(isEmpty, Is.Null);
                }
            });
        }

        [Test]
        [Category("Positive")]
        public void Flush_Called_ResetsDemuxer()
        {
            AsyncContext.Run(async () =>
            {
                var demuxerMock = Substitute.For<IDemuxer>();
                using (var controller = new DemuxerController(demuxerMock))
                {
                    await controller.Flush();

                    demuxerMock.Received().Reset();
                }
            });
        }

        [Test]
        [Category("Positive")]
        public void Flush_CalledWhileChunkIsPending_ChunkIsDeliveredToDemuxer()
        {
            AsyncContext.Run(async () =>
            {
                var demuxerMock = Substitute.For<IDemuxer>();
                using (var controller = new DemuxerController(demuxerMock))
                {
                    var subject = new Subject<byte[]>();
                    controller.SetDataSource(subject.AsObservable());
                    subject.OnNext(new byte[0]);

                    demuxerMock.When(demuxer => demuxer.Complete())
                        .Do(_ => demuxerMock.Received().PushChunk(Arg.Any<byte[]>()));

                    await controller.Flush();
                }
            });
        }

        [TestCase(StartType.StartForEs)]
        [TestCase(StartType.StartForUrl)]
        [Category("Positive")]
        public void Reset_Called_NextPacketNotCalledAfter(StartType startType)
        {
            AsyncContext.Run(async () =>
            {
                var demuxerMock = Substitute.For<IDemuxer>();
                var tcs = new TaskCompletionSource<Packet>();
                demuxerMock.NextPacket().Returns(tcs.Task);
                demuxerMock.IsInitialized().Returns(true);

                using (var controller = new DemuxerController(demuxerMock))
                {
                    StartController(controller, startType);
                    await Task.Yield(); // Demuxer.InitFor completes, first Demuxer.NextPacket is called

                    controller.Reset();
                    tcs.SetResult(new Packet());

                    await Task.Yield(); // first Demuxer.NextPacket completes and another Demuxer.NextPacket is
                    // not called
                    await demuxerMock.Received(1).NextPacket();
                }
            });
        }

        [TestCase(StartType.StartForEs)]
        [TestCase(StartType.StartForUrl)]
        [Category("Positive")]
        public void Pause_Called_DoesntRetrieveNextPacket(StartType startType)
        {
            AsyncContext.Run(async () =>
            {
                var demuxerMock = Substitute.For<IDemuxer>();
                demuxerMock.IsInitialized().Returns(true);

                using (var controller = new DemuxerController(demuxerMock))
                {
                    StartController(controller, startType);

                    controller.Pause();

                    await Task.Yield();

                    await demuxerMock.DidNotReceive().NextPacket();
                }
            });
        }

        [TestCase(StartType.StartForEs)]
        [TestCase(StartType.StartForUrl)]
        [Category("Positive")]
        public void Resume_Called_CallsNextPacket(StartType startType)
        {
            AsyncContext.Run(async () =>
            {
                var demuxerMock = Substitute.For<IDemuxer>();
                demuxerMock.IsInitialized().Returns(true);

                using (var controller = new DemuxerController(demuxerMock))
                {
                    StartController(controller, startType);

                    controller.Pause();

                    await Task.Yield();

                    controller.Resume();
                    await demuxerMock.Received().NextPacket();
                }
            });
        }

        [Test]
        [Category("Positive")]
        public void Reset_CalledWhileDataSourcePublishedChunk_SkipsPublishedChunk()
        {
            AsyncContext.Run(async () =>
            {
                var demuxerMock = Substitute.For<IDemuxer>();

                using (var dataSource = new Subject<byte[]>())
                using (var controller = new DemuxerController(demuxerMock))
                {
                    controller.SetDataSource(dataSource);

                    dataSource.OnNext(new byte[1]);
                    controller.Reset();
                    await Task.Yield();

                    demuxerMock.DidNotReceive().PushChunk(Arg.Any<byte[]>());
                }
            });
        }

        [Test]
        [Category("Positive")]
        public void Reset_CalledWhileFlushIsInProgress_CancelsFlush()
        {
            AsyncContext.Run(async () =>
            {
                var demuxerStub = Substitute.For<IDemuxer>();
                demuxerStub.Completion.Returns(Task.Delay(-1));

                using (var controller = new DemuxerController(demuxerStub))
                {
                    var flushTask = controller.Flush();

                    controller.Reset();

                    try
                    {
                        await flushTask;
                        Assert.Fail();
                    }
                    catch (TaskCanceledException)
                    {
                        // ignored
                    }
                    catch (OperationCanceledException)
                    {
                        // ignored
                    }
                }
            });
        }

        [Test]
        [Category("Positive")]
        public void Seek_Called_DemuxesNextPacket()
        {
            AsyncContext.Run(async () =>
            {
                var demuxerStub = Substitute.For<IDemuxer>();
                demuxerStub.IsInitialized().Returns(true);
                demuxerStub.NextPacket().Returns(Task.FromResult(new Packet()));
                demuxerStub.Completion.Returns(Utils.TaskExtensions.Never());

                using (var controller = new DemuxerController(demuxerStub))
                {
                    var packetReady = controller.PacketReady().FirstAsync().ToTask();

                    await controller.Seek(TimeSpan.FromSeconds(5), CancellationToken.None);

                    var packet = await packetReady;
                    Assert.That(packet, Is.Not.Null);
                }
            });
        }

        [Test]
        [Category("Positive")]
        public void Seek_Called_ListensForEos()
        {
            AsyncContext.Run(async () =>
            {
                var demuxerStub = Substitute.For<IDemuxer>();
                demuxerStub.NextPacket().Returns(Utils.TaskExtensions.Never<Packet>());
                demuxerStub.Completion.Returns(Task.CompletedTask);

                using (var controller = new DemuxerController(demuxerStub))
                {
                    var packetReady = controller.PacketReady().FirstAsync().ToTask();

                    await controller.Seek(TimeSpan.FromSeconds(5), CancellationToken.None);

                    var packet = await packetReady;
                    Assert.That(packet, Is.Null);
                }
            });
        }

        [Test]
        [Category("Positive")]
        public void Seek_CalledWhenPaused_DoesntDemuxNextPacket()
        {
            AsyncContext.Run(async () =>
            {
                var demuxerMock = Substitute.For<IDemuxer>();
                demuxerMock.IsInitialized().Returns(true);

                using (var controller = new DemuxerController(demuxerMock))
                {
                    controller.Pause();
                    await controller.Seek(TimeSpan.FromSeconds(5), CancellationToken.None);
                    await demuxerMock.DidNotReceive().NextPacket();
                }
            });
        }

        private static IDemuxer CreateDemuxerStub(ClipConfiguration configuration, StartType startType)
        {
            var demuxerStub = Substitute.For<IDemuxer>();

            switch (startType)
            {
                case StartType.StartForEs:
                    demuxerStub.InitForEs().Returns(configuration);
                    break;
                case StartType.StartForUrl:
                    demuxerStub.InitForUrl(Arg.Any<string>())
                        .Returns(configuration);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(startType), startType, null);
            }

            return demuxerStub;
        }

        private static IDemuxer CreateFailingDemuxerStub(StartType startType)
        {
            var demuxerStub = Substitute.For<IDemuxer>();
            var initException = Task.FromException<ClipConfiguration>(new DemuxerException("Init failed"));

            switch (startType)
            {
                case StartType.StartForEs:
                    demuxerStub.InitForEs().Returns(initException);
                    break;
                case StartType.StartForUrl:
                    demuxerStub.InitForUrl(Arg.Any<string>())
                        .Returns(initException);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(startType), startType, null);
            }

            return demuxerStub;
        }

        public enum StartType
        {
            StartForEs,
            StartForUrl
        }

        private void StartController(IDemuxerController controller, StartType startType)
        {
            switch (startType)
            {
                case StartType.StartForEs:
                    controller.StartForEs();
                    break;
                case StartType.StartForUrl:
                    controller.StartForUrl("dummy_url");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(startType), startType, null);
            }
        }
    }
}