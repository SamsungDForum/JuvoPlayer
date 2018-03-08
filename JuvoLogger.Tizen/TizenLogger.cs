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
