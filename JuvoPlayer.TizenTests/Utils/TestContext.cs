using System;
using System.Threading;

namespace JuvoPlayer.TizenTests.Utils
{
    public class TestContext
    {
        public PlayerService Service { get; set; }
        public string ClipTitle { get; set; }
        public TimeSpan? SeekTime { get; set; }
        public TimeSpan DelayTime { get; set; }
        public TimeSpan RandomMaxDelayTime { get; set; }
        public CancellationToken Token { get; set; }
        public TimeSpan Timeout { get; set; }
    }
}