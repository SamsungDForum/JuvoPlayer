using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Logging;
using JuvoPlayer.DRM;
using JuvoPlayer.DRM.Cenc;
using JuvoPlayer.Logging;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests
{
    [TestFixture]
    public class TSDRMSession
    {
        private static readonly byte[] PlayreadySystemId = { 0x9a, 0x04, 0xf0, 0x79, 0x98, 0x40, 0x42, 0x86, 0xab, 0x92, 0xe6, 0x5b, 0xe0, 0x88, 0x5f, 0x95 };

        private byte[] initData;

        private static readonly string LicenceUrl =
            "http://dash-mse-test.appspot.com/api/drm/playready?drm_system=playready&source=YOUTUBE&video_id=03681262dc412c06&ip=0.0.0.0&ipbits=0&expire=19000000000&sparams=ip,ipbits,expire,drm_system,source,video_id&signature=3BB038322E72D0B027F7233A733CD67D518AF675.2B7C39053DA46498D23F3BCB87596EF8FD8B1669&key=test_key1";

        private LoggerManager savedLoggerManager;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            savedLoggerManager = LoggerManager.ResetForTests();
            LoggerManager.Configure("JuvoPlayer=Verbose", CreateLoggerFunc);
        }

        [SetUp]
        public void SetUp()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream("JuvoPlayer.TizenTests.res.drm.init_data");
            using (var reader = new BinaryReader(stream))
            {
                initData = reader.ReadBytes((int) stream.Length);
            }
            Assert.That(initData, Is.Not.Null);
        }

        private static LoggerBase CreateLoggerFunc(string channel, LogLevel level)
        {
            return new TizenLogger(channel, level);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            LoggerManager.RestoreForTests(savedLoggerManager);
        }

        private static DRMDescription CreateDrmDescription()
        {
            var configuration = new DRMDescription()
            {
                Scheme = CencUtils.GetScheme(PlayreadySystemId),
                LicenceUrl = LicenceUrl,
                KeyRequestProperties = new Dictionary<string, string>() {{"Content-Type", "text/xml; charset=utf-8"}},
            };
            return configuration;
        }

        private DRMInitData CreateDrmInitData()
        {
            var drmInitData = new DRMInitData()
            {
                InitData = initData,
                SystemId = PlayreadySystemId,
                StreamType = StreamType.Video,
            };
            return drmInitData;
        }

        [Test]
        public async Task TestConfiguration()
        {
            var drmInitData = CreateDrmInitData();
            var configuration = CreateDrmDescription();

            using (var drmSession = CencSession.Create(drmInitData, configuration))
            {
                var result = await drmSession.StartLicenceChallenge();
                Assert.That(result, Is.EqualTo(ErrorCode.Success));
            }
        }

        [Test]
        public async Task TestInvalidLicenceUrl()
        {
            var drmInitData = CreateDrmInitData();
            var configuration = CreateDrmDescription();
            configuration.LicenceUrl = null;
            using (var drmSession = CencSession.Create(drmInitData, configuration))
            {
                var result = await drmSession.StartLicenceChallenge();
                Assert.That(result, Is.EqualTo(ErrorCode.InvalidArgument));
            }
        }

        [Test]
        public async Task TestInvalidInitData()
        {
            var drmInitData = CreateDrmInitData();
            var configuration = CreateDrmDescription();
            drmInitData.InitData = null;
            using (var drmSession = CencSession.Create(drmInitData, configuration))
            {
                var result = await drmSession.StartLicenceChallenge();
                Assert.That(result, Is.EqualTo(ErrorCode.InvalidArgument));
            }

            drmInitData.InitData = initData.Take(initData.Length / 2).ToArray();
            using (var drmSession = CencSession.Create(drmInitData, configuration))
            {
                var result = await drmSession.StartLicenceChallenge();
                Assert.That(result, Is.EqualTo(ErrorCode.Generic));
            }
        }
    }
}
