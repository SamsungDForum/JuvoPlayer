namespace JuvoPlayer.OpenGL.Services
{
    public class StreamDescription // TODO(g.skowinski): Use JuvoPlayer.Common.StreamDescription; instead
    {
        public enum StreamType // TODO(g.skowinski): Use JuvoPlayer.Common.StreamType instead
        {
            Audio,
            Video,
            Subtitle
        };

        public int Id { get; set; }
        public string Description { get; set; }
        public StreamType Type { get; set; }
        public bool Default { get; set; }
    }
}