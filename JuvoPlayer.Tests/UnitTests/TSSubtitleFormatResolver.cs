using JuvoPlayer.Common;
using JuvoPlayer.Subtitles;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSSubtitleFormatResolver
    {
        [Test]
        public void ResolveByPath_SrtPath_ReturnSrtFormat()
        {
            var subtitleFormatResolver = new SubtitleFormatResolver();

            var format = subtitleFormatResolver.ResolveByPath("subtitles.srt");

            Assert.That(format, Is.EqualTo(SubtitleFormat.Subrip));
        }

        [Test]
        public void ResolveByPath_InvalidPath_ReturnInvalidFormat()
        {
            var subtitleFormatResolver = new SubtitleFormatResolver();

            var format = subtitleFormatResolver.ResolveByPath("subtitles.mp4");

            Assert.That(format, Is.EqualTo(SubtitleFormat.Invalid));
        }

        [Test]
        public void ResolveByMimeType_WebVttMimeType_ReturnsWebVttFormat()
        {
            var subtitleFormatResolver = new SubtitleFormatResolver();

            var format = subtitleFormatResolver.ResolveByMimeType("text/vtt");

            Assert.That(format, Is.EqualTo(SubtitleFormat.WebVtt));
        }

        [Test]
        public void ResolveByMimeType_InvalidMimeType_ReturnsInvalidFormat()
        {
            var subtitleFormatResolver = new SubtitleFormatResolver();

            var format = subtitleFormatResolver.ResolveByMimeType("video/mp4");

            Assert.That(format, Is.EqualTo(SubtitleFormat.Invalid));
        }

        [Test]
        public void Resolve_SrtSubtitleInfo_ReturnsSrtFormat()
        {
            var subtitleFormatResolver = new SubtitleFormatResolver();
            var subtitleInfo = new SubtitleInfo()
            {
                Path = "subtitles.srt"
            };

            var format = subtitleFormatResolver.Resolve(subtitleInfo);

            Assert.That(format, Is.EqualTo(SubtitleFormat.Subrip));
        }
    }
}
