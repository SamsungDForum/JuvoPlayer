using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JuvoPlayer.Subtitles;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSSrtSubtitleParser
    {
        [Test]
        public void ParseTime_WhenTimeIsValid_ParsesSuccessfully()
        {
            var parser = CreateSrtParser();

            var parsed = parser.ParseTime("00:02:17,440");

            Assert.That(parsed, Is.EqualTo(new TimeSpan(0, 0, 2, 17, 440)));
        }

        [Test]
        public void ParseTime_WhenTimeExceedesADay_ParsesSuccessfully()
        {
            var parser = CreateSrtParser();

            var parsed = parser.ParseTime("25:02:17,440");

            Assert.That(parsed, Is.EqualTo(new TimeSpan(0, 25, 2, 17, 440)));
        }

        [TestCase(null, Description = "Null time")]
        [TestCase("", Description = "Empty time")]
        [TestCase("25.02.17.440", Description = "Invalid separators")]
        [TestCase("25:02:17", Description = "Missing milliseconds")]
        [TestCase("a.b.c.d", Description = "Letters instead of numbers")]
        public void ParseTime_WhenTimeFormatIsInvalid_ThrowsFormatException(string invalidTimeToParse)
        {
            var parser = CreateSrtParser();

            Assert.Throws<FormatException>(() =>
            {
                parser.ParseTime(invalidTimeToParse);
            });
        }

        [Test]
        public void ParseTimeLine_WhenTimeIsValid_ReturnsExpectedBeginAndEnd()
        {
            var parser = CreateSrtParser();

            (var begin, var end) = parser.ParseTimeLine("00:02:17,440 --> 00:02:20,375");

            Assert.That(begin, Is.EqualTo(new TimeSpan(0, 0, 2, 17, 440)));
            Assert.That(end, Is.EqualTo(new TimeSpan(0, 0, 2, 20, 375)));
        }

        [TestCase(null, Description = "Null time line")]
        [TestCase("", Description = "Empty time line")]
        [TestCase("00:02:17,440 - 00:02:20,375", Description="Invalid separator")]
        [TestCase("00:02:17,440 -->", Description = "Only begin time")]
        [TestCase("-->  00:02:17,440", Description = "Only end time")]
        public void ParseTimeLine_WhenLineIsInvalid_ThrowsFormatException(string invalidTimeLineToParse)
        {
            var parser = CreateSrtParser();

            Assert.Throws<FormatException>(() => { parser.ParseTimeLine(invalidTimeLineToParse); });
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

                var parser = CreateSrtParser();
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

                var parser = CreateSrtParser();
                var text = parser.ParseText(new StreamReader(memoryStream));

                using (var reader = new StringReader(text))
                {
                    Assert.That(reader.ReadLine(), Is.EqualTo("- How did he do that?"));
                    Assert.That(reader.ReadLine(), Is.EqualTo("- Made him an offer he couldn't refuse."));
                }
            }
        }

        private static SrtSubtitleParser CreateSrtParser()
        {
            return new SrtSubtitleParser();
        }
    }
}
