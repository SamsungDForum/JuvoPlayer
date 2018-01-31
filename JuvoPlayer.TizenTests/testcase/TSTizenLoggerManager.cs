using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JuvoPlayer.Common.Logging;
using JuvoPlayer.Logging;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests
{
    [TestFixture]
    class TSTizenLoggerManager
    {

        [TearDown]
        public void Reset()
        {
            LoggerManager.ResetForTests();
        }

        [Test]
        public void TestDefaultConfigure()
        {
            TizenLoggerManager.Configure();
            LoggerManager manager = LoggerManager.GetInstance();

            ILogger logger = manager.GetLogger("JuvoPlayer");
            Assert.That(logger.IsLevelEnabled(LogLevel.Info), Is.True);
            Assert.That(logger.IsLevelEnabled(LogLevel.Debug), Is.False);
        }
    }
}
