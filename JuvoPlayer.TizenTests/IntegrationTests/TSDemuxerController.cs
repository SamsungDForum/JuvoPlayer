using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Demuxers.FFmpeg;
using Nito.AsyncEx;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    public class TSDemuxerController
    {
        private byte[] initSegment;

        private byte[] dataSegment;

        private static string BigBuckBunnyUrl =
            "http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            initSegment = ReadAllBytes("JuvoPlayer.TizenTests.res.googlecar.car-20120827-89.mp4-init-segment");
            Assert.That(initSegment, Is.Not.Null);

            dataSegment = ReadAllBytes("JuvoPlayer.TizenTests.res.googlecar.car-20120827-89.mp4-3901498-7700066");
            Assert.That(dataSegment, Is.Not.Null);
        }

        private static byte[] ReadAllBytes(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(resourceName);
            using (var reader = new BinaryReader(stream))
            {
                return reader.ReadBytes((int) stream.Length);
            }
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
                        controller.StartForEs(InitializationMode.Full);
                        var configReadyTask = controller.StreamConfigReady().FirstAsync().ToTask();

                        chunkReady.OnNext(initSegment);
                        chunkReady.OnNext(dataSegment);

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
                    controller.StartForEs(InitializationMode.Full);

                    var packetReadyTask = controller.PacketReady().FirstAsync().ToTask();

                    chunkReady.OnNext(initSegment);
                    chunkReady.OnNext(dataSegment);

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