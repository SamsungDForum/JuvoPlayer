namespace JuvoPlayer.OpenGL
{
    internal struct Tile
    {
        public int Id;
        public string ImgPath;
        public int ImgWidth;
        public int ImgHeight;
        public byte[] ImgPixels;
        public string Name;
        public string Description;
    }

    internal enum IconType
    {
        Play,
        Resume,
        Stop,
        Pause,
        FastForward,
        Rewind,
        SkipToEnd,
        SkipToStart
    };

    internal struct Icon
    {
        public IconType Id;
        public string ImgPath;
        public int ImgWidth;
        public int ImgHeight;
        public byte[] ImgPixels;
    }
}