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

using System.Collections.Generic;

namespace JuvoLogger.Tizen
{
    public class TizenLogger : LoggerBase
    {
        private const int MaxLen = 800;

        private delegate void LogMethod(string tag, string message, string file, string func, int line);

        public TizenLogger(string channel, LogLevel level) : base(channel, level)
        {
        }

        public override void PrintLog(LogLevel level, string message, string file, string method, int line)
        {
            LogMethod tizenLog;
            switch (level)
            {
                case LogLevel.Verbose:
                    tizenLog = global::Tizen.Log.Verbose;
                    break;
                case LogLevel.Debug:
                    tizenLog = global::Tizen.Log.Debug;
                    break;
                case LogLevel.Info:
                    tizenLog = global::Tizen.Log.Info;
                    break;
                case LogLevel.Warn:
                    tizenLog = global::Tizen.Log.Warn;
                    break;
                case LogLevel.Error:
                    tizenLog = global::Tizen.Log.Error;
                    break;
                case LogLevel.Fatal:
                    tizenLog = global::Tizen.Log.Fatal;
                    break;
                default:
                    tizenLog = global::Tizen.Log.Error;
                    break;
            }

            if (message.Length <= MaxLen)
                tizenLog(Channel, message, file, method, line);
            else
            {
                foreach (var messagePart in SplitMessage(message))
                    tizenLog(Channel, messagePart, file, method, line);
            }
        }

        private static IEnumerable<string> SplitMessage(string message)
        {
            // Rules:
            // 1. If a message is less than or equal maxLen, then it's not split.
            // 2. If a message is greater than maxLen, then it's split firstly by a new line character.
            // 3. If a particular line is greater than maxLen, then it's split by maxLen size.
            //
            // Note: dlog's max log size is about 950 characters.

            if (message.Length <= MaxLen)
                return new[] {message};
            var result = new List<string>();
            foreach (var line in message.Split('\n'))
            {
                if (line.Length <= MaxLen)
                    result.Add(line);
                else
                {
                    var start = 0;
                    for (var end = MaxLen; end < line.Length; end += MaxLen)
                    {
                        result.Add(line.Substring(start, MaxLen));
                        start = end;
                    }
                    result.Add(line.Substring(start));
                }
            }
            return result;
        }
    }
}
