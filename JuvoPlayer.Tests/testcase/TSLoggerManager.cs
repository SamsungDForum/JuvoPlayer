using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using JuvoPlayer.Common.Logging;
using NUnit.Framework;

namespace JuvoPlayer.Tests
{
    [TestFixture]
    class TSLoggerManager
    {
        LoggerBase CreateLogger(string channel, LogLevel level) => new DummyLogger(channel, level);

        [TearDown]
        public void Reset()
        {
            LoggerManager.ResetForTests();
        }

        [Test]
        public void TestConfigureWithDefaultLevels()
        {
            LoggerManager.Configure(CreateLogger);
            LoggerManager manager = LoggerManager.GetInstance();

            ILogger firstLogger = manager.GetLogger("LogChannel1");
            Assert.That(firstLogger.IsLevelEnabled(LogLevel.Debug), Is.True);

            ILogger secondLogger = manager.GetLogger("LogChannel2");
            Assert.That(secondLogger.IsLevelEnabled(LogLevel.Debug), Is.True);
        }

        [Test]
        public void TestConfigureWithValidConfigFile()
        {
            var contents = LoadConfig("logging_valid.config");
            LoggerManager.Configure(contents, CreateLogger);
            LoggerManager manager = LoggerManager.GetInstance();

            ILogger juvoPlayerLogger = manager.GetLogger("JuvoPlayer");
            Assert.That(juvoPlayerLogger.IsLevelEnabled(LogLevel.Debug), Is.True);
            Assert.That(juvoPlayerLogger.IsLevelEnabled(LogLevel.Verbose), Is.False);

            ILogger juvoPlayerCommonLogger = manager.GetLogger("JuvoPlayer.Common");
            Assert.That(juvoPlayerCommonLogger.IsLevelEnabled(LogLevel.Warn), Is.True);
            Assert.That(juvoPlayerCommonLogger.IsLevelEnabled(LogLevel.Debug), Is.False);
        }

        [Test]
        public void TestConfigureWithNull()
        {
            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure(null));

            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure((string) null, null));
            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure((string) null, CreateLogger));
            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure(string.Empty, null));

            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure((Stream) null, null));
            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure((Stream) null, CreateLogger));

            Assert.Throws<InvalidOperationException>(() => LoggerManager.GetInstance());
        }

        [Test]
        public void TestConfigureMultipleTimes()
        {
            var contents = LoadConfig("logging_valid.config");
            LoggerManager.Configure(contents, CreateLogger);

            Assert.That(LoggerManager.GetInstance(), Is.Not.Null);

            Assert.Throws<InvalidOperationException>(() => LoggerManager.Configure(contents, CreateLogger));
            Assert.Throws<InvalidOperationException>(() => LoggerManager.Configure(CreateLogger));
        }

        [Test]
        public void TestGetInstanceBeforeConfigure()
        {
            Assert.Throws<InvalidOperationException>(() => LoggerManager.GetInstance());
        }

        private static string LoadConfig(string filename)
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.Contains(filename)) continue;
                using (var reader = new StreamReader(assembly.GetManifestResourceStream(name)))
                {
                    return reader.ReadToEnd();
                }
            }

            return null;
        }
    }
}

