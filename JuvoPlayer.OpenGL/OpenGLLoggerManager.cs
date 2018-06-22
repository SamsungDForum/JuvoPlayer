using System;
using System.IO;
using System.Reflection;
using System.Threading;
using JuvoLogger;
using JuvoLogger.Tizen;

namespace JuvoPlayer.OpenGL
{
    class OpenGLLoggerManager : LoggerManager
    {
        private static SynchronizationContext _uiContext = null;

        private static LoggerBase CreateLogger(string channel, LogLevel level)
        {
            if (_uiContext == null)
                throw new NullReferenceException("OpenGLLoggerManager must be first configured with proper UI SynchronizationContext object!");

            var composite = new CompositeLogger(channel, level);
            composite.Add(new TizenLogger(channel, level));
            composite.Add(new OpenGLLogger(channel, level, _uiContext));
            return composite;
        }

        public static void Configure(Stream stream, SynchronizationContext uiContext)
        {
            _uiContext = uiContext;
            Configure(stream, CreateLogger);
        }

        public static void Configure(string contents, SynchronizationContext uiContext)
        {
            _uiContext = uiContext;
            Configure(contents, CreateLogger);
        }

        public static void Configure(SynchronizationContext uiContext)
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
            Configure(contents, uiContext);
        }
    }
}
