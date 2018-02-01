using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JuvoPlayer.Common.Logging;

namespace JuvoPlayer.Logging
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
                    tizenLog = Tizen.Log.Verbose;
                    break;
                case LogLevel.Debug:
                    tizenLog = Tizen.Log.Debug;
                    break;
                case LogLevel.Info:
                    tizenLog = Tizen.Log.Info;
                    break;
                case LogLevel.Warn:
                    tizenLog = Tizen.Log.Warn;
                    break;
                case LogLevel.Error:
                    tizenLog = Tizen.Log.Error;
                    break;
                case LogLevel.Fatal:
                    tizenLog = Tizen.Log.Fatal;
                    break;
                default:
                    tizenLog = Tizen.Log.Error;
                    break;
            }

            tizenLog(Channel, message, file, method, line);
        }
    }
}
