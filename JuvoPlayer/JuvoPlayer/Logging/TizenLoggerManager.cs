using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JuvoPlayer.Common.Logging;
using Tizen.Applications;

namespace JuvoPlayer.Logging
{
    public class TizenLoggerManager : LoggerManager
    {
        protected TizenLoggerManager(Dictionary<string, LogLevel> loggingLevels, CreateLoggerFunc createLoggerFunc) : base(loggingLevels, createLoggerFunc)
        {
        }

        private static LoggerBase CreateLogger(string channel, LogLevel level) => new TizenLogger(channel, level);

        public static void Configure(Stream stream)
        {
            Configure(stream, CreateLogger);
        }

        public static void Configure(string contents)
        {
            Configure(contents, CreateLogger);
        }

        public static void Configure()
        {
            var configFilename = Path.Combine(
                Path.GetDirectoryName(Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location)),
                "res", "logger.config");

            var contents = string.Empty;
            if (File.Exists(configFilename))
            {
                contents = File.ReadAllText(configFilename);
            }
            Configure(contents);
        }
    }
}
