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
using IniParser.Model;
using IniParser.Parser;

namespace JuvoLogger.Udp
{
    public class UdpLoggerManager : LoggerManager
    {
        public static bool IsRunning { get; private set; } = false;
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

            var parser = new IniDataParser();
            var iniData = parser.Parse(File.ReadAllText(configFilename));
            var udpLoggerSection = iniData["UdpLogger"];

            ushort port = 0;
            IsRunning = udpLoggerSection.ContainsKey("Port") && ushort.TryParse(udpLoggerSection["Port"], out port);
            if (!IsRunning) return;

            if (_loggerService != null)
                return;

            _loggerService = new UdpLoggerService();
            _ = _loggerService.StartLogger(port, UdpLogger.LogFormat);

            Configure(iniData, CreateLogger);
        }

        public static void Configure(in IniData contents)
        {
            Configure(contents, CreateLogger);
        }

        public static void Terminate()
        {
            var currentService = _loggerService;
            _loggerService = null;
            currentService?.Dispose();
        }
    }
}
