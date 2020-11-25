/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2020, Samsung Electronics Co., Ltd
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
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using JuvoPlayer.Common;
using JuvoPlayer.Players;
using Tizen.TV.Multimedia;
using StreamType = Tizen.TV.Multimedia.StreamType;

namespace JuvoPlayer.Platforms.Tizen
{
    public static class EsPlayerExtensions
    {
        // Based on
        // https://developer.samsung.com/tv/develop/specifications/media-specifications/2018-tv-video-specifications
        private static readonly IList<VideoConfiguration> VideoConfigurations = new List<VideoConfiguration>
        {
            new VideoConfiguration
            {
                Codec = VideoCodec.Mpeg4,
                Fps = 60,
                Bps = 60 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.H263,
                Fps = 30,
                Bps = 30 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.H264,
                Fps = 30,
                Bps = 60 * 1000000,
                Width = 4096,
                Height = 2160,
                Supported = true
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.H264,
                Fps = 60,
                Bps = 60 * 1000000,
                Width = 3840,
                Height = 2160,
                Supported = true
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.H265,
                Fps = 60,
                Bps = 80 * 1000000,
                Width = 4096,
                Height = 2160,
                Supported = true
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.Indeo3,
                Fps = 30,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = false
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.Mpeg2,
                Fps = 60,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.Theora,
                Fps = 30,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = false
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.Vc1,
                Fps = 60,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.Vp8,
                Fps = 60,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.Vp9,
                Fps = 60,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = false
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.Wmv1,
                Fps = 30,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.Wmv2,
                Fps = 30,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                Codec = VideoCodec.Wmv3,
                Fps = 60,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            }
        };

        public static SubmitResult ToSubmitResult(this SubmitStatus status)
        {
            switch (status)
            {
                case SubmitStatus.NotPrepared:
                    return SubmitResult.NotPrepared;
                case SubmitStatus.InvalidPacket:
                    return SubmitResult.InvalidPacket;
                case SubmitStatus.OutOfMemory:
                    return SubmitResult.OutOfMemory;
                case SubmitStatus.Full:
                    return SubmitResult.Full;
                case SubmitStatus.Success:
                    return SubmitResult.Success;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        public static PlayerState ToPlayerState(this ESPlayerState esState)
        {
            switch (esState)
            {
                case ESPlayerState.None:
                    return PlayerState.None;
                case ESPlayerState.Idle:
                    return PlayerState.Idle;
                case ESPlayerState.Ready:
                    return PlayerState.Ready;
                case ESPlayerState.Playing:
                    return PlayerState.Playing;
                case ESPlayerState.Paused:
                    return PlayerState.Paused;
                default:
                    throw new ArgumentOutOfRangeException(nameof(esState), esState, null);
            }
        }

        internal static VideoStreamInfo EsVideoStreamInfo(this StreamConfig streamConfig)
        {
            if (!(streamConfig is VideoStreamConfig))
                throw new ArgumentException("StreamConfig is not of VideoStreamConfig Type");

            var videoConfig = (VideoStreamConfig)streamConfig;

            // Sort configuration by FPS (lowest first) & get an entry matching codec & FPS
            var fpsOrderedConfigs = VideoConfigurations.OrderBy(entry => entry.Fps);
            var configParameters = fpsOrderedConfigs.FirstOrDefault(entry =>
                                       videoConfig.Codec == entry.Codec && entry.Fps >= videoConfig.FrameRate) ??
                                   fpsOrderedConfigs.LastOrDefault(entry => videoConfig.Codec == entry.Codec);

            if (configParameters == null)
                throw new ArgumentException($"Unsupported codec {videoConfig.Codec}");

            return new VideoStreamInfo
            {
                codecData = videoConfig.CodecExtraData,
                mimeType = GetCodecMimeType(videoConfig.Codec),
                width = videoConfig.Size.Width,
                maxWidth = configParameters.Width,
                height = videoConfig.Size.Height,
                maxHeight = configParameters.Height,
                num = videoConfig.FrameRateNum,
                den = videoConfig.FrameRateDen
            };
        }

        internal static AudioStreamInfo EsAudioStreamInfo(this StreamConfig streamConfig)
        {
            if (!(streamConfig is AudioStreamConfig))
                throw new ArgumentException("StreamConfig is not of AudioStreamConfig Type");

            var audioConfig = (AudioStreamConfig)streamConfig;

            return new AudioStreamInfo
            {
                codecData = audioConfig.CodecExtraData,
                mimeType = GetCodecMimeType(audioConfig.Codec),
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
                case VideoCodec.Mpeg2:
                    return VideoMimeType.Mpeg2;
                case VideoCodec.Mpeg4:
                    return VideoMimeType.Mpeg4;
                case VideoCodec.Vp8:
                    return VideoMimeType.Vp8;
                case VideoCodec.Vp9:
                    return VideoMimeType.Vp9;
                case VideoCodec.Wmv3:
                    return VideoMimeType.Wmv3;
                case VideoCodec.Wmv1:
                case VideoCodec.Wmv2:
                default:
                    throw new ArgumentOutOfRangeException(
                        $"No mapping from Juvo video codec {videoCodec} to ESPlayer aideo codec");
            }
        }

        internal static AudioMimeType GetCodecMimeType(AudioCodec audioCodec)
        {
            switch (audioCodec)
            {
                case AudioCodec.Aac:
                    return AudioMimeType.Aac;
                case AudioCodec.Mp2:
                    return AudioMimeType.Mp2;
                case AudioCodec.Mp3:
                    return AudioMimeType.Mp3;
                case AudioCodec.Vorbis:
                    return AudioMimeType.Vorbis;
                case AudioCodec.PcmS16Be:
                    return AudioMimeType.PcmS16be;
                case AudioCodec.PcmS24Be:
                    return AudioMimeType.PcmS24be;
                case AudioCodec.Eac3:
                    return AudioMimeType.Eac3;

                case AudioCodec.Ac3:
                    return AudioMimeType.Ac3;
                case AudioCodec.Pcm:
                case AudioCodec.Flac:
                case AudioCodec.AmrNb:
                case AudioCodec.AmrWb:
                case AudioCodec.PcmMulaw:
                case AudioCodec.GsmMs:
                case AudioCodec.Opus:
                case AudioCodec.Wmav1:
                case AudioCodec.Wmav2:
                default:
                    throw new ArgumentOutOfRangeException(
                        $"No mapping from Juvo audio codec {audioCodec} to ESPlayer audio codec");
            }
        }

        private static unsafe byte[] ToManagedBuffer(this INativeDataStorage storage)
        {
            var managedBuffer = new byte[storage.Length];
            Marshal.Copy((IntPtr)storage.Data, managedBuffer, 0, storage.Length);
            return managedBuffer;
        }

        internal static ESPacket ToEsPacket(this Packet packet)
        {
            byte[] buffer;
            if (packet.Storage is IManagedDataStorage dataStorage)
                buffer = dataStorage.Data;
            else
                buffer = ((INativeDataStorage)packet.Storage).ToManagedBuffer();
            return new ESPacket
            {
                type = packet.StreamType.EsStreamType(),
                pts = packet.Pts.TotalNanoseconds(),
                duration = packet.Duration.TotalNanoseconds(),
                buffer = buffer
            };
        }

        internal static ESHandlePacket ToEsHandlePacket(this DecryptedPacket packet)
        {
            return new ESHandlePacket
            {
                type = packet.StreamType.EsStreamType(),
                pts = packet.Pts.TotalNanoseconds(),
                duration = packet.Duration.TotalNanoseconds(),
                handle = packet.Handle.handle,
                handleSize = packet.Handle.size
            };
        }

        internal static StreamType EsStreamType(this Common.StreamType juvoStreamType)
        {
            return juvoStreamType == Common.StreamType.Video
                ? StreamType.Video
                : StreamType.Audio;
        }

        internal static TimeSpan FromNano(this ulong nanoTime)
        {
            return TimeSpan.FromMilliseconds(nanoTime / 1000000D);
        }

        internal static ulong TotalNanoseconds(this TimeSpan clock)
        {
            return (ulong)(clock.TotalMilliseconds * 1000000);
        }

        private class VideoConfiguration
        {
            public VideoCodec Codec { get; internal set; }
            public int Fps { get; internal set; }
            public int Bps { get; internal set; }
            public int Width { get; internal set; }
            public int Height { get; internal set; }
            public bool Supported { get; internal set; }
        }

        [DllImport("/usr/lib/libesplusplayer.so", EntryPoint = "esplusplayer_set_ecore_display")]
        internal static extern int SetDisplay(IntPtr player, DisplayType type, IntPtr window, int x, int y, int w, int h);

        public static void SetDisplay(IWindow window, ESPlayer esPlayer)
        {
            switch (window)
            {
                case ElmSharpWindow elmSharpWindow:
                    esPlayer.SetDisplay(elmSharpWindow.Window);
                    break;
                case EcoreWindow ecoreWindow:
                    var player = (IntPtr)esPlayer.GetType().GetField("player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(esPlayer);

                    var error = SetDisplay(player, DisplayType.Overlay, ecoreWindow.Window, 0, 0, ecoreWindow.Width, ecoreWindow.Height);
                    if (error != 0)
                        throw new Exception();
                    break;
                default:
                    throw new ArgumentException("Unsupported window type.");
            }
        }
    }
}
