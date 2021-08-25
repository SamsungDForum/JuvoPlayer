/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2021, Samsung Electronics Co., Ltd
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

using System;
using FFmpegBindings.Interop;
using JuvoPlayer.Common;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public static class FFmpegExtensions
    {
        public static int GetIndex(this StreamConfig config)
        {
            switch (config)
            {
                case FFmpegAudioStreamConfig audioConfig:
                    return audioConfig.Index;
                case FFmpegVideoStreamConfig videoConfig:
                    return videoConfig.Index;
                default:
                    throw new ArgumentException(config.ToString());
            }
        }

        public static string ConvertToMimeType(this AVCodecID codec)
        {
            switch (codec)
            {
                case AVCodecID.AV_CODEC_ID_AAC:
                    return MimeType.AudioAac;
                case AVCodecID.AV_CODEC_ID_MP2:
                    return MimeType.AudioMpeg;
                case AVCodecID.AV_CODEC_ID_MP3:
                    return MimeType.AudioMp3;
                case AVCodecID.AV_CODEC_ID_VORBIS:
                    return MimeType.AudioVorbis;
                case AVCodecID.AV_CODEC_ID_FLAC:
                    return MimeType.AudioFlac;
                case AVCodecID.AV_CODEC_ID_PCM_MULAW:
                    return MimeType.AudioRaw;
                case AVCodecID.AV_CODEC_ID_OPUS:
                    return MimeType.AudioOpus;
                case AVCodecID.AV_CODEC_ID_EAC3:
                    return MimeType.AudioEac3;
                case AVCodecID.AV_CODEC_ID_DTS:
                    return MimeType.AudioDts;
                case AVCodecID.AV_CODEC_ID_AC3:
                    return MimeType.AudioAc3;
                case AVCodecID.AV_CODEC_ID_H264:
                    return MimeType.VideoH264;
                case AVCodecID.AV_CODEC_ID_HEVC:
                    return MimeType.VideoH265;
                case AVCodecID.AV_CODEC_ID_THEORA:
                    return MimeType.VideoTheora;
                case AVCodecID.AV_CODEC_ID_MPEG4:
                    return MimeType.VideoMp4;
                case AVCodecID.AV_CODEC_ID_VP8:
                    return MimeType.VideoVp8;
                case AVCodecID.AV_CODEC_ID_VP9:
                    return MimeType.VideoVp9;
                case AVCodecID.AV_CODEC_ID_MPEG2VIDEO:
                    return MimeType.VideoMpeg2;
                case AVCodecID.AV_CODEC_ID_VC1:
                    return MimeType.VideoVc1;
                case AVCodecID.AV_CODEC_ID_WMV1:
                    return MimeType.VideoWmv;
                case AVCodecID.AV_CODEC_ID_H263:
                    return MimeType.VideoH263;
                default:
                    throw new Exception("Unsupported codec: " + codec);
            }
        }
    }
}