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

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            savedLoggerManager = LoggerManager.ResetForTests();
            LoggerManager.Configure("JuvoPlayer=Verbose", CreateLoggerFunc);
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
        public async Task TestBasicPlayback()
        {
            using (var service = new PlayerService())
            {
                var clips = service.ReadClips();
                var mpegDashClip = clips.Find(clip => clip.Title.Equals("Clean MP4 over HTTP"));

                Assert.That(mpegDashClip, Is.Not.Null);

                service.SetClipDefinition(mpegDashClip);

                int retryCount = 0;
                while (service.State != PlayerService.PlayerState.Prepared && retryCount < 10)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    Console.WriteLine(service.State);
                    retryCount++;
                }

                Assert.That(service.State, Is.EqualTo(PlayerService.PlayerState.Prepared));

                service.Start();

                await Task.Delay(TimeSpan.FromSeconds(60));
            }
        }
    }
}
