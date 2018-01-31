using System;
using System.Collections.Generic;
using System.IO;

namespace JuvoPlayer.Common.Logging
{
    public class LoggerManager
    {
        private static LoggerManager _instance;

        private readonly Dictionary<string, LogLevel> _loggingLevels;
        private readonly CreateLoggerFunc _createLoggerFunc;
        private readonly List<LoggerBase> _loggers = new List<LoggerBase>();
        private static readonly LogLevel DefaultLogLevel = LogLevel.Debug;

        public delegate LoggerBase CreateLoggerFunc(string channel, LogLevel level);

        protected LoggerManager(Dictionary<string, LogLevel> loggingLevels, CreateLoggerFunc createLoggerFunc)
        {
            _loggingLevels = loggingLevels;
            _createLoggerFunc = createLoggerFunc;
        }

        public static void ResetForTests()
        {
            _instance = null;
        }

        public static void Configure(CreateLoggerFunc createLoggerFunc)
        {
            if (createLoggerFunc == null)
                throw new ArgumentNullException();
            if (_instance != null)
                throw new InvalidOperationException("LoggerManager is already configured");

            _instance = new LoggerManager(new Dictionary<string, LogLevel>(), createLoggerFunc);
        }

        public static void Configure(string configData, CreateLoggerFunc createLoggerFunc)
        {
            if (configData == null || createLoggerFunc == null)
                throw new ArgumentNullException();
            if (_instance != null)
                throw new InvalidOperationException("LoggerManager is already configured");

            var configParser = new ConfigParser(configData);
            _instance = new LoggerManager(configParser.LoggingLevels, createLoggerFunc);
        }

        public static void Configure(Stream configStream, CreateLoggerFunc createLoggerFunc)
        {
            if (configStream == null || createLoggerFunc == null)
                throw new ArgumentNullException();
            if (_instance != null)
                throw new InvalidOperationException("LoggerManager is already configured");

            var configData = string.Empty;
            using (var reader = new StreamReader(configStream))
            {
                configData = reader.ReadToEnd();
            }

            Configure(configData, createLoggerFunc);
        }

        public static LoggerManager GetInstance()
        {
            if (_instance == null)
                throw new InvalidOperationException("LoggerManager.Configure() should be called before GetInstance()");
            return _instance;
        }

        public ILogger GetLogger(string channel)
        {
            foreach (var logger in _loggers)
            {
                if (logger.Channel.Equals(channel))
                    return logger;
            }

            LogLevel newLogLevel = DefaultLogLevel;
            if (_loggingLevels.ContainsKey(channel))
                newLogLevel = _loggingLevels[channel];

            LoggerBase newLogger = _createLoggerFunc(channel, newLogLevel);
            _loggers.Add(newLogger);
            return newLogger;
        }
    }
}
