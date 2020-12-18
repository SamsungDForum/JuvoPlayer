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
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Demuxers.FFmpeg;
using JuvoPlayer.TizenTests.Utils;
using Nito.AsyncEx;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    public class TSDemuxerController
    {
        private static string BigBuckBunnyUrl =
            "http://106.120.45.49/bunny/bbb_sunflower_1080p_30fps_normal.mp4";

        private DashContent content;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var provider = new DashContentProvider();
            content = provider.GetGoogleCar();
            Assert.That(content.IsInitialized, Is.True);
        }

        [Test]
        public void StartForUrl_ContentWithAudioAndVideo_CallsStreamConfigReady()
        {
            AsyncContext.Run(async () =>
            {
                using (var controller = CreateDemuxerController())
                {
                    var audioFoundTask = controller.StreamConfigReady()
                        .Where(config => config.StreamType() == StreamType.Audio)
                        .FirstAsync()
                        .Timeout(TimeSpan.FromSeconds(2))
                        .ToTask();

                    var videoFoundTask = controller.StreamConfigReady()
                        .Where(config => config.StreamType() == StreamType.Video)
                        .FirstAsync()
                        .Timeout(TimeSpan.FromSeconds(2))
                        .ToTask();

                    controller.StartForUrl(BigBuckBunnyUrl);

                    await audioFoundTask;
                    await videoFoundTask;
                }
            });
        }

        [Test]
        public void StartForUrl_ContentWithDuration_CallsClipDurationChanged()
        {
            AsyncContext.Run(async () =>
            {
                using (var controller = CreateDemuxerController())
                {
                    var clipDurationTask = controller.ClipDurationFound().FirstAsync().ToTask();
                    controller.StartForUrl(BigBuckBunnyUrl);

                    var expectedDuration = new TimeSpan(0, 0, 10, 34, 533);
                    var receivedDuration = await clipDurationTask;
                    Assert.That(receivedDuration, Is.EqualTo(expectedDuration));
                }
            });
        }

        [Test]
        public void StartForUrl_ContentWithAudioAndVideo_CallsPacketReady()
        {
            AsyncContext.Run(async () =>
            {
                using (var controller = CreateDemuxerController())
                {
                    controller.StartForUrl(BigBuckBunnyUrl);
                    await controller.PacketReady().FirstAsync().Timeout(TimeSpan.FromSeconds(2));
                }
            });
        }

        [Test]
        public void Dispose_CalledTwice_ShouldntThrow()
        {
            var controller = CreateDemuxerController();

            controller.Dispose();

            Assert.DoesNotThrow(() => { controller.Dispose(); });
        }

        [Test]
        public void StartForEs_ChunksPushed_DemuxesStreamConfig()
        {
            AsyncContext.Run(async () =>
            {
                try
                {
                    var chunkReady = new Subject<byte[]>();
                    var controller = CreateDemuxerController(chunkReady.AsObservable());
                    using (controller)
                    {
                        controller.StartForEs();
                        var configReadyTask = controller.StreamConfigReady().FirstAsync().ToTask();

                        chunkReady.OnNext(content.InitSegment);
                        foreach (var segment in content.Segments)
                            chunkReady.OnNext(segment);

                        var config = await configReadyTask;
                        Assert.That(config, Is.Not.Null);
                    }
                }
                catch (Exception)
                {
                    Assert.Fail();
                }
            });
        }

        [Test]
        public void StartForEs_ChunksPushed_DemuxesPackets()
        {
            AsyncContext.Run(async () =>
            {
                var chunkReady = new Subject<byte[]>();
                var controller = CreateDemuxerController(chunkReady.AsObservable());
                using (controller)
                {
                    controller.StartForEs();

                    var packetReadyTask = controller.PacketReady().FirstAsync().ToTask();

                    chunkReady.OnNext(content.InitSegment);
                    foreach (var segment in content.Segments)
                        chunkReady.OnNext(segment);

                    var packet = await packetReadyTask;
                    Assert.That(packet, Is.Not.Null);
                    Assert.That(packet.StreamType, Is.EqualTo(StreamType.Video));
                }
            });
        }

        private static IDemuxerController CreateDemuxerController(IObservable<byte[]> chunkReady = null)
        {
            var demuxer = new FFmpegDemuxer(new FFmpegGlue());
            var controller = new DemuxerController(demuxer);
            if (chunkReady != null)
                controller.SetDataSource(chunkReady);
            return controller;
        }
    }
}
