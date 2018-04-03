using System;
using System.IO;
using System.Text;

namespace JuvoPlayer.Subtitles
{
    internal class ParserUtils
    {
        public static string TrimLastNewLine(StringBuilder contentsBuilder)
        {
            var contents = contentsBuilder.ToString();
            if (contents.EndsWith(Environment.NewLine))
            {
                contents = contents.TrimEnd(Environment.NewLine.ToCharArray());
            }
            return contents;
        }

        public static string ParseText(StreamReader reader)
        {
            var contentsBuilder = new StringBuilder();
            for (var line = reader.ReadLine(); !string.IsNullOrEmpty(line); line = reader.ReadLine())
            {
                contentsBuilder.AppendLine(line);
            }
            var contents = TrimLastNewLine(contentsBuilder);
            return contents;
        }
    }
}
