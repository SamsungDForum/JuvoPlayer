using System;
using System.Text;
using JuvoPlayer.Common.Logging;
using NUnit.Framework;

namespace JuvoPlayer.Tests
{
    [TestFixture]
    partial class TSLoggerBase
    {
        [Test]
        public void TestLoggerBase()
        {
            var dummyChannel = "channel";
            foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
            {
                var logger = new DummyLogger(dummyChannel, level);
                var loggerClient = new LoggerClient(logger);
                loggerClient.Func();

                var expectedLogsCount = (int) level + 1;
                Assert.That(logger.Logs.Count, Is.EqualTo(expectedLogsCount));

                foreach (var log in logger.Logs)
                {
                    // LoggerBase returns full path and subclasses must resolve file name manually.
                    // This is needed because TizenLogger requires full path.
                    Assert.That(log.File, Does.EndWith(LoggerClient.FileName));
                    Assert.That(log.Method, Is.EqualTo(nameof(loggerClient.Func)));
                    Assert.That(log.Message, Is.EqualTo(LoggerClient.LogMessage));
                }

                for (var index = 0; index < expectedLogsCount; ++index)
                {
                    var lineNumber = LoggerClient.FuncFirstLineNumber + index;
                    Assert.That(logger.Logs[index].Line, Is.EqualTo(lineNumber));
                }
            }
        }

        [Test]
        [Description("Convinient test for ConsoleLogger class. Tester must check logs manually.")]
        public void TestConsoleLogger()
        {
            var dummyChannel = "channel";
            var logger = new ConsoleLogger(dummyChannel, LogLevel.Verbose);
            var loggerClient = new LoggerClient(logger);
            loggerClient.Func();
        }

        [Test]
        public void TestConstructorWithNull()
        {
            Assert.Throws<ArgumentNullException>(() => new DummyLogger(null, LogLevel.Fatal));
        }

        [Test]
        public void TestInvalidLogLevel()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DummyLogger("channel", (LogLevel) int.MinValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DummyLogger("channel", (LogLevel) int.MaxValue));
        }
    }
}
