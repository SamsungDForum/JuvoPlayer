using JuvoLogger;

namespace JuvoPlayer.Tests.UnitTests
{
    // This class is used to check logging properties, like function names and line numbers.
    // Test cases assume that each logging statement is done in fixed lline.
    //
    // Please modify correspoding test cases if you would like to modify this file.
    public class LoggerClient
    {
        public ILogger Logger { get; }
        public static readonly string LogMessage = "message";
        public static readonly string FileName = "LoggerClient.cs";
        public static readonly int FuncFirstLineNumber = 23;

        public LoggerClient(ILogger logger)
        {
            this.Logger = logger;
        }

        public void Func()
        {
            Logger.Fatal(LogMessage);
            Logger.Error(LogMessage);
            Logger.Warn(LogMessage);
            Logger.Info(LogMessage);
            Logger.Debug(LogMessage);
            Logger.Verbose(LogMessage);
        }
    }
}
