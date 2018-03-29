using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

        internal TimeSpan ParseTime(string timeString)
        {
            if (string.IsNullOrEmpty(timeString))
                throw new FormatException("Invalid time format");

            timeString = timeString.Replace('.', ':');
            string[] parts = timeString.Split(':');

            if (parts.Length != 3 && parts.Length != 4)
                throw new FormatException("Invalid time format");

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
