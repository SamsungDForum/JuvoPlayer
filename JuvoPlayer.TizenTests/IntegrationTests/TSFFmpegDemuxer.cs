using System;
using System.IO;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Demuxers.FFmpeg;
using JuvoPlayer.SharedBuffers;
using JuvoPlayer.TizenTests.Utils;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    class TSFFmpegDemuxer
    {
        private static string BigBuckBunnyUrl = "http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4";

        [Test]
        public void StartForUrl_ContentWithAudioAndVideo_CallsStreamConfigReady()
        {
            var foundVideo = false;
            var foundAudio = false;

            using (var demuxer = CreateFFmpegDemuxer())
            {
                demuxer.StreamConfigReady += config =>
                {
                    if (config.StreamType() == StreamType.Audio)
                        foundAudio = true;
                    else if (config.StreamType() == StreamType.Video)
                        foundVideo = true;
                };

                demuxer.StartForUrl(BigBuckBunnyUrl);

                Assert.That(() => foundAudio, Is.True.After(2).Seconds.PollEvery(50).MilliSeconds);
                Assert.That(() => foundVideo, Is.True.After(2).Seconds.PollEvery(50).MilliSeconds);
            }
        }

        [Test]
        public void StartForUrl_ContentWithDuration_CallsClipDurationChanged()
        {
            var isDurationFound = false;
            var receivedDuration = TimeSpan.Zero;
            var expectedDuration = new TimeSpan(0, 0, 10, 34, 533);

            using (var demuxer = CreateFFmpegDemuxer())
            {
                demuxer.ClipDuration += duration =>
                {
                    if (isDurationFound)
                        return;
                    receivedDuration = duration;
                    isDurationFound = true;
                };

                demuxer.StartForUrl(BigBuckBunnyUrl);

                Assert.That(() => isDurationFound, Is.True.After(2).Seconds.PollEvery(50).MilliSeconds);
                Assert.That(receivedDuration, Is.EqualTo(expectedDuration));
            }
        }

        [Test]
        public void StartForUrl_ContentWithAudioAndVideo_CallsPacketReady()
        {
            var packetReceived = false;

            using (var demuxer = CreateFFmpegDemuxer())
            {
                demuxer.PacketReady += packet => { packetReceived = true; };
                demuxer.StartForUrl(BigBuckBunnyUrl);

                Assert.That(() => packetReceived, Is.True.After(2).Seconds.PollEvery(50).MilliSeconds);
            }
        }

        [Test]
        public async Task Pause_CalledAfterStart_ShouldntCallPacketReady()
        {
            var packetReceived = false;

            using (var demuxer = CreateFFmpegDemuxer())
            {
                demuxer.PacketReady += packet => {
                    packetReceived = true;
                };
                demuxer.StartForUrl(BigBuckBunnyUrl);

                Assert.That(() => packetReceived, Is.True.After(2).Seconds.PollEvery(50).MilliSeconds);

                demuxer.Pause();
                await Task.Delay(TimeSpan.FromMilliseconds(100)); // give demuxer so time to pause itself

                packetReceived = false;
                await Task.Delay(TimeSpan.FromMilliseconds(100));

                Assert.That(() => packetReceived, Is.False);
            }
        }

        [Test]
        public void Resume_CalledAfterPause_CallsPacketReady()
        {
            var packetReceived = false;

            using (var demuxer = CreateFFmpegDemuxer())
            {
                demuxer.PacketReady += packet => {
                    packetReceived = true;
                };

                demuxer.Pause();
                demuxer.StartForUrl(BigBuckBunnyUrl);
                demuxer.Resume();

                Assert.That(() => packetReceived, Is.True.After(2).Seconds.PollEvery(50).MilliSeconds);
            }
        }

        [Test]
        public void Dispose_CalledTwice_ShouldntThrow()
        {
            var demuxer = CreateFFmpegDemuxer();

            demuxer.Dispose();

            Assert.DoesNotThrow(() => { demuxer.Dispose(); });
        }

        private static IDemuxer CreateFFmpegDemuxer(ISharedBuffer sharedBuffer = null)
        {
            return new FFmpegDemuxer(sharedBuffer);
        }
    }
}
