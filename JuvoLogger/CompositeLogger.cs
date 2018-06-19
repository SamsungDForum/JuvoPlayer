using System.Collections.Generic;

namespace JuvoLogger
{
    public class CompositeLogger : LoggerBase
    {
        private List<LoggerBase> loggers = new List<LoggerBase>();

        public CompositeLogger(string channel, LogLevel level) : base(channel, level)
        {
        }

        public void Add(LoggerBase logger)
        {
            loggers.Add(logger);
        }

        public override void PrintLog(LogLevel level, string message, string file, string method, int line)
        {
            foreach (var logger in loggers)
            {
                logger.PrintLog(level, message, file, method, line);
            }
        }
    }
}
