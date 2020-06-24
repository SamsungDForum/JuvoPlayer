/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using JuvoPlayer.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using JuvoPlayer.Drms;
using JuvoPlayer.Utils;
using Tizen.TV.Multimedia;
using StreamType = Tizen.TV.Multimedia.StreamType;

using PacketAndStorageType = System.ValueTuple<System.Type, System.Type>;
using PushFunc = System.Func<Tizen.TV.Multimedia.ESPlayer, JuvoPlayer.Common.Packet, Tizen.TV.Multimedia.SubmitStatus>;

namespace JuvoPlayer.Player.EsPlayer
{
    /// <summary>
    /// TimeSpan extension methods. Provide time conversion
    /// functionality to convert TimeSpan to ESPlayer time values
    /// </summary>
    internal static class TimeSpanExtensions
    {
        internal static TimeSpan FromNano(this ulong nanoTime)
        {
            return TimeSpan.FromMilliseconds(nanoTime / (double)1000000);
        }

        internal static ulong TotalNanoseconds(this TimeSpan clock)
        {
            return (ulong)(clock.TotalMilliseconds * 1000000);
        }
    }

    /// <summary>
    /// Packet extension method allowing conversion from
    /// Common.Packet type to Tizen.TV.Multimedia.ESPlayer.EsPacket type
    /// </summary>
    internal static class PacketConversionExtensions
    {
        private static unsafe byte[] ToManagedBuffer(this INativeDataStorage storage)
        {
            var managedBuffer = new byte[storage.Length];
            Marshal.Copy((IntPtr)storage.Data, managedBuffer, 0, storage.Length);
            return managedBuffer;
        }

        internal static ESPacket ToESPacket(this Packet packet)
        {
            byte[] buffer;
            if (packet.Storage is IManagedDataStorage dataStorage)
                buffer = dataStorage.Data;
            else
                buffer = ((INativeDataStorage)packet.Storage).ToManagedBuffer();
            return new ESPacket
            {
                type = packet.StreamType.ESStreamType(),
                pts = packet.Pts.TotalNanoseconds(),
                duration = packet.Duration.TotalNanoseconds(),
                buffer = buffer
            };
        }

        internal static ESHandlePacket ToESHandlePacket(this DecryptedEMEPacket packet)
        {
            return new ESHandlePacket
            {
                type = packet.StreamType.ESStreamType(),
                pts = packet.Pts.TotalNanoseconds(),
                duration = packet.Duration.TotalNanoseconds(),
                handle = packet.HandleSize.handle,
                handleSize = packet.HandleSize.size
            };
        }
    }

    internal static class PushPacketStrategies
    {
        private static readonly IDictionary<PacketAndStorageType, PushFunc> dispatchMap = new Dictionary<PacketAndStorageType, PushFunc>();

        private static Type GetStorageType(IDataStorage storage)
        {
            switch (storage)
            {
                case null:
                    return null;
                case IManagedDataStorage _:
                    return typeof(IManagedDataStorage);
                case INativeDataStorage _:
                    return typeof(INativeDataStorage);
                default:
                    throw new InvalidOperationException($"Unsupported type: {storage.GetType()}");
            }
        }

        static PushPacketStrategies()
        {
            dispatchMap[(typeof(Packet), typeof(INativeDataStorage))] = SubmitManagedPriv;
            dispatchMap[(typeof(Packet), typeof(IManagedDataStorage))] = SubmitManagedPriv;
            dispatchMap[(typeof(DecryptedEMEPacket), null)] = (player, packet) =>
                SubmitDecryptedPriv(player, (DecryptedEMEPacket)packet);
            dispatchMap[(typeof(EOSPacket), null)] = (player, packet) => SubmitEOSPriv(player, (EOSPacket)packet);
        }

        internal static SubmitStatus Submit(this ESPlayer player, Packet packet)
        {
            return dispatchMap[(packet.GetType(), GetStorageType(packet.Storage))].Invoke(player, packet);
        }

        private static SubmitStatus SubmitManagedPriv(ESPlayer player, Packet packet)
        {
            var esPacket = packet.ToESPacket();
            return EsPlayerExtensions.IsWorkaroundNeeded()
                ? player.SubmitPacketExt(esPacket)
                : player.SubmitPacket(esPacket);
        }

        private static SubmitStatus SubmitDecryptedPriv(ESPlayer player, DecryptedEMEPacket packet)
        {
            var esHandle = packet.ToESHandlePacket();
            return player.SubmitPacket(esHandle);
        }

        private static SubmitStatus SubmitEOSPriv(ESPlayer player, EOSPacket packet)
        {
            var esStreamType = packet.StreamType.ESStreamType();
            return player.SubmitEosPacket(esStreamType);
        }
    }

    internal static class ESPlayerStreamInfoExtensions
    {
        internal static string DumpConfig(this VideoStreamInfo videoConf)
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
            sb.AppendLine("\tCodec Data:");
            sb.AppendLine(DumpTools.HexDump(videoConf.codecData));

            return sb.ToString();
        }

        internal static string DumpConfig(this AudioStreamInfo audioConf)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("AudioStreamInfo:");
            sb.Append("\tmimeType = ");
            sb.AppendLine(audioConf.mimeType.ToString());
            sb.Append("\tsampleRate = ");
            sb.AppendLine(audioConf.sampleRate.ToString());
            sb.Append("\tchannels = ");
            sb.AppendLine(audioConf.channels.ToString());
            sb.AppendLine("\tCodec Data:");
            sb.AppendLine(DumpTools.HexDump(audioConf.codecData));
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
        private class VideoConfiguration
        {
            public VideoCodec Codec { get; internal set; }
            public int Fps { get; internal set; }
            public int Bps { get; internal set; }
            public int Width { get; internal set; }
            public int Height { get; internal set; }
            public bool Supported { get; internal set; }
        }

        // Based on
        // https://developer.samsung.com/tv/develop/specifications/media-specifications/2018-tv-video-specifications
        //
        private static readonly IList<VideoConfiguration> VideoConfigurations = new List<VideoConfiguration>
        {
            new VideoConfiguration{Codec = VideoCodec.MPEG4,  Fps = 60, Bps = 60*1000000, Width=1920, Height = 1080, Supported = true},
            new VideoConfiguration{Codec = VideoCodec.H263,   Fps = 30, Bps = 30*1000000, Width=1920, Height = 1080, Supported = true},
            new VideoConfiguration{Codec = VideoCodec.H264,   Fps = 30, Bps = 60*1000000, Width=4096, Height = 2160, Supported = true},
            new VideoConfiguration{Codec = VideoCodec.H264,   Fps = 60, Bps = 60*1000000, Width=3840, Height = 2160, Supported = true},
            new VideoConfiguration{Codec = VideoCodec.H265,   Fps = 60, Bps = 80*1000000, Width=4096, Height = 2160, Supported = true},
            new VideoConfiguration{Codec = VideoCodec.INDEO3, Fps = 30, Bps = 20*1000000, Width=1920, Height = 1080, Supported = false},
            new VideoConfiguration{Codec = VideoCodec.MPEG2,  Fps = 60, Bps = 20*1000000, Width=1920, Height = 1080, Supported = true},
            new VideoConfiguration{Codec = VideoCodec.THEORA, Fps = 30, Bps = 20*1000000, Width=1920, Height = 1080, Supported = false},
            new VideoConfiguration{Codec = VideoCodec.VC1,    Fps = 60, Bps = 20*1000000, Width=1920, Height = 1080, Supported = true},
            new VideoConfiguration{Codec = VideoCodec.VP8,    Fps = 60, Bps = 20*1000000, Width=1920, Height = 1080, Supported = true},
            new VideoConfiguration{Codec = VideoCodec.VP9,    Fps = 60, Bps = 20*1000000, Width=1920, Height = 1080, Supported = false},
            new VideoConfiguration{Codec = VideoCodec.WMV1,   Fps = 30, Bps = 20*1000000, Width=1920, Height = 1080, Supported = true},
            new VideoConfiguration{Codec = VideoCodec.WMV2,   Fps = 30, Bps = 20*1000000, Width=1920, Height = 1080, Supported = true},
            new VideoConfiguration{Codec = VideoCodec.WMV3,   Fps = 60, Bps = 20*1000000, Width=1920, Height = 1080, Supported = true}
        };

        internal static Common.StreamType JuvoStreamType(this StreamType esStreamType)
        {
            return esStreamType == StreamType.Video ? Common.StreamType.Video : Common.StreamType.Audio;
        }

        internal static StreamType ESStreamType(this Common.StreamType juvoStreamType)
        {
            return juvoStreamType == Common.StreamType.Video ? StreamType.Video : StreamType.Audio;
        }

        internal static VideoStreamInfo ESVideoStreamInfo(this Common.StreamConfig streamConfig)
        {
            if (!(streamConfig is Common.VideoStreamConfig))
                throw new ArgumentException("StreamConfig is not of VideoStreamConfig Type");

            var videoConfig = (Common.VideoStreamConfig)streamConfig;

            // Sort configuration by FPS (lowest first) & get an entry matching codec & FPS
            var fpsOrderedConfigs = VideoConfigurations.OrderBy(entry => entry.Fps);
            var configParameters = fpsOrderedConfigs.FirstOrDefault(entry =>
                                       videoConfig.Codec == entry.Codec && entry.Fps >= videoConfig.FrameRate) ??
                                   fpsOrderedConfigs.LastOrDefault(entry => videoConfig.Codec == entry.Codec);

            if (configParameters == null)
                throw new UnsupportedStreamException($"Unsupported codec {videoConfig.Codec}");

            return new VideoStreamInfo
            {
                codecData = videoConfig.CodecExtraData,
                mimeType = EsPlayerUtils.GetCodecMimeType(videoConfig.Codec),
                width = videoConfig.Size.Width,
                maxWidth = configParameters.Width,
                height = videoConfig.Size.Height,
                maxHeight = configParameters.Height,
                num = videoConfig.FrameRateNum,
                den = videoConfig.FrameRateDen
            };
        }

        internal static AudioStreamInfo ESAudioStreamInfo(this Common.StreamConfig streamConfig)
        {
            if (!(streamConfig is Common.AudioStreamConfig))
                throw new ArgumentException("StreamConfig is not of AudioStreamConfig Type");

            var audioConfig = (Common.AudioStreamConfig)streamConfig;

            return new AudioStreamInfo
            {
                codecData = audioConfig.CodecExtraData,
                mimeType = EsPlayerUtils.GetCodecMimeType(audioConfig.Codec),
                sampleRate = audioConfig.SampleRate,
                channels = audioConfig.ChannelLayout
            };
        }

        internal static VideoMimeType GetCodecMimeType(VideoCodec videoCodec)
        {
            switch (videoCodec)
            {
                case VideoCodec.H263:
                    return VideoMimeType.H263;
                case VideoCodec.H264:
                    return VideoMimeType.H264;
                case VideoCodec.H265:
                    return VideoMimeType.Hevc;
                case VideoCodec.MPEG2:
                    return VideoMimeType.Mpeg2;
                case VideoCodec.MPEG4:
                    return VideoMimeType.Mpeg4;
                case VideoCodec.VP8:
                    return VideoMimeType.Vp8;
                case VideoCodec.VP9:
                    return VideoMimeType.Vp9;
                case VideoCodec.WMV3:
                    return VideoMimeType.Wmv3;
                case VideoCodec.WMV1:
                case VideoCodec.WMV2:
                default:
                    throw new ArgumentOutOfRangeException(
                        $"No mapping from Juvo video codec {videoCodec} to ESPlayer aideo codec");
            }
        }

        internal static AudioMimeType GetCodecMimeType(AudioCodec audioCodec)
        {
            switch (audioCodec)
            {
                case AudioCodec.AAC:
                    return AudioMimeType.Aac;
                case AudioCodec.MP2:
                    return AudioMimeType.Mp2;
                case AudioCodec.MP3:
                    return AudioMimeType.Mp3;
                case AudioCodec.VORBIS:
                    return AudioMimeType.Vorbis;
                case AudioCodec.PCM_S16BE:
                    return AudioMimeType.PcmS16be;
                case AudioCodec.PCM_S24BE:
                    return AudioMimeType.PcmS24be;
                case AudioCodec.EAC3:
                    return AudioMimeType.Eac3;

                case AudioCodec.AC3:
                    return AudioMimeType.Ac3;
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
                    throw new ArgumentOutOfRangeException(
                        $"No mapping from Juvo audio codec {audioCodec} to ESPlayer audio codec");
            }
        }
    }

    internal static class StreamConfigExtensions
    {
        internal static bool IsCompatible(this StreamConfig config, StreamConfig otherConfig)
        {
            switch (config)
            {
                case VideoStreamConfig videoConfig:
                    return videoConfig.IsCompatible(otherConfig as VideoStreamConfig);

                case AudioStreamConfig audioConfig:
                    return audioConfig.IsCompatible(otherConfig as AudioStreamConfig);

                case null:
                    throw new ArgumentNullException(nameof(config), "Config cannot be null");

                default:
                    throw new ArgumentException("Unsupported configuration type", nameof(config));
            }
        }

        private static bool IsCompatible(this VideoStreamConfig config, VideoStreamConfig otherConfig)
        {
            return otherConfig != null &&
                   config.Codec == otherConfig.Codec;
        }

        private static bool IsCompatible(this AudioStreamConfig config, AudioStreamConfig otherConfig)
        {
            return otherConfig != null &&
                   config.Codec == otherConfig.Codec &&
                   config.SampleRate == otherConfig.SampleRate &&
                   config.BitsPerChannel == otherConfig.BitsPerChannel;
        }
    }
}