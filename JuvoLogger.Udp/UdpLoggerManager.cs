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
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace JuvoLogger.Udp
{
    public class UdpLoggerManager : LoggerManager
    {
        public static bool IsRunning { get; private set; } = false;
        private const string UdpPortPattern = @"UdpPort\s*=\s*([0-9]+)";
        private static UdpLoggerService _loggerService;
        private static LoggerBase CreateLogger(string channel, LogLevel level) => new UdpLogger(channel, level, GetLoggingService);

        private static UdpLoggerService GetLoggingService()
        {
            return (_loggerService == null || _loggerService.StopOutput) ? null : _loggerService;
        }

        public static void Configure()
        {
            var configFilename = Path.Combine(
                Path.GetDirectoryName(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)), "res", "logger.config");

            if (!File.Exists(configFilename)) return;

            var contents = File.ReadAllText(configFilename);
            IsRunning = GetUdpPort(contents, out var port);
            if (!IsRunning) return;

            _loggerService = _loggerService ?? new UdpLoggerService(port, UdpLogger.LogFormat);
            Configure(contents, CreateLogger);
        }

        private static bool GetUdpPort(in string config, out int port)
        {
            var regEx = new Regex(UdpPortPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var match = regEx.Match(config);
            if (!match.Success)
            {
                port = -1;
                return false;
            }

            if (!int.TryParse(match.Groups[1].Value, out port) || port < 0 || port > ushort.MaxValue)
                return false;

            return true;
        }

        public static void Terminate()
        {
            var currentService = _loggerService;
            _loggerService = null;
            currentService?.Dispose();
        }
    }
}
