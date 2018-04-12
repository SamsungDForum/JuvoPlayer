using System.IO;
using JuvoPlayer.Subtitles;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    class TSParserUtils
    {
        [Test]
        public void ParseText_TextHasOneLine_ParsesSuccessfullyWithoutNewLine()
        {
            var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream))
            {
                writer.WriteLine("- How did he do that?");
                writer.Flush();
                memoryStream.Position = 0;

                var utils = CreateParserUtils();
                var text = ParserUtils.ParseText(new StreamReader(memoryStream));
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

                var utils = CreateParserUtils();
                var text = ParserUtils.ParseText(new StreamReader(memoryStream));

                using (var reader = new StringReader(text))
                {
                    Assert.That(reader.ReadLine(), Is.EqualTo("- How did he do that?"));
                    Assert.That(reader.ReadLine(), Is.EqualTo("- Made him an offer he couldn't refuse."));
                }
            }
        }

        private ParserUtils CreateParserUtils()
        {
            return new ParserUtils();
        }
    }
}
