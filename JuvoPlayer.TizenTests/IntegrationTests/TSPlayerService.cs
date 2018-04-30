using System;
using System.Threading.Tasks;
using JuvoPlayer.TizenTests.Utils;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    [Ignore("These tests usually cause crashes/hangs/out of memory issues")]
    class TSPlayerService
    {
        [TestCase("Clean MP4 over HTTP")]
        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        [TestCase("Clean HLS")]
        [TestCase("Clean HEVC 4k MPEG DASH")]
        public async Task Playback_Basic_PreparesAndStarts(string clipTitle)
        {
            using (var service = new PlayerService())
            {
                PrepareAndStart(service, clipTitle);

                await Task.Delay(TimeSpan.FromSeconds(1));

                Assert.That(service.CurrentPosition, Is.GreaterThan(TimeSpan.Zero));
            }
        }

        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        public void Seek_Random10Times_SeeksWithin500Milliseconds(string clipTitle)
        {
            var rand = new Random();
            using (var service = new PlayerService())
            {
                PrepareAndStart(service, clipTitle);

                for (var i = 0; i < 10; ++i)
                {
                    var seekTime = TimeSpan.FromSeconds(rand.Next((int) service.Duration.TotalSeconds));

                    service.SeekTo(seekTime);

                    Assert.That(() => service.CurrentPosition,
                        Is.EqualTo(seekTime)
                            .Within(500).Milliseconds
                            .After(10).Seconds
                            .PollEvery(100).MilliSeconds);
                }
            }
        }

        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        public void Seek_Backward_SeeksWithin500Milliseconds(string clipTitle)
        {
            using (var service = new PlayerService())
            {
                PrepareAndStart(service, clipTitle);

                for (var nextSeekTime = service.Duration - TimeSpan.FromSeconds(5); nextSeekTime > TimeSpan.Zero; nextSeekTime -= TimeSpan.FromSeconds(20))
                {
                    service.SeekTo(nextSeekTime);

                    Assert.That(() => service.CurrentPosition,
                        Is.EqualTo(nextSeekTime)
                            .Within(500).Milliseconds
                            .After(10).Seconds
                            .PollEvery(100).MilliSeconds
                        );
                }
            }
        }

        private static void PrepareAndStart(PlayerService service, string clipTitle)
        {
            var clips = service.ReadClips();
            var clip = clips.Find(_ => _.Title.Equals(clipTitle));

            Assert.That(clip, Is.Not.Null);

            service.SetClipDefinition(clip);

            Assert.That(() => service.State, Is.EqualTo(PlayerService.PlayerState.Prepared)
                .After(10).Seconds
                .PollEvery(100).MilliSeconds);

            service.Start();
        }
    }
}
