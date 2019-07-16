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
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    public class TSCencSession
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        /// <summary>
        /// PlayReady has a limit of ~15 concurrent session. Higher value will cause initialization
        /// failures
        /// </summary>
        private static readonly int ConcurrentDrmSessionsLimit = 15;
        private static readonly string[] DrmTypes = { "Widevine", "PlayReady" };
        private delegate DRMDescription DrmDescriptionDelegate();
        private delegate DRMInitData DrmInitDataDelegate();

        private class DrmConfiguration
        {
            public DrmDescriptionDelegate GetDrmDescription;
            public DrmInitDataDelegate GetDrmInitData;
            public EncryptedPacket EncryptedDataPacket;
        }

        private static readonly Dictionary<string, DrmConfiguration> DrmConfigurations = new Dictionary<string, DrmConfiguration>();

        private static byte[] initPlayReadyData;
        private static byte[] initWidevineData;

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
        private EncryptedPacket encryptedPlayReadyPacket;
        private EncryptedPacket encryptedWidevinePacket;

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
                initPlayReadyData = reader.ReadBytes((int)drmInitDataStream.Length);
            }

            drmInitDataStream = assembly.GetManifestResourceStream("JuvoPlayer.TizenTests.res.drm.tos4kuhd_dash_widevine_encrypted_init_data");
            using (var reader = new BinaryReader(drmInitDataStream))
            {
                initWidevineData = reader.ReadBytes((int)drmInitDataStream.Length);
            }

            Assert.That(initPlayReadyData, Is.Not.Null);
            Assert.That(initWidevineData, Is.Not.Null);

            // Load PlayReady packet
            var encryptedPacketStream =
                assembly.GetManifestResourceStream(
                    "JuvoPlayer.TizenTests.res.drm.google_dash_encrypted_video_packet_pts_10_01.xml");
            var packetSerializer = new XmlSerializer(typeof(EncryptedPacket));
            encryptedPlayReadyPacket = (EncryptedPacket)packetSerializer.Deserialize(encryptedPacketStream);
            var storageSerializer = new XmlSerializer(typeof(ManagedDataStorage));
            var encryptedPacketStorageStream =
                assembly.GetManifestResourceStream(
                    "JuvoPlayer.TizenTests.res.drm.google_dash_encrypted_video_packet_pts_10_01_storage.xml");
            encryptedPlayReadyPacket.Storage = (IDataStorage)storageSerializer.Deserialize(encryptedPacketStorageStream);

            DrmConfigurations.Add(DrmTypes[1], new DrmConfiguration
            {
                GetDrmDescription = CreatePlayReadyDrmDescription,
                GetDrmInitData = CreatePlayReadyDrmInitData,
                EncryptedDataPacket = encryptedPlayReadyPacket
            });

            // Load widevine packet
            encryptedPacketStream = assembly.GetManifestResourceStream(
                "JuvoPlayer.TizenTests.res.drm.tos4kuhd_dash_widevine_encrypted_video_packet.xml");
            packetSerializer = new XmlSerializer(typeof(EncryptedPacket));
            encryptedWidevinePacket = (EncryptedPacket)packetSerializer.Deserialize(encryptedPacketStream);
            storageSerializer = new XmlSerializer(typeof(ManagedDataStorage));
            encryptedPacketStorageStream =
                assembly.GetManifestResourceStream(
                    "JuvoPlayer.TizenTests.res.drm.tos4kuhd_dash_widevine_encrypted_video_packet_storage.xml");
            encryptedWidevinePacket.Storage = (IDataStorage)storageSerializer.Deserialize(encryptedPacketStorageStream);

            DrmConfigurations.Add(DrmTypes[0], new DrmConfiguration
            {
                GetDrmDescription = CreateWidevineDrmDescription,
                GetDrmInitData = CreateWidevineDrmInitData,
                EncryptedDataPacket = encryptedWidevinePacket
            });

            foreach (var drm in DrmTypes)
            {
                Assert.That(DrmConfigurations[drm].EncryptedDataPacket, Is.Not.Null);
            }
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

        private static DRMDescription CreatePlayReadyDrmDescription()
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

        private static DRMInitData CreatePlayReadyDrmInitData()
        {
            var drmInitData = new DRMInitData()
            {
                InitData = initPlayReadyData,
                SystemId = PlayreadySystemId,
                StreamType = StreamType.Video,
            };
            return drmInitData;
        }

        private static DRMInitData CreateWidevineDrmInitData()
        {
            var drmInitData = new DRMInitData()
            {
                InitData = initWidevineData,
                SystemId = WidevineSystemId,
                StreamType = StreamType.Video,
            };
            return drmInitData;
        }

        private DRMInitData CreateDrmInitData(string drmName) =>
            DrmConfigurations[drmName].GetDrmInitData();

        private DRMDescription CreateDrmDescription(string drmName) =>
            DrmConfigurations[drmName].GetDrmDescription();

        private EncryptedPacket CreateEncryptedPacket(string drmName) =>
            DrmConfigurations[drmName].EncryptedDataPacket;

        [Test, TestCaseSource(nameof(DrmTypes))]
        public void Constructor_WhenLicenceUrlIsNull_ThrowsNullReferenceException(string drmName)
        {
            Logger.Info(drmName);

            var drmInitData = CreateDrmInitData(drmName);
            var configuration = CreateDrmDescription(drmName);
            configuration.LicenceUrl = null;

            Assert.Throws<NullReferenceException>(() => CencSession.Create(drmInitData, configuration));
        }

        [Test, TestCaseSource(nameof(DrmTypes))]
        public void Initialize_WhenInitDataIsInvalid_ThrowsDRMException(string drmName)
        {
            Logger.Info(drmName);

            var drmInitData = CreateDrmInitData(drmName);
            var configuration = CreateDrmDescription(drmName);
            drmInitData.InitData = null;
            Assert.ThrowsAsync<DrmException>(async () =>
            {
                using (var drmSession = CencSession.Create(drmInitData, configuration))
                    await drmSession.Initialize();
            });

            drmInitData.InitData = initPlayReadyData.Take(initPlayReadyData.Length / 2).ToArray();
            Assert.ThrowsAsync<DrmException>(async () =>
            {
                using (var drmSession = CencSession.Create(drmInitData, configuration))
                    await drmSession.Initialize();
            });
        }

        [Test, TestCaseSource(nameof(DrmTypes))]
        public void Dispose_WhenInitializationInProgress_DoesNotThrow(string drmName)
        {
            Logger.Info(drmName);

            var drmInitData = CreateDrmInitData(drmName);
            var configuration = CreateDrmDescription(drmName);
            Assert.DoesNotThrow(() =>
            {
                var drmSession = CencSession.Create(drmInitData, configuration);
                drmSession.Initialize();
                drmSession.Dispose();
            });
        }

        [Test, TestCaseSource(nameof(DrmTypes))]
        public void Concurrent_CreateDispose_DoesNotThrow(string drmName)
        {
            Assert.DoesNotThrowAsync(async () =>
           {
               using (var goDispose = new Barrier(0))
               {
                   var id = 0;

                   async Task DoWork(string drm)
                   {

                       var drmInitData = CreateDrmInitData(drm);
                       var configuration = CreateDrmDescription(drm);

                       var myId = Interlocked.Increment(ref id);
                       var signaled = false;

                       try
                       {
                           Logger.Info($"Creating {drm} Session {myId}");
                           using (var ses = CencSession.Create(drmInitData, configuration))
                           {
                               Logger.Info($"Initializing {drm} Session {myId}");
                               ses.Initialize();

                               // Widevine cases - 15 concurrent sessions - individual session may take 60s+
                               // to initialize when run @15 sessions.
                               using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
                               {
                                   try
                                   {
                                       await ses.WaitForInitialization(cts.Token);
                                   }
                                   catch (OperationCanceledException)
                                   {
                                       Logger.Info($"TIMEOUT {drm} Session {myId}");
                                       throw;
                                   }
                                   catch (Exception e)
                                   {
                                       Logger.Error(e);
                                       throw;
                                   }
                               }

                               Logger.Info($"{drm} Session {myId} Initialized. Waiting for Dispose");
                               goDispose.SignalAndWait();
                               signaled = true;
                           }

                           Logger.Info($"{drm} Session {myId} completed");
                       }
                       finally
                       {
                           if (!signaled)
                               goDispose.RemoveParticipant();
                       }
                   }

                   var workPool = new List<Task>();

                   for (var i = 0; i < ConcurrentDrmSessionsLimit; i++)
                   {
                       workPool.Add(DoWork(drmName));
                       goDispose.AddParticipant();
                   }

                   await Task.WhenAll(workPool.ToArray());
                   Logger.Info($"{drmName} Test completed");
               }
           });
        }

        [Test, TestCaseSource(nameof(DrmTypes))]
        public async Task DecryptPacket_WhenPacketIsValid_DecryptsSuccessfully(string drmName)
        {
            Logger.Info(drmName);

            var drmInitData = CreateDrmInitData(drmName);
            var configuration = CreateDrmDescription(drmName);
            var encryptedPacket = CreateEncryptedPacket(drmName);

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