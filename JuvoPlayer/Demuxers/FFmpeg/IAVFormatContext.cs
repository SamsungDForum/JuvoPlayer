using System;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers.FFmpeg.Interop;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public interface IAVFormatContext : IDisposable
    {
        long ProbeSize { get; set; }
        TimeSpan MaxAnalyzeDuration { get; set; }
        IAVIOContext AVIOContext { get; set; }
        TimeSpan Duration { get; }
        DRMInitData[] DRMInitData { get; }

        void Open();
        void FindStreamInfo();
        int FindBestStream(AVMediaType mediaType);
        int FindBestBandwidthStream(AVMediaType mediaType);
        void SetStreams(int audioIdx, int videoIdx);
        StreamConfig ReadConfig(int index);
        Packet NextPacket(int[] streamIndexes);
    }
}