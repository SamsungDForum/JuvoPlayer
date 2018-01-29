using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static JuvoPlayer.Common.Logging.LogLevel;

namespace JuvoPlayer.Common.Logging
{
    public abstract class LoggerBase : ILogger
    {
        protected string Channel;
        private readonly LogLevel _level;

        protected LoggerBase(string channel, LogLevel level)
        {
            Channel = channel;
            _level = level;
        }

        public void Verbose(string message, string file = "", string method = "", int line = 0)
        {
            PrintLogIfEnabled(LogLevel.Verbose, message, file, method, line);
        }

        public void Debug(string message, string file = "", string method = "", int line = 0)
        {
            PrintLogIfEnabled(LogLevel.Debug, message, file, method, line);
        }

        public void Info(string message, string file = "", string method = "", int line = 0)
        {
            PrintLogIfEnabled(LogLevel.Info, message, file, method, line);
        }

        public void Warn(string message, string file = "", string method = "", int line = 0)
        {
            PrintLogIfEnabled(LogLevel.Warn, message, file, method, line);
        }

        public void Error(string message, string file = "", string method = "", int line = 0)
        {
            PrintLogIfEnabled(LogLevel.Error, message, file, method, line);
        }

        public void Fatal(string message, string file = "", string method = "", int line = 0)
        {
            PrintLogIfEnabled(LogLevel.Fatal, message, file, method, line);
        }

        private void PrintLogIfEnabled(LogLevel level, string message, string file, string method, int line)
        {
            if (!IsLevelEnabled(level)) return;
            PrintLog(level, message, file, method, line);
        }

        protected abstract void PrintLog(LogLevel level, string message, string file, string method, int line);

        public bool IsLevelEnabled(LogLevel level)
        {
            return level <= _level;
        }
    }
}
