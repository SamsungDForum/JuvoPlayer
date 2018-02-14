using JuvoPlayer.Common;
using Tizen.TV.Smplayer;
using StreamType = JuvoPlayer.Common.StreamType;

namespace JuvoPlayer.Player
{
    static class SMPlayerUtils
    {
        public static TrackType GetTrackType(StreamPacket packet)
        {
            TrackType trackType;
            if (packet.StreamType == StreamType.Video)
                trackType = TrackType.Video;
            else if (packet.StreamType == StreamType.Audio)
                trackType = TrackType.Audio;
            else if (packet.StreamType == StreamType.Subtitle)
                trackType = TrackType.Subtitle;
            else
                trackType = TrackType.Max;
            return trackType;
        }

        public static string GetCodecMimeType(VideoCodec videoCodec)
        {
            switch (videoCodec)
            {
                case VideoCodec.H264:
                    return "video/x-h264";
                case VideoCodec.H265:
                    return "video/x-h265";
                case VideoCodec.MPEG2:
                case VideoCodec.MPEG4:
                    return "video/mpeg";
                case VideoCodec.VP8:
                    return "video/x-vp8";
                case VideoCodec.VP9:
                    return "video/x-vp9";
                case VideoCodec.WMV1:
                case VideoCodec.WMV2:
                case VideoCodec.WMV3:
                    return "video/x-wmv";
                default:
                    return "";
            }
        }

        public static string GetCodecMimeType(AudioCodec audioCodec)
        {
            switch (audioCodec)
            {
                case AudioCodec.AAC:
                case AudioCodec.MP2:
                case AudioCodec.MP3:
                    return "audio/mpeg";
                case AudioCodec.PCM:
                    return "audio/x-raw-int";
                case AudioCodec.VORBIS:
                    return "audio/x-vorbis";
                case AudioCodec.FLAC:
                    return "audio/x-flac";
                case AudioCodec.AMR_NB:
                    return "audio/AMR";
                case AudioCodec.AMR_WB:
                    return "audio/AMR-WB";
                case AudioCodec.PCM_MULAW:
                    return "audio/x-mulaw";
                case AudioCodec.GSM_MS:
                    return "audio/ms-gsm";
                case AudioCodec.PCM_S16BE:
                    return "audio/x-raw";
                case AudioCodec.PCM_S24BE:
                    return "audio/x-raw";
                case AudioCodec.OPUS:
                    return "audio/ogg";
                case AudioCodec.EAC3:
                    return "audio/x-eac3";
                case AudioCodec.DTS:
                    return "audio/x-dts";
                case AudioCodec.AC3:
                    return "audio/x-ac3";
                case AudioCodec.WMAV1:
                case AudioCodec.WMAV2:
                    return "audio/x-ms-wma";
                default:
                    return "";
            }
        }

        public static uint GetCodecVersion(VideoCodec videoCodec)
        {
            switch (videoCodec)
            {
                case VideoCodec.MPEG2:
                case VideoCodec.WMV2:
                    return 2;
                case VideoCodec.MPEG4:
                    return 4;
                case VideoCodec.WMV1:
                    return 1;
                case VideoCodec.WMV3:
                    return 3;
                case VideoCodec.H264:
                    return 4;
                default:
                    return 0;
            }
        }

        public static uint GetCodecVersion(AudioCodec audioCodec)
        {
            switch (audioCodec)
            {
                case AudioCodec.AAC:
                    return 4;
                case AudioCodec.MP3:
                    return 1;
                case AudioCodec.MP2:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
