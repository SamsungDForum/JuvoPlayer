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
using JuvoPlayer.Dash.MPD;
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
                MimeType = MimeType.VideoMp4,
                Fps = 60,
                Bps = 60 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                MimeType = MimeType.VideoH263,
                Fps = 30,
                Bps = 30 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                MimeType = MimeType.VideoH264,
                Fps = 30,
                Bps = 60 * 1000000,
                Width = 4096,
                Height = 2160,
                Supported = true
            },
            new VideoConfiguration
            {
                MimeType = MimeType.VideoH264,
                Fps = 60,
                Bps = 60 * 1000000,
                Width = 3840,
                Height = 2160,
                Supported = true
            },
            new VideoConfiguration
            {
                MimeType = MimeType.VideoH265,
                Fps = 60,
                Bps = 80 * 1000000,
                Width = 4096,
                Height = 2160,
                Supported = true
            },
            new VideoConfiguration
            {
                MimeType = MimeType.VideoMpeg2,
                Fps = 60,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                MimeType = MimeType.VideoTheora,
                Fps = 30,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = false
            },
            new VideoConfiguration
            {
                MimeType = MimeType.VideoVc1,
                Fps = 60,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                MimeType = MimeType.VideoVp8,
                Fps = 60,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = true
            },
            new VideoConfiguration
            {
                MimeType = MimeType.VideoVp9,
                Fps = 60,
                Bps = 20 * 1000000,
                Width = 1920,
                Height = 1080,
                Supported = false
            },
            new VideoConfiguration
            {
                MimeType = MimeType.VideoWmv,
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
                                       string.Equals(videoConfig.MimeType, entry.MimeType) && entry.Fps >= videoConfig.FrameRate) ??
                                   fpsOrderedConfigs.LastOrDefault(entry => string.Equals(videoConfig.MimeType, entry.MimeType));

            if (configParameters == null)
                throw new ArgumentException($"Unsupported mime type {videoConfig.MimeType}");

            return new VideoStreamInfo
            {
                codecData = videoConfig.CodecExtraData,
                mimeType = ConvertAsEsVideoMimeType(videoConfig.MimeType),
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
                mimeType = ConvertAsEsAudioMimeType(audioConfig.MimeType),
                sampleRate = audioConfig.SampleRate,
                channels = audioConfig.ChannelLayout
            };
        }

        internal static VideoMimeType ConvertAsEsVideoMimeType(string mimeType)
        {
            if (MimeType.VideoH263.Equals(mimeType))
                return VideoMimeType.H263;
            if (MimeType.VideoH264.Equals(mimeType))
                return VideoMimeType.H264;
            if (MimeType.VideoH265.Equals(mimeType))
                return VideoMimeType.Hevc;
            if (MimeType.VideoMpeg2.Equals(mimeType))
                return VideoMimeType.Mpeg2;
            if (MimeType.VideoMp4.Equals(mimeType))
                return VideoMimeType.Mpeg4;
            if (MimeType.VideoVp8.Equals(mimeType))
                return VideoMimeType.Vp8;
            if (MimeType.VideoVp9.Equals(mimeType))
                return VideoMimeType.Vp9;
            if (MimeType.VideoWmv.Equals(mimeType))
                return VideoMimeType.Wmv3;
            throw new ArgumentOutOfRangeException(
                $"No mapping from Juvo video mime type {mimeType} to ESPlayer video mime type");
        }

        internal static AudioMimeType ConvertAsEsAudioMimeType(string mimeType)
        {
            if (MimeType.AudioAac.Equals(mimeType))
                return AudioMimeType.Aac;
            if (MimeType.AudioMpeg.Equals(mimeType))
                return AudioMimeType.Mp2;
            if (MimeType.AudioMp3.Equals(mimeType))
                return AudioMimeType.Mp3;
            if (MimeType.AudioVorbis.Equals(mimeType))
                return AudioMimeType.Vorbis;
            if (MimeType.AudioRaw.Equals(mimeType))
                return AudioMimeType.PcmS16le;
            if (MimeType.AudioEac3.Equals(mimeType))
                return AudioMimeType.Eac3;
            if (MimeType.AudioAc3.Equals(mimeType))
                return AudioMimeType.Ac3;
            throw new ArgumentOutOfRangeException(
                $"No mapping from Juvo audio mime type {mimeType} to ESPlayer audio mime type");
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
            public string MimeType { get; internal set; }
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
