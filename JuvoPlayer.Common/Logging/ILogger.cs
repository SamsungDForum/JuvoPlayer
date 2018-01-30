using System.Runtime.CompilerServices;

namespace JuvoPlayer.Common.Logging
{
    public interface ILogger
    {
        void Verbose(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Debug(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Info(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Warn(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Error(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        void Fatal(string message, [CallerFilePath] string file = "", [CallerMemberName] string method = "",
            [CallerLineNumber] int line = 0);

        bool IsLevelEnabled(LogLevel level);
    }
}
