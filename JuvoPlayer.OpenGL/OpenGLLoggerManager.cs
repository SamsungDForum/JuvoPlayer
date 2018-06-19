using System.IO;
using System.Reflection;
using JuvoLogger;
using JuvoLogger.Tizen;

namespace JuvoPlayer.OpenGL
{
    class OpenGLLoggerManager : LoggerManager
    {
        private static LoggerBase CreateLogger(string channel, LogLevel level)
        {
            var composite = new CompositeLogger(channel, level);
            composite.Add(new TizenLogger(channel, level));
            composite.Add(new OpenGLLogger(channel, level));
            return composite;
        }

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
