// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using JuvoPlayer.Common;
using System;
using System.Runtime.InteropServices;
using System.Text;
using Tizen.TV.Multimedia.IPTV;
using Window = ElmSharp.Window;
using ESPlayer = Tizen.TV.Multimedia.ESPlayer;
using StreamType = JuvoPlayer.Common.StreamType;

namespace JuvoPlayer.Player.EsPlayer
{
    /// <summary>
    /// TimeSpan extension methods. Provide time conversion
    /// functionality to convert TimeSpan to ESPlayer time values
    /// </summary>
    internal static class TimeSpanExtensions
    {
        internal static ulong ToMilliseconds(this TimeSpan clock)
        {
            return (ulong)(clock.Ticks / TimeSpan.TicksPerMillisecond);
        }

        internal static ulong ToNanoseconds(this TimeSpan clock)
        {
            return (ToMilliseconds(clock) * 1000000);
        }
    }

    /// <summary>
    /// Packet extension method allowing conversion from
    /// Common.Packet type to Tizen.TV.Multimedia.ESPlayer.EsPacket type
    /// </summary>
    internal static class PacketConversionExtensions
    {
        internal static ESPlayer.ESPacket ESUnencryptedPacket(this Common.Packet packet, ESPlayer.StreamType esStreamType)
        {
            return new ESPlayer.ESPacket
            {
                type = esStreamType,
                pts = packet.Pts.ToNanoseconds(),
                duration = packet.Duration.ToNanoseconds(),
                buffer = packet.Data
            };
        }

        internal static ESPlayer.ESHandlePacket ESDecryptedPacket(this Common.DecryptedEMEPacket packet, ESPlayer.StreamType esStreamType)
        {
            return new ESPlayer.ESHandlePacket
            {
                type = esStreamType,
                pts = packet.Pts.ToNanoseconds(),
                duration = packet.Duration.ToNanoseconds(),

                handle = packet.HandleSize.handle,
                handleSize = packet.HandleSize.size

            };
        }
    }

    internal static class ESPlayerStreamInfoExtensions
    {
        internal static string DumpConfig(this ESPlayer.VideoStreamInfo videoConf)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("VideoStreamInfo:");
            sb.Append("\tmimeType = ");
            sb.AppendLine(videoConf.mimeType.ToString());
            sb.Append("\tWidth / Max = ");
            sb.Append(videoConf.width);
            sb.Append(" / ");
            sb.AppendLine(videoConf.maxWidth.ToString());
            sb.Append("\tHeight / Max = ");
            sb.Append(videoConf.height);
            sb.Append(" / ");
            sb.AppendLine(videoConf.maxHeight.ToString());
            sb.Append("\tFrameRate = ");
            sb.Append(videoConf.num + "/");
            sb.Append(videoConf.den);
            sb.AppendLine(" (" + (videoConf.num / (videoConf.den == 0 ? 1 : videoConf.den)) + ")");

            return sb.ToString();
        }

        internal static string DumpConfig(this ESPlayer.AudioStreamInfo audioConf)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("AudioStreamInfo:");
            sb.Append("\tmimeType = ");
            sb.AppendLine(audioConf.mimeType.ToString());
            sb.Append("\tsampleRate = ");
            sb.AppendLine(audioConf.sampleRate.ToString());
            sb.Append("\tchannels = ");
            sb.AppendLine(audioConf.channels.ToString());

            return sb.ToString();
        }
    }

    /// <summary>
    /// Buffer configuration data storage in Common.Packet type.
    /// </summary>
    internal class BufferConfigurationPacket : Packet
    {
        public static BufferConfigurationPacket Create(StreamConfig config)
        {
            var result = new BufferConfigurationPacket()
            {
                Config = config,
                StreamType = config.StreamType(),
                Pts = TimeSpan.MinValue
            };

            return result;
        }

        public StreamConfig Config { get; private set; }
    };

    /// <summary>
    /// EsPlayer various utility/helper functions
    /// </summary>
    internal static class EsPlayerUtils
    {
        public static readonly int DefaultWindowWidth = 1920;
        public static readonly int DefaultWindowHeight = 1080;

        internal static Window CreateWindow(int width, int height)
        {
            var window = new Window("JuvoPlayer")
            {
                Geometry = new ElmSharp.Rect(0, 0, width, height)
            };

            // Sample code calls following API:
            // skipping geometry settings
            //
            // window.Resize(width, height);
            // window.Realize(null);
            // window.Active();
            // window.Show();
            // 
            // Does not seem to be necessary in case of Juvo/Xamarin
            //

            return window;
        }

        internal static void DestroyWindow(ref Window window)
        {
            window.Hide();
            window.Unrealize();
            window = null;
        }

        internal static Common.StreamType JuvoStreamType(ESPlayer.StreamType esStreamType)
        {
            return esStreamType == ESPlayer.StreamType.Video ?
                StreamType.Video : StreamType.Audio;
        }

        internal static ESPlayer.VideoMimeType GetCodecMimeType(VideoCodec videoCodec)
        {
            switch (videoCodec)
            {
                case VideoCodec.H264:
                    return ESPlayer.VideoMimeType.H264;
                case VideoCodec.H265:
                    return ESPlayer.VideoMimeType.Hevc;
                case VideoCodec.MPEG2:
                    return ESPlayer.VideoMimeType.Mpeg2;
                case VideoCodec.MPEG4:
                    return ESPlayer.VideoMimeType.Mpeg4;
                case VideoCodec.VP8:
                    return ESPlayer.VideoMimeType.Vp8;
                case VideoCodec.VP9:
                    return ESPlayer.VideoMimeType.Vp9;
                case VideoCodec.WMV3:
                    return ESPlayer.VideoMimeType.Wmv3;
                case VideoCodec.WMV1:
                case VideoCodec.WMV2:
                default:
                    return ESPlayer.VideoMimeType.UnKnown;
            }
        }

        internal static ESPlayer.AudioMimeType GetCodecMimeType(AudioCodec audioCodec)
        {
            switch (audioCodec)
            {
                case AudioCodec.AAC:
                    return ESPlayer.AudioMimeType.Aac;
                case AudioCodec.MP2:
                    return ESPlayer.AudioMimeType.Mp2;
                case AudioCodec.MP3:
                    return ESPlayer.AudioMimeType.Mp3;
                case AudioCodec.VORBIS:
                    return ESPlayer.AudioMimeType.Vorbis;
                case AudioCodec.PCM_S16BE:
                    return ESPlayer.AudioMimeType.PcmS16be;
                case AudioCodec.PCM_S24BE:
                    return ESPlayer.AudioMimeType.PcmS24be;
                case AudioCodec.EAC3:
                    return ESPlayer.AudioMimeType.Eac3;
                case AudioCodec.DTS:
                    return ESPlayer.AudioMimeType.Dts;
                case AudioCodec.AC3:
                    return ESPlayer.AudioMimeType.Ac3;
                case AudioCodec.PCM:
                case AudioCodec.FLAC:
                case AudioCodec.AMR_NB:
                case AudioCodec.AMR_WB:
                case AudioCodec.PCM_MULAW:
                case AudioCodec.GSM_MS:
                case AudioCodec.OPUS:
                case AudioCodec.WMAV1:
                case AudioCodec.WMAV2:
                default:
                    return ESPlayer.AudioMimeType.UnKnown;
            }
        }
    }


}
