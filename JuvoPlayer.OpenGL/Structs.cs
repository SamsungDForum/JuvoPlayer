using Tizen.TV.NUI.GLApplication;

namespace JuvoPlayer.OpenGL
{
    internal unsafe partial class Program : TVGLApplication
    {
        private struct Tile {
            public int Id;
            public string ImgPath;
            public int ImgWidth;
            public int ImgHeight;
            public byte[] ImgPixels;
            public string Name;
            public string Description;
        }

        private enum IconType {
            Play,
            Resume,
            Stop,
            Pause,
            FastForward,
            Rewind,
            SkipToEnd,
            SkipToStart
        };

        private struct Icon {
            public IconType Id;
            public string ImgPath;
            public int ImgWidth;
            public int ImgHeight;
            public byte[] ImgPixels;
        }
    }
}