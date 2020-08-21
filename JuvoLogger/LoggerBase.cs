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

namespace JuvoLogger
{
    public abstract class LoggerBase : ILogger
    {
        public string Channel { get; }

        private LogLevel level;

        protected LogLevel Level
        {
            get => level;
            set => level = Enum.IsDefined(typeof(LogLevel), value) ? value : throw new ArgumentOutOfRangeException();
        }

        protected LoggerBase(string channel, LogLevel level)
        {
            Channel = channel ?? throw new ArgumentNullException();
            this.Level = level;
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

        private void PrintLogIfEnabled(LogLevel level, string message, string file, string method, int line)
        {
            if (!IsLevelEnabled(level)) return;
            PrintLog(level, message, file, method, line);
        }

        public abstract void PrintLog(LogLevel level, string message, string file, string method, int line);

        public bool IsLevelEnabled(LogLevel level)
        {
            return level <= this.Level;
        }
    }
}
