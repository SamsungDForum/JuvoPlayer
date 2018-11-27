using System;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public delegate ArraySegment<byte>? ReadPacket(int size);

    public interface IFFmpegGlue
    {
        void Initialize();
        IAVIOContext AllocIOContext(ulong bufferSize, ReadPacket readPacket);
        IAVFormatContext AllocFormatContext();
    }
}