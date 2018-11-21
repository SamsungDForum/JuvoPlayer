using System;
using System.IO;
using System.Reactive.Linq;
using System.Threading;
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
        private static string BigBuckBunnyUrl =
            "http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4";

        [Test]
        public void StartForUrl_ContentWithAudioAndVideo_CallsStreamConfigReady()
        {
            var foundVideo = false;
            var foundAudio = false;

            using (var demuxer = CreateFFmpegDemuxer())
            {
                demuxer.StreamConfigReady().Subscribe(config =>
                {
                    if (config.StreamType() == StreamType.Audio)
                        foundAudio = true;
                    else if (config.StreamType() == StreamType.Video)
                        foundVideo = true;
                }, SynchronizationContext.Current);

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
                demuxer.ClipDurationChanged().Subscribe(duration =>
                {
                    if (isDurationFound)
                        return;
                    receivedDuration = duration;
                    isDurationFound = true;
                }, SynchronizationContext.Current);

                demuxer.StartForUrl(BigBuckBunnyUrl);

                Assert.That(() => isDurationFound, Is.True.After(2).Seconds.PollEvery(50).MilliSeconds);
                Assert.That(receivedDuration, Is.EqualTo(expectedDuration));
            }
        }

        [Test]
        public async Task StartForUrl_ContentWithAudioAndVideo_CallsPacketReady()
        {
            using (var demuxer = CreateFFmpegDemuxer())
            {
                demuxer.StartForUrl(BigBuckBunnyUrl);
                await demuxer.PacketReady().FirstAsync().Timeout(TimeSpan.FromSeconds(2));
            }
        }

        [Test]
        public async Task Pause_CalledAfterStart_ShouldntCallPacketReady()
        {
            using (var demuxer = CreateFFmpegDemuxer())
            {
                demuxer.StartForUrl(BigBuckBunnyUrl);

                await demuxer.PacketReady().FirstAsync().Timeout(TimeSpan.FromSeconds(2));

                demuxer.Pause();
                await Task.Delay(TimeSpan.FromMilliseconds(100)); // give demuxer so time to pause itself

                Assert.ThrowsAsync<TimeoutException>(async () =>
                    await demuxer.PacketReady().FirstAsync().Timeout(TimeSpan.FromMilliseconds(100)));
            }
        }

        [Test]
        public async Task Resume_CalledAfterPause_CallsPacketReady()
        {
            using (var demuxer = CreateFFmpegDemuxer())
            {
                demuxer.Pause();
                demuxer.StartForUrl(BigBuckBunnyUrl);
                demuxer.Resume();

                await demuxer.PacketReady().FirstAsync().Timeout(TimeSpan.FromSeconds(2));
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
            return new FFmpegDemuxer(ResolveFFmpegLibDir(), sharedBuffer);
        }

        private static string ResolveFFmpegLibDir()
        {
            return Path.Combine(Paths.ApplicationPath, "lib");
        }
    }
}