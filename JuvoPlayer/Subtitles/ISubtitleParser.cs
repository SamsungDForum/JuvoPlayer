using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JuvoPlayer.Subtitles
{
    internal interface ISubtitleParser
    {
        IEnumerable<Cue> Parse(StreamReader reader);

        IEnumerable<Cue> Parse(Stream stream);
    }
}
