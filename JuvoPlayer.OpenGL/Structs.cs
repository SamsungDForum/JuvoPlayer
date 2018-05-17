namespace JuvoPlayer.OpenGL
{
    internal struct ImageData
    {
        public string Path;
        public int Width;
        public int Height;
        public byte[] Pixels;
    }

    internal struct Tile
    {
        public int Id;
        public ImageData Image;
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
        public ImageData Image;
    }

    internal struct Font
    {
        public int Id;
        public string FontPath;
        public byte[] FontData;
    }

    internal enum ColorSpace
    {
        RGB,
        RGBA
    }
}