/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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

namespace JuvoLogger.Udp
{
    internal class UdpLogger : LoggerBase
    {
        private static readonly string[] LogLevelNames = Enum.GetNames(typeof(LogLevel));
        private delegate void LogMethod(string tag, string message, string file, string func, int line);

        private readonly Func<UdpLoggerService> GetService;

        public const string LogFormat = "{1}/{0}: {3}: {4}({5}) > {2}\r\n";

        public UdpLogger(string channel, LogLevel level, Func<UdpLoggerService> getService) : base(channel, level)
        {
            GetService = getService;
        }

        public override void PrintLog(LogLevel level, string message, string file, string method, int line)
        {
            var fullPathSpan = file.AsSpan();
            var fnameStart = fullPathSpan.LastIndexOfAny('/', '\\') + 1; //Handles found & not found cases (-1)
            var fileName = fullPathSpan.Slice(fnameStart).ToString();
            GetService()?.Log(Channel, LogLevelNames[(int)level], message, fileName, method, line);
        }
    }
}
