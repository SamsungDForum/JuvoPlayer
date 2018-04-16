using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoLogger.Tizen;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    class TSPlayerService
    {
        private LoggerManager savedLoggerManager;
        private ILogger Logger;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            savedLoggerManager = LoggerManager.ResetForTests();
            LoggerManager.Configure("JuvoPlayer=Verbose", CreateLoggerFunc);
            Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        }

        private LoggerBase CreateLoggerFunc(string channel, LogLevel level)
        {
            return new TizenLogger(channel, level);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            LoggerManager.RestoreForTests(savedLoggerManager);
        }

        [Test]
        [Repeat(25)]
        public async Task CleanMP4OverHTTP_BasicPlayback_PreparesAndStarts()
        {
            using (var service = new PlayerService())
            {
                var clips = service.ReadClips();
                var mpegDashClip = clips.Find(clip => clip.Title.Equals("Clean MP4 over HTTP"));

                Assert.That(mpegDashClip, Is.Not.Null);

                service.SetClipDefinition(mpegDashClip);

                Assert.That(() => service.State, Is.EqualTo(PlayerService.PlayerState.Prepared).After(10).Seconds.PollEvery(100).MilliSeconds);

                service.Start();

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        [Test]
        [Repeat(25)]
        public void CleanByteRangeMPEGDASH_BasicPlayback_PreparesAndStarts()
        {
            using (var service = new PlayerService())
            {
                var clips = service.ReadClips();
                var mpegDashClip = clips.Find(clip => clip.Title.Equals("Clean byte range MPEG DASH"));

                Assert.That(mpegDashClip, Is.Not.Null);

                service.SetClipDefinition(mpegDashClip);

                Assert.That(() => service.State, Is.EqualTo(PlayerService.PlayerState.Prepared).After(10).Seconds.PollEvery(100).MilliSeconds);

                service.Start();
            }
        }

        [Test]
        public void CleanByteRangeMPEGDash_Seek_SeeksWithin500Milliseconds([Random(0, 180, 30)] int seekTimeInSeconds)
        {
            using (var service = new PlayerService())
            {
                var clips = service.ReadClips();
                var mpegDashClip = clips.Find(clip => clip.Title.Equals("Clean byte range MPEG DASH"));

                Assert.That(mpegDashClip, Is.Not.Null);

                service.SetClipDefinition(mpegDashClip);

                Assert.That(() => service.State, Is.EqualTo(PlayerService.PlayerState.Prepared).After(10).Seconds.PollEvery(100).MilliSeconds);

                service.Start();

                service.SeekTo(TimeSpan.FromSeconds(seekTimeInSeconds));

                Assert.That(() => service.CurrentPosition, Is.EqualTo(TimeSpan.FromSeconds(seekTimeInSeconds)).Within(500).Milliseconds.After(10).Seconds.PollEvery(100).MilliSeconds);
            }
        }

        [Test]
        public void CleanByteRangeMPEGDash_MultipleSeek_SeeksWithin500Milliseconds()
        {
            var rand = new Random();
            using (var service = new PlayerService())
            {
                var clips = service.ReadClips();
                var mpegDashClip = clips.Find(clip => clip.Title.Equals("Clean byte range MPEG DASH"));

                Assert.That(mpegDashClip, Is.Not.Null);

                service.SetClipDefinition(mpegDashClip);

                Assert.That(() => service.State, Is.EqualTo(PlayerService.PlayerState.Prepared).After(10).Seconds.PollEvery(100).MilliSeconds);

                service.Start();

                for (int i = 0; i < 30; ++i)
                {
                    var nextSeekTimeInSeconds = rand.Next(180);

                    service.SeekTo(TimeSpan.FromSeconds(nextSeekTimeInSeconds));

                    Assert.That(() => service.CurrentPosition,
                        Is.EqualTo(TimeSpan.FromSeconds(nextSeekTimeInSeconds)).Within(500).Milliseconds.After(10)
                            .Seconds.PollEvery(100).MilliSeconds);
                }
            }
        }
    }
}
