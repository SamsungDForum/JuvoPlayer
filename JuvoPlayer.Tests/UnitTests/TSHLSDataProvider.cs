using System;
using JuvoPlayer.Common;
using JuvoPlayer.DataProviders.HLS;
using JuvoPlayer.Demuxers;
using NSubstitute;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSHLSDataProvider
    {

        [Test]
        public void GetStreamsDescription_ClipWithNoSubtitles_ReturnsEmptyList()
        {
            var demuxerMock = Substitute.For<IDemuxer>();
            var clipDefinition = new ClipDefinition();
            var dataProvider = new HLSDataProvider(demuxerMock, clipDefinition);

            var streamsDescription = dataProvider.GetStreamsDescription(StreamType.Subtitle);

            Assert.That(streamsDescription, Is.Empty);
        }

        [Test]
        public void OnChangeActiveStream_UnknownSubtitles_ThrowsArgumentException()
        {
            var demuxerMock = Substitute.For<IDemuxer>();
            var clipDefinition = new ClipDefinition();
            var dataProvider = new HLSDataProvider(demuxerMock, clipDefinition);
            var streamDescription = new StreamDescription
            {
                StreamType = StreamType.Subtitle
            };

            Assert.Throws<ArgumentException>(() => dataProvider.OnChangeActiveStream(streamDescription));
        }
    }
}
