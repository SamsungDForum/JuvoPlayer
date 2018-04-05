using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace JuvoPlayer.Subtitles
{
    /// <summary>
    /// Simplified WebVtt parser. Text formatting, CSS styles, positioning
    /// are ignored.
    /// </summary>
    internal class WebVttSubtitleParser : ISubtitleParser
    {
        private static readonly string TimeSeparator = "-->";

        public string DefaultEncoding { get; } = "utf-8";

        public IEnumerable<Cue> Parse(StreamReader reader)
        {
            try
            {
                return ParseLoop(reader);
            }
            catch (FormatException ex)
            {
                throw new SubtitleParserException("Cannot parse subtitle file", ex);
            }
        }

        private IEnumerable<Cue> ParseLoop(StreamReader reader)
        {
            while (!reader.EndOfStream)
            {
                var line = MoveToTimeLine(reader);
                if (line == null) yield break;

                (var begin, var end) = ParseTimeLine(line);
                var text = ParseText(reader);

                yield return new Cue()
                {
                    Begin = begin,
                    End = end,
                    Text = text,
                };
            }
        }

        internal string MoveToTimeLine(StreamReader reader)
        {
            string line;
            do
            {
                line = reader.ReadLine();
            } while (!IsTimeLine(line) && !reader.EndOfStream);
            return string.IsNullOrEmpty(line) ? null : line;
        }

        internal (TimeSpan, TimeSpan) ParseTimeLine(string timeLine)
        {
            if (!IsTimeLine(timeLine))
                throw new FormatException("Time line cannot be null");

            string[] parts = timeLine.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3 || !parts[1].Equals(TimeSeparator))
                throw new FormatException(string.Format("Invalid time line format [{0}]", timeLine));

            return (ParseTime(parts[0]), ParseTime(parts[2]));
        }

        private bool IsTimeLine(string timeLine)
        {
            return timeLine != null && timeLine.Contains(TimeSeparator);
        }

        internal virtual TimeSpan ParseTime(string timeString)
        {
            if (string.IsNullOrEmpty(timeString))
                throw new FormatException("Time string cannot be null");

            if (!Regex.IsMatch(timeString, @"^\d+:\d+:\d+\.\d+$") && !Regex.IsMatch(timeString, @"^\d+:\d+\.\d+$"))
                throw new FormatException(string.Format("Invalid time format [{0}]", timeString));

            timeString = timeString.Replace('.', ':');
            string[] parts = timeString.Split(':');

            int days = 0;
            int nextPartIndex = 0;
            int hours = parts.Length == 4 ? Int32.Parse(parts[nextPartIndex++]) : 0;
            int minutes = Int32.Parse(parts[nextPartIndex++]);
            int seconds = Int32.Parse(parts[nextPartIndex++]);
            int milliseconds = Int32.Parse(parts[nextPartIndex++]);

            return new TimeSpan(days, hours, minutes, seconds, milliseconds);
        }

        internal string ParseText(StreamReader reader)
        {
            var parsed = ParserUtils.ParseText(reader);
            return WebUtility.HtmlDecode(parsed);
        }
    }
}
