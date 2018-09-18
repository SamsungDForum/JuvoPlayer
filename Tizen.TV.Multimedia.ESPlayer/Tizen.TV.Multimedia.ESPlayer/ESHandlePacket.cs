using System.Runtime.InteropServices;

namespace Tizen.TV.Multimedia.ESPlayer
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ESHandlePacket
    {
        public StreamType type;
        public uint handle;
        public uint handleSize;
        public ulong pts;
        public ulong duration;
    }
}
