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
        private LoggerManager savedInstance;

        [SetUp]
        public void SetUp()
        {
            savedInstance = LoggerManager.ResetForTests();
        }
        [TearDown]
        public void TearDown()
        {
            LoggerManager.RestoreForTests(savedInstance);
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
