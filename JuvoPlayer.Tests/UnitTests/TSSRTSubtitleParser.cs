using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JuvoPlayer.Subtitles;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSSRTSubtitleParser
    {
        [Test]
        public void ParseTime_WhenTimeIsValid_ParsesSuccessfully()
        {
            var parser = CreateSRTParser();

            var parsed = parser.ParseTime("00:02:17,440");

            Assert.That(parsed, Is.EqualTo(new TimeSpan(0, 0, 2, 17, 440)));
        }

        [Test]
        public void ParseTime_WhenTimeExceedesADay_ParsesSuccessfully()
        {
            var parser = CreateSRTParser();

            var parsed = parser.ParseTime("25:02:17,440");

            Assert.That(parsed, Is.EqualTo(new TimeSpan(0, 25, 2, 17, 440)));
        }

        [Test]
        public void ParseTime_WhenTimeFormatIsInvalid_ThrowsFormatException()
        {
            var parser = CreateSRTParser();

            Assert.Throws<FormatException>(() =>
            {
                parser.ParseTime("25.02.17.440");
            });
        }

        [Test]
        public void ParseTimeLine_WhenTimeIsValid_ReturnsExpectedBeginAndEnd()
        {
            var parser = CreateSRTParser();

            (var begin, var end) = parser.ParseTimeLine("00:02:17,440 --> 00:02:20,375");

            Assert.That(begin, Is.EqualTo(new TimeSpan(0, 0, 2, 17, 440)));
            Assert.That(end, Is.EqualTo(new TimeSpan(0, 0, 2, 20, 375)));
        }

        [Test]
        public void ParseTimeLine_WhenLineDoesntContainSeparator_ThrowsFormatException()
        {
            var parser = CreateSRTParser();

            Assert.Throws<FormatException>(() => { parser.ParseTimeLine("00:02:17,440 - 00:02:20,375"); });
        }

        [Test]
        public void ParseText_TextHasOneLine_ParsesSuccessfullyWithoutNewLine()
        {
            var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream))
            {
                writer.WriteLine("- How did he do that?");
                writer.Flush();
                memoryStream.Position = 0;

                var parser = CreateSRTParser();
                var text = parser.ParseText(new StreamReader(memoryStream));
                Assert.That(text, Is.EqualTo("- How did he do that?"));
            }
        }
        
        [Test]
        public void ParseText_TextHasTwoLines_ReturnsTextWithTwoSeparatedLines()
        {
            var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream))
            {
                writer.WriteLine("- How did he do that?");
                writer.WriteLine("- Made him an offer he couldn't refuse.");
                writer.Flush();
                memoryStream.Position = 0;

                var parser = CreateSRTParser();
                var text = parser.ParseText(new StreamReader(memoryStream));

                using (var reader = new StringReader(text))
                {
                    Assert.That(reader.ReadLine(), Is.EqualTo("- How did he do that?"));
                    Assert.That(reader.ReadLine(), Is.EqualTo("- Made him an offer he couldn't refuse."));
                }
            }
        }

        private static SRTSubtitleParser CreateSRTParser()
        {
            return new SRTSubtitleParser();
        }
    }
}
