using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace JuvoPlayer.Subtitles
{
    internal class SrtSubtitleParser : ISubtitleParser
    {
        public IEnumerable<Cue> Parse(Stream stream)
        {
            return Parse(new StreamReader(stream, Encoding.GetEncoding("windows-1252")));
        }

        public IEnumerable<Cue> Parse(StreamReader reader)
        {
            while (!reader.EndOfStream)
            {
                var line = MoveToFirstNonEmptyLine(reader);
                if (string.IsNullOrEmpty(line)) yield break;

                if (!IsCounterLine(line)) yield break;

                var timeLine = reader.ReadLine();
                (var begin, var end) = ParseTimeLine(timeLine);

                var text = ParseText(reader);

                yield return new Cue() { Text = text, Begin = begin, End = end};
            }
        }

        internal string ParseText(StreamReader reader)
        {
            var contentsBuilder = new StringBuilder();
            for (var line = reader.ReadLine(); !string.IsNullOrEmpty(line); line = reader.ReadLine())
            {
                contentsBuilder.AppendLine(line);
            }
            var contents = TrimLastNewLine(contentsBuilder);
            return contents;
        }

        private static string TrimLastNewLine(StringBuilder contentsBuilder)
        {
            var contents = contentsBuilder.ToString();
            if (contents.EndsWith(Environment.NewLine))
            {
                contents = contents.TrimEnd(Environment.NewLine.ToCharArray());
            }
            return contents;
        }

        private string MoveToFirstNonEmptyLine(StreamReader reader)
        {
            string line;
            do
            {
                line = reader.ReadLine();
            } while (string.IsNullOrEmpty(line));
            return line;
        }

        internal bool IsCounterLine(string line)
        {
            return Regex.IsMatch(line, @"^\d+$");
        }

        internal (TimeSpan, TimeSpan) ParseTimeLine(string line)
        {
            const string timeSeparator = "-->";
            if (!line.Contains(timeSeparator))
                throw new FormatException("Missing separator");
            string[] parts = line.Split(new string[] { timeSeparator }, StringSplitOptions.None);
            var beginString = parts[0];
            var endString = parts[1];
            return (ParseTime(beginString), ParseTime(endString));
        }

        internal TimeSpan ParseTime(string timeString)
        {
            timeString = timeString.Trim().Replace(',', ':');
            string[] parts = timeString.Split(':');
            int days = 0;
            int hours = Int32.Parse(parts[0]);
            int minutes = Int32.Parse(parts[1]);
            int seconds = Int32.Parse(parts[2]);
            int milliseconds = Int32.Parse(parts[3]);
            return new TimeSpan(days, hours, minutes, seconds, milliseconds);
        }
    }
}
