using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Common.Logging
{
    public class ConsoleLogger : LoggerBase
    {
        public ConsoleLogger(string channel, LogLevel level) : base(channel, level)
        {
        }

        protected override void PrintLog(LogLevel level, string message, string file, string method, int line)
        {
            Console.WriteLine(String.Format("[{0}] {1}: {2}:{3} > {4}", level, file, method, line, message));
        }
    }
}
