using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JuvoPlayer.Common.Logging
{
    public class ConsoleLogger : LoggerBase
    {
        public ConsoleLogger(string channel, LogLevel level) : base(channel, level)
        {
        }

        public override void PrintLog(LogLevel level, string message, string file, string method, int line)
        {
            var uri = new Uri("file://" + file);
            Console.WriteLine(String.Format("[{0}] {1}: {2}:{3} > {4}", level, Path.GetFileName(uri.AbsolutePath), method, line, message));
        }
    }
}
