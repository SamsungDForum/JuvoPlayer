using System;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public interface IAVIOContext : IDisposable
    {
        bool Seekable { get; set; }
        bool WriteFlag { get; set; }
    }
}