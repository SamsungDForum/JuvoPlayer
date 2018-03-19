using System;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using JuvoPlayer.Player;
using NSubstitute;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
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
                var packet = new Packet { StreamType = StreamType.Video};
                Assert.Throws<ArgumentException>(() => stream.OnAppendPacket(packet));
            }
        }

        [Test]
        public void OnAppendPacket_WhenConfigured_CallsPlayerAdapter()
        {
            var drmManagerStub = Substitute.For<IDrmManager>();
            var playerMock = Substitute.For<IPlayer>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerMock, drmManagerStub))
            {
                var packet = new Packet { StreamType = StreamType.Audio};
                var config = new AudioStreamConfig();

                stream.OnStreamConfigChanged(config);
                stream.OnAppendPacket(packet);

                playerMock.Received().AppendPacket(Arg.Any<Packet>());
            }
        }

        [Test]
        public void OnAppendPacket_WhenDrmSessionIsConfigured_CallsPlayerAdapter()
        {
            var drmSessionStub = CreateDrmSessionFake();

            var drmManagerStub = CreateDrmManagerFake(drmSessionStub);

            var playerMock = Substitute.For<IPlayer>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerMock, drmManagerStub))
            {
                var packet = new EncryptedPacket() {StreamType = StreamType.Audio};
                var config = new AudioStreamConfig();
                var drmInitData = new DRMInitData();

                stream.OnStreamConfigChanged(config);
                stream.OnDRMFound(drmInitData);
                stream.OnAppendPacket(packet);

                playerMock.Received().AppendPacket(Arg.Any<Packet>());
            }
        }

        [Test]
        public void OnAppendPacket_WhenDrmSessionIsConfigured_CallsDrmSession()
        {
            var drmSessionMock = CreateDrmSessionFake();

            var drmManagerStub = CreateDrmManagerFake(drmSessionMock);

            var playerStub = Substitute.For<IPlayer>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerStub, drmManagerStub))
            {
                var packet = new EncryptedPacket() { StreamType = StreamType.Audio };
                var config = new AudioStreamConfig();
                var drmInitData = new DRMInitData();

                stream.OnStreamConfigChanged(config);
                stream.OnDRMFound(drmInitData);
                stream.OnAppendPacket(packet);

                drmSessionMock.Received().DecryptPacket(Arg.Any<EncryptedPacket>());
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

        private static IDrmManager CreateDrmManagerFake(IDrmSession drmSessionStub)
        {
            var drmManagerStub = Substitute.For<IDrmManager>();
            drmManagerStub.CreateDRMSession(Arg.Any<DRMInitData>()).Returns(drmSessionStub);
            return drmManagerStub;
        }

        private static IDrmSession CreateDrmSessionFake()
        {
            var drmSessionFake = Substitute.For<IDrmSession>();
            drmSessionFake.Initialize().Returns(Task.CompletedTask);
            drmSessionFake.DecryptPacket(Arg.Any<EncryptedPacket>()).Returns(Task.FromResult(new Packet()));
            return drmSessionFake;
        }
    }
}
