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

namespace JuvoLogger
{
    public class LoggerManager
    {
        public delegate LoggerBase CreateLoggerFunc(string channel, LogLevel level);

        private static LoggerManager _instance;
        private static readonly LogLevel DefaultLogLevel = LogLevel.Debug;
        private readonly List<LoggerWrapper> _loggers = new List<LoggerWrapper>();
        private CreateLoggerFunc _createLoggerFunc;

        private Dictionary<string, LogLevel> _loggingLevels;

        protected LoggerManager()
        {
        }

        private void Update(Dictionary<string, LogLevel> loggingLevels, CreateLoggerFunc createLoggerFunc)
        {
            _loggingLevels = loggingLevels;
            _createLoggerFunc = createLoggerFunc;

            foreach (var loggerWrapper in _loggers)
            {
                var channel = loggerWrapper.Channel;
                var newLevel = loggingLevels.ContainsKey(channel) ? loggingLevels[channel] : DefaultLogLevel;
                var newLogger = createLoggerFunc?.Invoke(loggerWrapper.Channel, newLevel);
                loggerWrapper.Update(newLogger, newLevel);
            }
        }

        public static LoggerManager ResetForTests()
        {
            var instance = _instance;
            _instance = null;
            return instance;
        }

        public static void RestoreForTests(LoggerManager instance)
        {
            _instance = instance;
        }

        public static void Configure(CreateLoggerFunc createLoggerFunc)
        {
            if (_instance == null)
                _instance = new LoggerManager();

            _instance.Update(new Dictionary<string, LogLevel>(), createLoggerFunc);
        }

        public static void Configure(string configData, CreateLoggerFunc createLoggerFunc)
        {
            if (configData == null)
                throw new ArgumentNullException();
            if (_instance == null)
                _instance = new LoggerManager();

            var configParser = new ConfigParser(configData);
            _instance.Update(configParser.LoggingLevels, createLoggerFunc);
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
            if (_instance == null)
                Configure(null);
            return _instance;
        }

        public ILogger GetLogger(string channel)
        {
            foreach (var logger in _loggers)
            {
                if (logger.Channel.Equals(channel))
                    return logger;
            }

            var newLogLevel = DefaultLogLevel;
            if (_loggingLevels.ContainsKey(channel))
                newLogLevel = _loggingLevels[channel];

            var newLogger = _createLoggerFunc?.Invoke(channel, newLogLevel);
            var wrapped = new LoggerWrapper(channel, newLogLevel) { Logger = newLogger };
            _loggers.Add(wrapped);
            return wrapped;
        }

        private class LoggerWrapper : LoggerBase
        {
            public LoggerWrapper(string channel, LogLevel level) : base(channel, level)
            {
            }

            public LoggerBase Logger { get; set; }

            public override void PrintLog(LogLevel level, string message, string file, string method, int line)
            {
                Logger?.PrintLog(level, message, file, method, line);
            }

            public void Update(LoggerBase newLogger, LogLevel level)
            {
                Level = level;
                Logger = newLogger;
            }
        }
    }
}