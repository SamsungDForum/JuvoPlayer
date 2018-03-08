using System.IO;
using System.Reflection;

namespace JuvoLogger.Tizen
{
    public class TizenLoggerManager : LoggerManager
    {
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
