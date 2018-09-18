using System;
using System.Runtime.InteropServices;

namespace Tizen.TV.Multimedia.ESPlayer
{
    public struct ESPacket
    {
        public StreamType type;
        public byte[] buffer;
        public ulong pts;
        public ulong duration;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ESNativePacket
    {
        public StreamType type;
        public IntPtr buffer;
        public uint bufferSize;
        public ulong pts;
        public ulong duration;
    }
}
