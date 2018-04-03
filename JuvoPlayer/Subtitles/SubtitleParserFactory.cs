using System;

namespace JuvoPlayer.Subtitles
{
    class SubtitleParserFactory
    {

        public ISubtitleParser CreateParser(SubtitleFormat format)
        {
            switch (format)
            {
                case SubtitleFormat.Invalid:
                    break;
                case SubtitleFormat.Subrip:
                    return new SrtSubtitleParser();
                case SubtitleFormat.WebVtt:
                    return new WebVttSubtitleParser();
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            return null;
        }
    }
}
