using JuvoPlayer.Common.Logging;
using JuvoPlayer.Logging;
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
