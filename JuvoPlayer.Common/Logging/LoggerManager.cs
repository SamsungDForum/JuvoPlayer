using System;
using System.Collections.Generic;
using System.IO;

namespace JuvoPlayer.Common.Logging
{
    public class LoggerManager
    {
        private static LoggerManager Instance;

        private readonly Dictionary<string, LogLevel> loggingLevels;
        private readonly CreateLoggerFunc createLoggerFunc;
        private readonly List<LoggerBase> loggers = new List<LoggerBase>();
        private static readonly LogLevel DefaultLogLevel = LogLevel.Debug;

        public delegate LoggerBase CreateLoggerFunc(string channel, LogLevel level);

        protected LoggerManager(Dictionary<string, LogLevel> loggingLevels, CreateLoggerFunc createLoggerFunc)
        {
            this.loggingLevels = loggingLevels;
            this.createLoggerFunc = createLoggerFunc;
        }

        public static LoggerManager ResetForTests()
        {
            LoggerManager instance = Instance;
            Instance = null;
            return instance;
        }

        public static void RestoreForTests(LoggerManager instance)
        {
            Instance = instance;
        }

        public static void Configure(CreateLoggerFunc createLoggerFunc)
        {
            if (createLoggerFunc == null)
                throw new ArgumentNullException();
            if (Instance != null)
                throw new InvalidOperationException("LoggerManager is already configured");

            Instance = new LoggerManager(new Dictionary<string, LogLevel>(), createLoggerFunc);
        }

        public static void Configure(string configData, CreateLoggerFunc createLoggerFunc)
        {
            if (configData == null || createLoggerFunc == null)
                throw new ArgumentNullException();
            if (Instance != null)
                throw new InvalidOperationException("LoggerManager is already configured");

            var configParser = new ConfigParser(configData);
            Instance = new LoggerManager(configParser.LoggingLevels, createLoggerFunc);
        }

        public static void Configure(Stream configStream, CreateLoggerFunc createLoggerFunc)
        {
            if (configStream == null || createLoggerFunc == null)
                throw new ArgumentNullException();
            if (Instance != null)
                throw new InvalidOperationException("LoggerManager is already configured");

            string configData;
            using (var reader = new StreamReader(configStream))
            {
                configData = reader.ReadToEnd();
            }

            Configure(configData, createLoggerFunc);
        }

        public static LoggerManager GetInstance()
        {
            if (Instance == null)
                throw new InvalidOperationException("LoggerManager.Configure() should be called before GetInstance()");
            return Instance;
        }

        public ILogger GetLogger(string channel)
        {
            foreach (var logger in loggers)
            {
                if (logger.Channel.Equals(channel))
                    return logger;
            }

            LogLevel newLogLevel = DefaultLogLevel;
            if (loggingLevels.ContainsKey(channel))
                newLogLevel = loggingLevels[channel];

            LoggerBase newLogger = createLoggerFunc(channel, newLogLevel);
            loggers.Add(newLogger);
            return newLogger;
        }
    }
}
