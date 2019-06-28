/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using JuvoPlayer.Drms.Cenc;
using JuvoPlayer.Player.EsPlayer;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    public class TSCencSession
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private byte[] initData;
        private byte[] initWidevineData;

        // This test needs one, arbitrary, video encrypted packet from Google Dash OOPS Cenc content.
        // To dump one encrypted packet add following code to JuvoPlayer.Player.PacketStream class:
        // private bool serialized;
        // ...
        // public void OnAppendPacket(Packet packet) {
        //  ...
        //  if (packet is EncryptedPacket && packet.StreamType == StreamType.Video && serialized == false)
        //  {
        //     EncryptedPacket encryptedPacket = (EncryptedPacket) packet;
        //     using (var stream = File.Create("/tmp/encrypted_packet.xml"))
        //     {
        //         new XmlSerializer(encryptedPacket.GetType()).Serialize(stream, encryptedPacket);
        //     }
        //     serialized = true;
        //  }
        //  ...
        // }
        private EncryptedPacket encryptedPacket;

        private LoggerManager savedLoggerManager;

        private static byte[] PlayreadySystemId = new byte[]
            {0x9a, 0x04, 0xf0, 0x79, 0x98, 0x40, 0x42, 0x86, 0xab, 0x92, 0xe6, 0x5b, 0xe0, 0x88, 0x5f, 0x95};

        private static byte[] WidevineSystemId = new byte[]
            {0xed, 0xef, 0x8b, 0xa9, 0x79, 0xd6, 0x4a, 0xce, 0xa3, 0xc8, 0x27, 0xdc, 0xd5, 0x1d, 0x21, 0xed };

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            savedLoggerManager = LoggerManager.ResetForTests();
            LoggerManager.Configure("JuvoPlayer=Verbose", CreateLoggerFunc);

            var assembly = Assembly.GetExecutingAssembly();
            var drmInitDataStream = assembly.GetManifestResourceStream("JuvoPlayer.TizenTests.res.drm.google_dash_encrypted_init_data");
            using (var reader = new BinaryReader(drmInitDataStream))
            {
                initData = reader.ReadBytes((int)drmInitDataStream.Length);
            }

            drmInitDataStream = assembly.GetManifestResourceStream("JuvoPlayer.TizenTests.res.drm.tos4kuhd_dash_widevine_encrypted_init_data");
            using (var reader = new BinaryReader(drmInitDataStream))
            {
                initWidevineData = reader.ReadBytes((int)drmInitDataStream.Length);
            }

            Assert.That(initData, Is.Not.Null);
            Assert.That(initWidevineData, Is.Not.Null);

            var encryptedPacketStream =
                assembly.GetManifestResourceStream(
                    "JuvoPlayer.TizenTests.res.drm.google_dash_encrypted_video_packet_pts_10_01.xml");
            var packetSerializer = new XmlSerializer(typeof(EncryptedPacket));
            encryptedPacket = (EncryptedPacket)packetSerializer.Deserialize(encryptedPacketStream);
            var storageSerializer = new XmlSerializer(typeof(ManagedDataStorage));
            var encryptedPacketStorageStream =
                assembly.GetManifestResourceStream(
                    "JuvoPlayer.TizenTests.res.drm.google_dash_encrypted_video_packet_pts_10_01_storage.xml");
            encryptedPacket.Storage = (IDataStorage)storageSerializer.Deserialize(encryptedPacketStorageStream);

            Assert.That(encryptedPacket, Is.Not.Null);
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
            var licenceUrl =
                "http://dash-mse-test.appspot.com/api/drm/playready?drm_system=playready&source=YOUTUBE&video_id=03681262dc412c06&ip=0.0.0.0&ipbits=0&expire=19000000000&sparams=ip,ipbits,expire,drm_system,source,video_id&signature=3BB038322E72D0B027F7233A733CD67D518AF675.2B7C39053DA46498D23F3BCB87596EF8FD8B1669&key=test_key1";
            var configuration = new DRMDescription()
            {
                Scheme = CencUtils.GetScheme(PlayreadySystemId),
                LicenceUrl = licenceUrl,
                KeyRequestProperties = new Dictionary<string, string>() { { "Content-Type", "text/xml; charset=utf-8" } },
            };
            return configuration;
        }

        private static DRMDescription CreateWidevineDrmDescription()
        {
            var licenceUrl = "https://proxy.uat.widevine.com/proxy?provider=widevine_test";
            var configuration = new DRMDescription()
            {
                Scheme = CencUtils.GetScheme(WidevineSystemId),
                LicenceUrl = licenceUrl,
                KeyRequestProperties = new Dictionary<string, string>() { { "Content-Type", "text/xml; charset=utf-8" } },
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

        private DRMInitData CreateWidevineDrmInitData()
        {
            var drmInitData = new DRMInitData()
            {
                InitData = initWidevineData,
                SystemId = WidevineSystemId,
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
        public void Dispose_WhenInitializationInProgress_DoesNotThrow()
        {
            var drmInitData = CreateDrmInitData();
            var configuration = CreateDrmDescription();
            Assert.DoesNotThrow(() =>
            {
                var drmSession = CencSession.Create(drmInitData, configuration);
                drmSession.Initialize();
                drmSession.Dispose();
            });
        }

        private async Task Disposer(CencSessionHolder ses, int delay)
        {
            using (var cts = new CancellationTokenSource(delay))
            {
                try
                {
                    await ses.initTask.WithCancellation(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!cts.Token.IsCancellationRequested)
                        throw;
                }
            }

            ses.session.Dispose();
            Logger.Info($"Session #{ses.id} Disposed");
        }

        private struct CencSessionHolder
        {
            public CencSession session;
            public int id;
            public Task initTask;
        }

        [Test]
        public void MultiThreaded_CreateDispose_DoesNotThrow()
        {
            Assert.DoesNotThrowAsync(async () =>
            {
                DRMInitData[] initData = new DRMInitData[2];
                DRMDescription[] configData = new DRMDescription[2];
                initData[0] = CreateWidevineDrmInitData();
                initData[1] = CreateDrmInitData();
                configData[0] = CreateWidevineDrmDescription();
                configData[1] = CreateDrmDescription();

                int drmType = 0;

                var rnd = new Random();

                var createDisposeTries = 50;
                var createCount = 0;
                var disposeCount = 0;
                var concurrent = 0;
                var maxConcurrent = 10;

                var launchedTasks = new List<Task>();

                while (createCount < createDisposeTries)
                {
                    CencSessionHolder cencHolder = new CencSessionHolder();
                    createCount++;
                    var r = Interlocked.Add(ref concurrent, 1);
                    cencHolder.session = CencSession.Create(initData[drmType], configData[drmType]);
                    cencHolder.initTask = cencHolder.session.Initialize();
                    cencHolder.id = createCount;

                    drmType = drmType ^ 1;

                    var disposeTask = Task.Run(async () =>
                    {
                        // Delay actual termination so various phases of init process are tested.
                        await Disposer(cencHolder, rnd.Next(200, 2000));
                        Interlocked.Add(ref concurrent, -1);
                        Interlocked.Add(ref disposeCount, 1);
                    });

                    launchedTasks.Add(disposeTask);

                    Logger.Info($"Session #{cencHolder.id} Created");

                    if (r >= maxConcurrent)
                    {
                        Logger.Info($"{maxConcurrent} concurrent CencSession. Waiting...");
                        while (Volatile.Read(ref concurrent) >= maxConcurrent)
                        {
                            await Task.Delay(1000);
                        }

                        Logger.Info($"{maxConcurrent} concurrent CencSession. Waiting completed.");
                    }
                }

                Logger.Info($"Waiting completion...");

                var endt = Task.WhenAll(launchedTasks);
                await endt;

                if (endt.IsFaulted)
                    throw new Exception("Disposers terminated with fault");

                Assert.AreEqual(createCount, Volatile.Read(ref disposeCount));
                Assert.AreEqual(createCount, createDisposeTries);
                Assert.AreEqual(Volatile.Read(ref disposeCount), createDisposeTries);

                Logger.Info($"All done");


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
                using (var decrypted = await drmSession.DecryptPacket(encryptedPacket, CancellationToken.None))
                {
                    Assert.That(decrypted, Is.Not.Null);
                    Assert.That(decrypted, Is.InstanceOf<DecryptedEMEPacket>());

                    var decryptedEme = decrypted as DecryptedEMEPacket;

                    Assert.That(decryptedEme.Dts, Is.EqualTo(encryptedPacket.Dts));
                    Assert.That(decryptedEme.Pts, Is.EqualTo(encryptedPacket.Pts));
                    Assert.That(decryptedEme.IsKeyFrame, Is.EqualTo(encryptedPacket.IsKeyFrame));
                    Assert.That(decryptedEme.StreamType, Is.EqualTo(encryptedPacket.StreamType));
                    Assert.That(decryptedEme.HandleSize, Is.Not.Null);
                    Assert.That(decryptedEme.HandleSize.handle, Is.GreaterThan(0));
                    Assert.That(decryptedEme.HandleSize.size, Is.EqualTo(encryptedPacket.Storage.Length));
                }
            }
        }
    }
}