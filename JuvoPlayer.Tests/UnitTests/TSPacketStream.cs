/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Utils.IReferenceCountable;
using JuvoPlayer.Drms;
using JuvoPlayer.Player;
using NSubstitute;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    /// <summary>
    /// Dummy IDrmSession. Due to reference counting, simple interface replacement
    /// does not do the trick - Static methods do not seem to be replaceable by Unit Tests
    /// </summary>
    public class TestDrmSession : IDrmSession
    {
        private int Counter;
        ref int IReferenceCountable.Count => ref Counter;

        public bool IsInitialized()
        {
            throw new NotImplementedException();
        }

        public ICdmInstance CdmInstance { get; } = new TestCdmInstance();

        public void Dispose() { }

        public Task<bool> WaitForInitialization()
        {
            return Task.FromResult(true);
        }

        public Task<Packet> DecryptPacket(EncryptedPacket packet, CancellationToken token) { return Task.FromResult(new Packet()); }

        public Task GetInitializationTask() => Task.CompletedTask;
        public bool CanDecrypt() => true;

        public void SetSessionId(string sessionId)
        {
            throw new NotImplementedException();
        }

        public void SetLicenceInstalled()
        {
            throw new NotImplementedException();
        }

        public CancellationToken CancellationToken()
        {
            throw new NotImplementedException();
        }

        public DrmInitData GetDrmInitData()
        {
            throw new NotImplementedException();
        }

        public DrmDescription GetDrmDescription()
        {
            throw new NotImplementedException();
        }

        public string GetSessionId()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<byte[]> GetKeys()
        {
            throw new NotImplementedException();
        }
    }

    public class TestCdmInstance : ICdmInstance
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public ref int Count => throw new NotImplementedException();

        public Task<Packet> DecryptPacket(EncryptedPacket packet, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<IDrmSession> GetDrmSession(DrmInitData data, IEnumerable<byte[]> keys, List<DrmDescription> clipDrmConfigurations)
        {
            throw new NotImplementedException();
        }

        public void CloseSession(string sessionId)
        {
            throw new NotImplementedException();
        }

        public Task GenerateRequest(string sessionId, DrmInitData initData)
        {
            throw new NotImplementedException();
        }

        public Task WaitForAllSessionsInitializations(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    [TestFixture]
    public class TSPacketStream
    {

        protected virtual IPacketStream CreatePacketStream(StreamType streamType)
        {
            var playerStub = Substitute.For<IPlayer>();
            var drmManagerStub = Substitute.For<IDrmManager>();
            return CreatePacketStream(streamType, playerStub, drmManagerStub);
        }

        protected virtual IPacketStream CreatePacketStream(StreamType streamType, IPlayer player, IDrmManager drmManager)
        {
            return new PacketStream(streamType, player, drmManager);
        }

        [Test]
        [Category("Negative")]
        public void OnAppendPacket_WhenNotConfigured_ThrowsInvalidOperationException()
        {
            using (var stream = CreatePacketStream(StreamType.Audio))
            {
                var packet = new Packet();
                Assert.ThrowsAsync<InvalidOperationException>(() => stream.OnAppendPacket(packet));
            }
        }

        [Test]
        [Category("Negative")]
        public void OnAppendPacket_WhenPacketTypeIsInvalid_ThrowsArgumentException()
        {
            using (var stream = CreatePacketStream(StreamType.Audio))
            {
                var packet = new Packet { StreamType = StreamType.Video };
                Assert.ThrowsAsync<ArgumentException>(() => stream.OnAppendPacket(packet));
            }
        }

        [Test]
        [Category("Positive")]
        public void OnAppendPacket_WhenConfigured_CallsPlayerAdapter()
        {
            var drmManagerStub = Substitute.For<IDrmManager>();
            var playerMock = Substitute.For<IPlayer>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerMock, drmManagerStub))
            {
                var packet = new Packet { StreamType = StreamType.Audio };
                var config = new AudioStreamConfig();

                stream.OnStreamConfigChanged(config);
                stream.OnAppendPacket(packet);

                playerMock.Received().AppendPacket(Arg.Any<Packet>());
            }
        }

        [Test]
        [Category("Positive")]
        public void OnAppendPacket_WhenDrmSessionIsConfigured_CallsPlayerAdapter()
        {
            var drmSessionStub = CreateDrmSessionFake();

            var drmManagerStub = CreateDrmManagerFake(drmSessionStub);

            var playerMock = Substitute.For<IPlayer>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerMock, drmManagerStub))
            {
                var packet = new EncryptedPacket() { StreamType = StreamType.Audio };
                var config = new AudioStreamConfig();
                var drmInitData = new DrmInitData();

                stream.OnStreamConfigChanged(config);
                stream.OnDRMFound(drmInitData);
                stream.OnAppendPacket(packet);

                playerMock.Received().AppendPacket(Arg.Any<Packet>());

            }
        }

        [Test]
        [Category("Negative")]
        public void OnStreamConfigChanged_WhenStreamConfigIsUnsupported_ThrowsArgumentException()
        {
            using (var stream = CreatePacketStream(StreamType.Audio))
            {
                var videoConfig = new VideoStreamConfig();
                Assert.Throws<ArgumentException>(() => stream.OnStreamConfigChanged(videoConfig));
            }
        }

        [Test]
        [Category("Negative")]
        public void OnStreamConfigChanged_WhenStreamConfigIsNull_ThrowsArgumentNullException()
        {
            using (var stream = CreatePacketStream(StreamType.Audio))
            {
                Assert.Throws<ArgumentNullException>(() => stream.OnStreamConfigChanged(null));
            }
        }

        private static IDrmManager CreateDrmManagerFake(IDrmSession drmSessionStub)
        {
            var drmManagerStub = Substitute.For<IDrmManager>();
            drmManagerStub.GetDrmSession(Arg.Any<DrmInitData>()).Returns(drmSessionStub);
            return drmManagerStub;
        }

        private static IDrmSession CreateDrmSessionFake()
        {
            var drmSessionFake = new TestDrmSession();

            return drmSessionFake;
        }
    }
}
