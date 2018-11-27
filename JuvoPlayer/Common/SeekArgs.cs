using System;

namespace JuvoPlayer.Common
{
    public struct SeekArgs
    {
        public TimeSpan Position { get; set; }
        public uint Id { get; set; }
    }
}