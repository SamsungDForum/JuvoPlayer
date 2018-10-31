namespace JuvoPlayer.OpenGL
{
    public enum PlayerState // int values are passed down to native code
    {
        Error = -1,
        Idle = 0,
        Preparing = 1,
        Prepared = 2,
        Stopped = 3,
        Playing = 4,
        Paused = 5,
        Completed = 6,
        Buffering = 7
    }

    internal struct ImageData
    {
        public string Path;
        public int Width;
        public int Height;
        public byte[] Pixels;
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
        SkipToStart,
        Options
    };

    internal struct Icon
    {
        public IconType Id;
        public ImageData Image;
    }

    internal enum ColorSpace
    {
        RGB,
        RGBA
    }

    internal enum MenuAction
    {
        None = 0,
        PlaybackControl = 1,
        OptionsMenu = 2
    }
}