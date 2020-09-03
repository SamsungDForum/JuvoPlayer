using System;

namespace JuvoPlayer.Common
{
    public struct Configuration
    {
        public string PreferredAudioLanguage { get; set; }
        public TimeSpan? StartTime { get; set; }
    }
}
