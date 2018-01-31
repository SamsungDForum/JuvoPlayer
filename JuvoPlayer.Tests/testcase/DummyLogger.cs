using System.Collections.Generic;
using JuvoPlayer.Common.Logging;

namespace JuvoPlayer.Tests
{

    internal class DummyLogger : LoggerBase
    {
        public class LoggingData
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

        public List<LoggingData> Logs { get; } = new List<LoggingData>();

        public DummyLogger(string channel, LogLevel level) : base(channel, level)
        {
        }

        protected override void PrintLog(LogLevel level, string message, string file, string method, int line)
        {
            Logs.Add(new LoggingData(level, message, file, method, line));
        }
    }
}
