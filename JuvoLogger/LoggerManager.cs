/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using IniParser.Model;

namespace JuvoLogger
{
    public class LoggerManager
    {
        private static LoggerManager Instance;

        private Dictionary<string, LogLevel> loggingLevels;
        private CreateLoggerFunc createLoggerFunc;
        private readonly List<LoggerWrapper> loggers = new List<LoggerWrapper>();
        private static readonly LogLevel DefaultLogLevel = LogLevel.Debug;

        public delegate LoggerBase CreateLoggerFunc(string channel, LogLevel level);

        private class LoggerWrapper : LoggerBase
        {
            public LoggerBase Logger { get; set; }

            public override void PrintLog(LogLevel level, string message, string file, string method, int line)
            {
                Logger?.PrintLog(level, message, file, method, line);
            }

            public LoggerWrapper(string channel, LogLevel level) : base(channel, level)
            {
            }

            public void Update(LoggerBase newLogger, LogLevel level)
            {
                Level = level;
                Logger = newLogger;
            }
        }

        protected LoggerManager()
        {
        }

        private void Update(Dictionary<string, LogLevel> loggingLevels, CreateLoggerFunc createLoggerFunc)
        {
            this.loggingLevels = loggingLevels;
            this.createLoggerFunc = createLoggerFunc;

            foreach (var loggerWrapper in loggers)
            {
                var channel = loggerWrapper.Channel;
                var newLevel = loggingLevels.ContainsKey(channel) ? loggingLevels[channel] : DefaultLogLevel;
                LoggerBase newLogger = createLoggerFunc?.Invoke(loggerWrapper.Channel, newLevel);
                loggerWrapper.Update(newLogger, newLevel);
            }
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
            if (Instance == null)
                Instance = new LoggerManager();

            Instance.Update(new Dictionary<string, LogLevel>(), createLoggerFunc);
        }

        public static void Configure(in IniData configData, CreateLoggerFunc createLoggerFunc)
        {
            if (configData == null)
                throw new ArgumentNullException();
            if (Instance == null)
                Instance = new LoggerManager();

            var configParser = new ConfigParser(configData);
            Instance.Update(configParser.LoggingLevels, createLoggerFunc);
        }

        public static void Configure(string configData, CreateLoggerFunc createLoggerFunc)
        {
            if (configData == null)
                throw new ArgumentNullException();
            if (Instance == null)
                Instance = new LoggerManager();

            var configParser = new ConfigParser(configData);
            Instance.Update(configParser.LoggingLevels, createLoggerFunc);
        }

        public static void Configure(Stream configStream, CreateLoggerFunc createLoggerFunc)
        {
            if (configStream == null)
                throw new ArgumentNullException();
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
                Configure(null);
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

            LoggerBase newLogger = createLoggerFunc?.Invoke(channel, newLogLevel);
            LoggerWrapper wrapped = new LoggerWrapper(channel, newLogLevel)
            {
                Logger = newLogger
            };
            loggers.Add(wrapped);
            return wrapped;
        }
    }
}
