using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.DRM;
using JuvoPlayer.Player;
using NSubstitute;
using NSubstitute.Core;
using NUnit.Framework;

namespace JuvoPlayer.Tests
{
    [TestFixture]
    public class TSPacketStream
    {

        protected virtual IPacketStream CreatePacketStream(StreamType streamType)
        {
            var playerStub = Substitute.For<IPlayerAdapter>();
            var drmManagerStub = Substitute.For<IDRMManager>();
            return CreatePacketStream(streamType, playerStub, drmManagerStub);
        }

        protected virtual IPacketStream CreatePacketStream(StreamType streamType, IPlayerAdapter player, IDRMManager drmManager)
        {
            return new PacketStream(streamType, player, drmManager);
        }

        [Test]
        public void OnAppendPacket_WhenNotConfigured_ThrowsInvalidOperationException()
        {
            using (var stream = CreatePacketStream(StreamType.Audio))
            {
                var packet = new StreamPacket();
                Assert.Throws<InvalidOperationException>(() => stream.OnAppendPacket(packet));
            }
        }

        [Test]
        public void OnAppendPacket_WhenPacketTypeIsInvalid_ThrowsArgumentException()
        {
            using (var stream = CreatePacketStream(StreamType.Audio))
            {
                var packet = new StreamPacket {StreamType = StreamType.Video};
                Assert.Throws<ArgumentException>(() => stream.OnAppendPacket(packet));
            }
        }

        [Test]
        public void OnAppendPacket_WhenConfigured_CallsPlayerAdapter()
        {
            var drmManagerStub = Substitute.For<IDRMManager>();
            var playerMock = Substitute.For<IPlayerAdapter>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerMock, drmManagerStub))
            {
                var packet = new StreamPacket {StreamType = StreamType.Audio};
                var config = new AudioStreamConfig();

                stream.OnStreamConfigChanged(config);
                stream.OnAppendPacket(packet);

                playerMock.Received().AppendPacket(Arg.Any<StreamPacket>());
            }
        }

        [Test]
        public void OnAppendPacket_WhenDrmSessionIsConfigured_CallsPlayerAdapter()
        {
            var drmSessionStub = CreateDrmSessionFake();

            var drmManagerStub = CreateDrmManagerFake(drmSessionStub);

            var playerMock = Substitute.For<IPlayerAdapter>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerMock, drmManagerStub))
            {
                var packet = new EncryptedStreamPacket() {StreamType = StreamType.Audio};
                var config = new AudioStreamConfig();
                var drmInitData = new DRMInitData();

                stream.OnStreamConfigChanged(config);
                stream.OnDRMFound(drmInitData);
                stream.OnAppendPacket(packet);

                playerMock.Received().AppendPacket(Arg.Any<StreamPacket>());
            }
        }

        [Test]
        public void OnAppendPacket_WhenDrmSessionIsConfigured_CallsDrmSession()
        {
            var drmSessionMock = CreateDrmSessionFake();

            var drmManagerStub = CreateDrmManagerFake(drmSessionMock);

            var playerStub = Substitute.For<IPlayerAdapter>();

            using (var stream = CreatePacketStream(StreamType.Audio, playerStub, drmManagerStub))
            {
                var packet = new EncryptedStreamPacket() { StreamType = StreamType.Audio };
                var config = new AudioStreamConfig();
                var drmInitData = new DRMInitData();

                stream.OnStreamConfigChanged(config);
                stream.OnDRMFound(drmInitData);
                stream.OnAppendPacket(packet);

                drmSessionMock.Received().DecryptPacket(Arg.Any<EncryptedStreamPacket>());
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

        private static IDRMManager CreateDrmManagerFake(IDRMSession drmSessionStub)
        {
            var drmManagerStub = Substitute.For<IDRMManager>();
            drmManagerStub.CreateDRMSession(Arg.Any<DRMInitData>()).Returns(drmSessionStub);
            return drmManagerStub;
        }

        private static IDRMSession CreateDrmSessionFake()
        {
            var drmSessionFake = Substitute.For<IDRMSession>();
            drmSessionFake.Initialize().Returns(Task.CompletedTask);
            drmSessionFake.DecryptPacket(Arg.Any<EncryptedStreamPacket>()).Returns(Task.FromResult(new StreamPacket()));
            return drmSessionFake;
        }
    }
}
