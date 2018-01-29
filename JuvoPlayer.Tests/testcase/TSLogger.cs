using System;
using System.Collections.Generic;
using System.Text;
using JuvoPlayer.Common.Logging;
using NUnit.Framework;

namespace JuvoPlayer.Tests
{
    [TestFixture]
    class TSLogger
    {
        class LoggingData
        {
            public LogLevel Level { get; set; }
            public string Message { get; set; }
            public string File { get; set; }
            public string Method { get; set; }
            public int Line { get; set; }

            public LoggingData(LogLevel level, string message, string file, string method, int line)
            {
                Level = level;
                Message = message;
                File = file;
                Method = method;
                Line = line;
            }
        }

        class DummyLogger : LoggerBase
        {
            public List<LoggingData> Logs { get; } = new List<LoggingData>();

            public DummyLogger(string channel, LogLevel level) : base(channel, level)
            {
            }

            protected override void PrintLog(LogLevel level, string message, string file, string method, int line)
            {
                Logs.Add(new LoggingData(level, message, file, method, line));
            }
        }

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
    }
}
