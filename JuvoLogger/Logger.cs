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

namespace JuvoLogger
{
    public class Logger : ILogger
    {
        private readonly LogLevel _level;
        private readonly IList<ILoggerSink> _loggerSinks;
        private readonly string _prefix;
        private readonly string _channel;

        public Logger(LogLevel level, IList<ILoggerSink> sinks, string channel, string prefix)
        {
            _level = level;
            _loggerSinks = sinks;
            _channel = channel;
            _prefix = prefix;
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

        public void Warn(Exception ex, string message, string file = "", string method = "", int line = 0)
        {
            if (!string.IsNullOrEmpty(message))
                PrintLogIfEnabled(LogLevel.Warn, message, file, method, line);
            PrintLogIfEnabled(LogLevel.Warn, ex.Message, file, method, line);
        }

        public void Error(string message, string file = "", string method = "", int line = 0)
        {
            PrintLogIfEnabled(LogLevel.Error, message, file, method, line);
        }

        public void Error(Exception ex, string message, string file = "", string method = "", int line = 0)
        {
            if (!string.IsNullOrEmpty(message))
                PrintLogIfEnabled(LogLevel.Error, message, file, method, line);
            PrintLogIfEnabled(LogLevel.Error, ex.Message, file, method, line);
            PrintLogIfEnabled(LogLevel.Error, ex.Source, file, method, line);
            PrintLogIfEnabled(LogLevel.Error, ex.StackTrace, file, method, line);
        }

        public void Fatal(string message, string file = "", string method = "", int line = 0)
        {
            PrintLogIfEnabled(LogLevel.Fatal, message, file, method, line);
        }

        public void Fatal(Exception ex, string message, string file = "", string method = "", int line = 0)
        {
            if (!string.IsNullOrEmpty(message))
                PrintLogIfEnabled(LogLevel.Fatal, message, file, method, line);
            PrintLogIfEnabled(LogLevel.Fatal, ex.Message, file, method, line);
            PrintLogIfEnabled(LogLevel.Fatal, ex.Source, file, method, line);
            PrintLogIfEnabled(LogLevel.Fatal, ex.StackTrace, file, method, line);
        }

        public bool IsLevelEnabled(LogLevel level)
        {
            return level <= _level;
        }

        public ILogger CopyWithPrefix(string prefix)
        {
            return new Logger(_level, _loggerSinks, _channel, prefix);
        }

        public ILogger CopyWithChannel(string channel)
        {
            return new Logger(_level, _loggerSinks, channel, _prefix);
        }

        private void PrintLogIfEnabled(LogLevel level, string message, string file, string method, int line)
        {
            if (!IsLevelEnabled(level))
                return;

            if (_prefix != null)
                message = $"[{_prefix}] {message}";

            foreach (var loggerSink in _loggerSinks)
                loggerSink.PrintLog(_channel, level, message, file, method, line);
        }
    }
}