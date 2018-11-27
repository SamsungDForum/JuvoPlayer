using System;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public class FFmpegException : Exception
    {
        public FFmpegException(string message)
            : base(message)
        {
        }

        public FFmpegException(string message, Exception ex)
            : base(message, ex)
        {
        }
    }
}