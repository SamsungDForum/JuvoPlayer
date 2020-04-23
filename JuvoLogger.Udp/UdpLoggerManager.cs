using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoLogger.Udp
{
    public class UdpLoggerManager: LoggerManager
    {
        private static LoggerBase CreateLogger(string channel, LogLevel level) => new UdpLogger(channel, level, _loggerService);
        private static UdpLoggerService _loggerService;
        public static void Configure(string ip, int port)
        {
            _loggerService = _loggerService ?? new UdpLoggerService(ip, port);
            Configure(CreateLogger);
        }
    }
}
