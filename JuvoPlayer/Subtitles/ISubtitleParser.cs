using System.Collections.Generic;
using System.IO;

namespace JuvoPlayer.Subtitles
{
    /// <summary>
    /// A generic subtitle parser interface.
    /// </summary>
    internal interface ISubtitleParser
    {
        /// <summary>
        /// Parses subtitle content.
        /// </summary>
        /// <param name="reader">A reader which allows to read subtitle content</param>
        /// <exception cref="JuvoPlayer.Subtitles.SubtitleParserException">A <paramref name="reader"/> has invalid subtitle format</exception>
        /// <returns><see cref="System.Collections.Generic.IEnumerable"/> iterator, which contains parsed <see cref="JuvoPlayer.Subtitles.Cue"></see> cues</returns>
        IEnumerable<Cue> Parse(StreamReader reader);

        /// <summary>
        /// Parses subtitle content.
        /// </summary>
        /// <param name="stream">A stream which contains subtitles data. Default encoding is used basing on subtitle format</param>
        /// <exception cref="JuvoPlayer.Subtitles.SubtitleParserException">A <paramref name="stream"/> has invalid subtitle format</exception>
        /// <returns><see cref="System.Collections.Generic.IEnumerable"></see> iterator, which contains parsed <see cref="JuvoPlayer.Subtitles.Cue"></see> cues</returns>
        IEnumerable<Cue> Parse(Stream stream);
    }
}
