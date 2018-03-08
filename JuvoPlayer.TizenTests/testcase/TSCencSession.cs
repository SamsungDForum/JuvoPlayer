using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.Drms;
using JuvoPlayer.Drms.Cenc;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests
{
    [TestFixture]
    public class TSCencSession
    {
        private byte[] initData;

        private List<EncryptedPacket> packets = new List<EncryptedPacket>();

        private LoggerManager savedLoggerManager;
        private static byte[] PlayreadySystemId = new byte[] { 0x9a, 0x04, 0xf0, 0x79, 0x98, 0x40, 0x42, 0x86, 0xab, 0x92, 0xe6, 0x5b, 0xe0, 0x88, 0x5f, 0x95 };

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            savedLoggerManager = LoggerManager.ResetForTests();
            LoggerManager.Configure("JuvoPlayer=Verbose", CreateLoggerFunc);

            var assembly = Assembly.GetExecutingAssembly();
            var drmInitDataStream = assembly.GetManifestResourceStream("JuvoPlayer.TizenTests.res.drm.init_data");
            using (var reader = new BinaryReader(drmInitDataStream))
            {
                initData = reader.ReadBytes((int)drmInitDataStream.Length);
            }
            Assert.That(initData, Is.Not.Null);

            var encryptedPacketsStream = assembly.GetManifestResourceStream("JuvoPlayer.TizenTests.res.drm.encrypted_packets_list.bin");
            BinaryFormatter formatter = new BinaryFormatter();
            packets = (List<EncryptedPacket>) formatter.Deserialize(encryptedPacketsStream);

            Assert.That(packets, Is.Not.Null);
            Assert.That(packets.Count, Is.GreaterThan(0));
        }

        private static LoggerBase CreateLoggerFunc(string channel, LogLevel level)
        {
            return new ConsoleLogger(channel, level);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            LoggerManager.RestoreForTests(savedLoggerManager);
        }

        private static DRMDescription CreateDrmDescription()
        {
            var licenceUrl = "http://dash-mse-test.appspot.com/api/drm/playready?drm_system=playready&source=YOUTUBE&video_id=03681262dc412c06&ip=0.0.0.0&ipbits=0&expire=19000000000&sparams=ip,ipbits,expire,drm_system,source,video_id&signature=3BB038322E72D0B027F7233A733CD67D518AF675.2B7C39053DA46498D23F3BCB87596EF8FD8B1669&key=test_key1";
            var configuration = new DRMDescription()
            {
                Scheme = CencUtils.GetScheme(PlayreadySystemId),
                LicenceUrl = licenceUrl,
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
        public void Constructor_WhenLicenceUrlIsNull_ThrowsNullReferenceException()
        {
            var drmInitData = CreateDrmInitData();
            var configuration = CreateDrmDescription();
            configuration.LicenceUrl = null;

            Assert.Throws<NullReferenceException>(() => CencSession.Create(drmInitData, configuration));
        }

        [Test]
        public void Initialize_WhenInitDataIsInvalid_ThrowsDRMException()
        {
            var drmInitData = CreateDrmInitData();
            var configuration = CreateDrmDescription();
            drmInitData.InitData = null;
            Assert.ThrowsAsync<DrmException>(async () =>
            {
                using (var drmSession = CencSession.Create(drmInitData, configuration))
                    await drmSession.Initialize();
            });

            drmInitData.InitData = initData.Take(initData.Length / 2).ToArray();
            Assert.ThrowsAsync<DrmException>(async () =>
            {
                using (var drmSession = CencSession.Create(drmInitData, configuration))
                    await drmSession.Initialize();
            });
        }

        [Test]
        public async Task DecryptPacket_WhenPacketIsValid_DecryptsSuccessfully()
        {
            var drmInitData = CreateDrmInitData();
            var configuration = CreateDrmDescription();

            using (var drmSession = CencSession.Create(drmInitData, configuration))
            {
                await drmSession.Initialize();

                foreach (var encrypted in packets)
                {
                    using (var decrypted = await drmSession.DecryptPacket(encrypted))
                    {
                        Assert.That(decrypted, Is.Not.Null);
                        Assert.That(decrypted, Is.InstanceOf<DecryptedEMEPacket>());

                        var decryptedEme = decrypted as DecryptedEMEPacket;

                        Assert.That(decryptedEme.Dts, Is.EqualTo(encrypted.Dts));
                        Assert.That(decryptedEme.Pts, Is.EqualTo(encrypted.Pts));
                        Assert.That(decryptedEme.IsEOS, Is.EqualTo(encrypted.IsEOS));
                        Assert.That(decryptedEme.IsKeyFrame, Is.EqualTo(encrypted.IsKeyFrame));
                        Assert.That(decryptedEme.StreamType, Is.EqualTo(encrypted.StreamType));
                        Assert.That(decryptedEme.HandleSize, Is.Not.Null);
                        Assert.That(decryptedEme.HandleSize.handle, Is.GreaterThan(0));
                        Assert.That(decryptedEme.HandleSize.size, Is.EqualTo(encrypted.Data.Length));
                    }
                }
            }
        }
    }
}
