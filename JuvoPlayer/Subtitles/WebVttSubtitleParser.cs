using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace JuvoPlayer.Subtitles
{
    internal class WebVttSubtitleParser : ISubtitleParser
    {
        public IEnumerable<Cue> Parse(StreamReader reader)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Cue> Parse(Stream stream)
        {
            throw new NotImplementedException();
        }

        internal (TimeSpan, TimeSpan) ParseTimeLine(string timeLine)
        {
            if (string.IsNullOrEmpty(timeLine))
                throw new FormatException("Time line cannot be null");

            string[] parts = timeLine.Split(new char[0], StringSplitOptions.RemoveEmptyEntries);

            const string timeSeparator = "-->";
            if (parts.Length < 3 || !parts[1].Equals(timeSeparator))
                throw new FormatException(string.Format("Invalid time line format [{0}]", timeLine));

            return (ParseTime(parts[0]), ParseTime(parts[2]));
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
    }
}
