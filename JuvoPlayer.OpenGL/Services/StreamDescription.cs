namespace JuvoPlayer.OpenGL.Services
{
    public class StreamDescription
    {
        public enum StreamType
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