using JuvoLogger;
using JuvoLogger.Tizen;
using JuvoPlayer.Tests;
using JuvoPlayer.Tests.UnitTests;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
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
