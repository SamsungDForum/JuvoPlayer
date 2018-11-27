using System;
using System.Runtime.InteropServices;
using JuvoLogger;
using JuvoPlayer.Demuxers.FFmpeg.Interop;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public class FFmpegGlue : IFFmpegGlue
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public void Initialize()
        {
            try
            {
                Interop.FFmpeg
                    .av_register_all(); // TODO(g.skowinski): Is registering multiple times unwanted or doesn't it matter?
                unsafe
                {
                    Interop.FFmpeg.av_log_set_level(FFmpegMacros.AV_LOG_WARNING);
                    av_log_set_callback_callback logCallback = (p0, level, format, vl) =>
                    {
                        if (level > Interop.FFmpeg.av_log_get_level()) return;

                        const int lineSize = 1024;
                        var lineBuffer = stackalloc byte[lineSize];
                        var printPrefix = 1;
                        Interop.FFmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                        var line = Marshal.PtrToStringAnsi((IntPtr) lineBuffer);

                        Logger.Warn(line);
                    };
                    Interop.FFmpeg.av_log_set_callback(logCallback);
                }
            }
            catch (Exception e)
            {
                Logger.Info("Could not load and register FFmpeg library");
                throw new DemuxerException("Could not load and register FFmpeg library", e);
            }
        }

        public IAVIOContext AllocIOContext(ulong bufferSize, ReadPacket readPacket)
        {
            return new AVIOContextWrapper(bufferSize, readPacket);
        }

        public IAVFormatContext AllocFormatContext()
        {
            return new AVFormatContextWrapper();
        }
    }
}