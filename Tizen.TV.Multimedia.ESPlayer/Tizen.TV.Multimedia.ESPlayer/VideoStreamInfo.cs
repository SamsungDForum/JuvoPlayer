using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Tizen.TV.Multimedia.ESPlayer
{
    public struct VideoStreamInfo
    {
        public byte[] codecData;
        public VideoMimeType mimeType;
        public int width;
        public int height;
        public int maxWidth;
        public int maxHeight;
        public int num;
        public int den;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeVideoStreamInfo
    {
        public IntPtr codecData;
        public int codecDataLength;
        public VideoMimeType mimeType;
        public int width;
        public int height;
        public int maxWidth;
        public int maxHeight;
        public int num;
        public int den;
    }
}
