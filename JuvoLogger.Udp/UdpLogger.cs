
using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoLogger.Udp
{
    class UdpLogger : LoggerBase
    {
        private delegate void LogMethod(string tag, string message, string file, string func, int line);

        private readonly UdpLoggerService _loggerService;
        public UdpLogger(string channel, LogLevel level, UdpLoggerService service) : base(channel, level)
        {
            _loggerService = service;
        }

        public override void PrintLog(LogLevel level, string message, string file, string method, int line)
        {
            // probably better to send nth tuple of data then to construct new string...
            var fileSpan = file.AsSpan();
            var fnameStart = fileSpan.LastIndexOfAny('/','\\')+1; //Handles found & not found cases (-1)
     
            _loggerService.Log($"{level}/{Channel}: {fileSpan.Slice(fnameStart).ToString()} > {message}");
        }
    }
}
