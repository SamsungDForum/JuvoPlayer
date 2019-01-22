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

namespace JuvoLogger.Tizen
{
    public class TizenLogger : LoggerBase
    {
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

            tizenLog(Channel, message, file, method, line);
        }
    }
}
