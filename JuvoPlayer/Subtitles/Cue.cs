using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.Subtitles
{
    public class Cue
    {
        public string Text { get; set; }
        public TimeSpan Begin { get; set; }
        public TimeSpan End { get; set; }
    }
}
