using System;
using System.Collections.Generic;
using System.Text;

namespace XamarinPlayer.Services
{
    public class Stream
    {
        public enum StreamType
        {
            Audio,
            Video,
            Subtitle
        };

        public int Id { get; set; }
        public string Lang { get; set; }
        public StreamType Type { get; set; }
    }
}
