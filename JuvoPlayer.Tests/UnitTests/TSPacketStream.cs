using System;
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
    /// does not do the trick - Static methods do not seem to be replacable by Unit Tests
    /// </summary>
    public class TestDrmSession : IDrmSession
    {
        private int Counter;
        ref int IReferenceCoutable.Count => ref Counter;

        public void Dispose() { }

        public Task Initialize() { return Task.CompletedTask; }

        public Task<Packet> DecryptPacket(EncryptedPacket packet) { return Task.FromResult(new Packet()); }
    }

    [TestFixture]
    public class TSPacketStream
    {

        protected virtual IPacketStream CreatePacketStream(StreamType streamType)
        {
            var playerStub = Substitute.For<IPlayer>();
            var drmManagerStub = Substitute.For<IDrmManager>();
            var codecExtraDataHandlerStub = Substitute.For<ICodecExtraDataHandler>();
            return CreatePacketStream(streamType, playerStub, drmManagerStub, codecExtraDataHandlerStub);
        }

        protected virtual IPacketStream CreatePacketStream(StreamType streamType, IPlayer player, IDrmManager drmManager, ICodecExtraDataHandler codecExtraDataHandler)
        {
            return new PacketStream(streamType, player, drmManager, codecExtraDataHandler);
        }

        [Test]
        public void OnAppendPacket_WhenNotConfigured_ThrowsInvalidOperationException()
        {
            using (var stream = CreatePacketStream(StreamType.Audio))
            {
                var packet = new Packet();
                Assert.Throws<InvalidOperationException>(() => stream.OnAppendPacket(packet));
            }
        }

        [Test]
        public void OnAppendPacket_WhenPacketTypeIsInvalid_ThrowsArgumentException()
        {
            using (var stream = CreatePacketStream(StreamType.Audio))
            {
                var packet = new Packet { StreamType = StreamType.Video };
                Assert.Throws<ArgumentException>(() => stream.OnAppendPacket(packet));
            }
        }

        [Test]
        public void OnAppendPacket_WhenConfigured_CallsPlayerAdapter()
        {
            var codecExtraDataHandlerStub = Substitute.For<ICodecExtraDataHandler>();
            var drmManagerStub = Substitute.For<IDrmManager>();
            var playerMock = Substitute.For<IPlayer>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerMock, drmManagerStub, codecExtraDataHandlerStub))
            {
                var packet = new Packet { StreamType = StreamType.Audio };
                var config = new AudioStreamConfig();

                stream.OnStreamConfigChanged(config);
                stream.OnAppendPacket(packet);

                playerMock.Received().AppendPacket(Arg.Any<Packet>());
            }
        }

        [Test]
        public void OnAppendPacket_WhenConfigured_CallsContextExtraDataHandler()
        {
            var codecExtraDataHandlerStub = Substitute.For<ICodecExtraDataHandler>();
            var drmManagerStub = Substitute.For<IDrmManager>();
            var playerMock = Substitute.For<IPlayer>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerMock, drmManagerStub, codecExtraDataHandlerStub))
            {
                var packet = new Packet { StreamType = StreamType.Audio };
                var config = new AudioStreamConfig();

                stream.OnStreamConfigChanged(config);
                stream.OnAppendPacket(packet);

                codecExtraDataHandlerStub.Received().OnAppendPacket(Arg.Any<Packet>());
            }
        }

        [Test]
        public void OnAppendPacket_WhenDrmSessionIsConfigured_CallsPlayerAdapter()
        {
            var codecExtraDataHandlerStub = Substitute.For<ICodecExtraDataHandler>();

            var drmSessionStub = CreateDrmSessionFake();

            var drmManagerStub = CreateDrmManagerFake(drmSessionStub);

            var playerMock = Substitute.For<IPlayer>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerMock, drmManagerStub, codecExtraDataHandlerStub))
            {
                var packet = new EncryptedPacket() { StreamType = StreamType.Audio };
                var config = new AudioStreamConfig();
                var drmInitData = new DRMInitData();

                stream.OnStreamConfigChanged(config);
                stream.OnDRMFound(drmInitData);
                stream.OnAppendPacket(packet);

                playerMock.Received().AppendPacket(Arg.Any<Packet>());

            }
        }

        [Test]
        public void OnStreamConfigChanged_WhenStreamConfigIsUnsupported_ThrowsArgumentException()
        {
            using (var stream = CreatePacketStream(StreamType.Audio))
            {
                var videoConfig = new VideoStreamConfig();
                Assert.Throws<ArgumentException>(() => stream.OnStreamConfigChanged(videoConfig));
            }
        }

        [Test]
        public void OnStreamConfigChanged_WhenStreamConfigIsNull_ThrowsArgumentNullExceptionn()
        {
            using (var stream = CreatePacketStream(StreamType.Audio))
            {
                Assert.Throws<ArgumentNullException>(() => stream.OnStreamConfigChanged(null));
            }
        }

        [Test]
        public void OnStreamConfigChanged_WhenStreamConfigIsValid_CallsContextExtraDataHandler()
        {
            var codecExtraDataHandlerStub = Substitute.For<ICodecExtraDataHandler>();
            var drmManagerStub = Substitute.For<IDrmManager>();
            var playerMock = Substitute.For<IPlayer>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerMock, drmManagerStub, codecExtraDataHandlerStub))
            {
                var packet = new Packet { StreamType = StreamType.Audio };
                var config = new AudioStreamConfig();

                stream.OnStreamConfigChanged(config);

                codecExtraDataHandlerStub.Received().OnStreamConfigChanged(Arg.Any<StreamConfig>());
            }
        }

        private static IDrmManager CreateDrmManagerFake(IDrmSession drmSessionStub)
        {
            var drmManagerStub = Substitute.For<IDrmManager>();
            drmManagerStub.CreateDRMSession(Arg.Any<DRMInitData>()).Returns(drmSessionStub);
            return drmManagerStub;
        }

        private static IDrmSession CreateDrmSessionFake()
        {
            var drmSessionFake = new TestDrmSession();
            
            return drmSessionFake;
        }
    }
}
