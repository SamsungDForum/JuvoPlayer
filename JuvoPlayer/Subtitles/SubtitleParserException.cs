using System;

namespace JuvoPlayer.Subtitles
{
    public class SubtitleParserException : Exception
    {
        public SubtitleParserException()
        {
        }

        public SubtitleParserException(string message)
            : base(message)
        {
        }

        public SubtitleParserException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}