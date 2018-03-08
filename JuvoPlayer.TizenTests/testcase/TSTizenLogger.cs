using JuvoLogger;
using JuvoLogger.Tizen;
using NUnit.Framework;

namespace JuvoPlayer.Tests
{
    [TestFixture]
    public class TSTizenLogger
    {
        [Test]
        public void TestTizenLogger()
        {
            var dummyChannel = "TizenLog";
            var logger = new TizenLogger(dummyChannel, LogLevel.Verbose);
            var loggerClient = new LoggerClient(logger);
            loggerClient.Func();
        }
    }
}
