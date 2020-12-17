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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FFmpegBindings.Interop;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public unsafe class AVFormatContextWrapper : IAVFormatContext
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private AVFormatContext* formatContext;
        private AVIOContextWrapper avioContext;
        private readonly Dictionary<int, byte[]> parsedExtraDatas = new Dictionary<int, byte[]>();

        private const int MillisecondsPerSecond = 1000;

        private readonly AVRational millsBase = new AVRational
        {
            num = 1,
            den = MillisecondsPerSecond
        };

        public AVFormatContextWrapper()
        {
            formatContext = FFmpegBindings.Interop.FFmpeg.avformat_alloc_context();
            if (formatContext == null)
                throw new FFmpegException("Cannot allocate AVFormatContext");
        }

        public long ProbeSize
        {
            get => formatContext->probesize;
            set => formatContext->probesize = value;
        }

        public TimeSpan MaxAnalyzeDuration
        {
            get => TimeSpan.FromMilliseconds(formatContext->max_analyze_duration);
            set => formatContext->max_analyze_duration = (long)value.TotalMilliseconds;
        }

        public IAVIOContext AVIOContext
        {
            get => avioContext;
            set
            {
                if (value == null)
                    formatContext->pb = null;
                else
                {
                    if (value.GetType() != typeof(AVIOContextWrapper))
                        throw new FFmpegException($"Unexpected context type. Got {value.GetType()}");
                    avioContext = (AVIOContextWrapper)value;
                    formatContext->pb = avioContext.Context;
                }
            }
        }

        public TimeSpan Duration => formatContext->duration > 0
            ? TimeSpan.FromMilliseconds(formatContext->duration / 1000)
            : TimeSpan.Zero;


        public DrmInitData[] DRMInitData => GetDRMInitData();

        private DrmInitData[] GetDRMInitData()
        {
            var result = new List<DrmInitData>();
            if (formatContext->protection_system_data_count <= 0)
                return result.ToArray();
            for (uint i = 0; i < formatContext->protection_system_data_count; ++i)
            {
                var systemData = formatContext->protection_system_data[i];
                if (systemData.pssh_box_size <= 0)
                    continue;

                var drmData = new DrmInitData
                {
                    SystemId = systemData.system_id.ToArray(),
                    InitData = new byte[systemData.pssh_box_size],
                    DataType = DrmInitDataType.Pssh,
                    KeyIDs = null   // Key are embedded in DataType.
                };

                Marshal.Copy((IntPtr)systemData.pssh_box, drmData.InitData, 0, (int)systemData.pssh_box_size);
                result.Add(drmData);
            }

            return result.ToArray();
        }

        public void Open()
        {
            formatContext->flags |= FFmpegMacros.AVFMT_FLAG_CUSTOM_IO;
            Open(null);
        }

        public void Open(string url)
        {
            fixed
                (AVFormatContext** formatContextPointer = &formatContext)
            {
                var ret = FFmpegBindings.Interop.FFmpeg.avformat_open_input(formatContextPointer, url, null, null);
                if (ret != 0)
                    throw new FFmpegException("Cannot open AVFormatContext");
            }
        }

        public void FindStreamInfo()
        {
            var ret = FFmpegBindings.Interop.FFmpeg.avformat_find_stream_info(formatContext, null);
            if (ret < 0)
                throw new FFmpegException($"Could not find stream info (error code: {ret})!");
        }

        public int FindBestStream(AVMediaType mediaType)
        {
            return FFmpegBindings.Interop.FFmpeg.av_find_best_stream(formatContext, mediaType, -1, -1, null, 0);
        }

        public int FindBestBandwidthStream(AVMediaType mediaType)
        {
            ulong bandwidth = 0;
            var streamId = -1;
            for (var i = 0; i < formatContext->nb_streams; ++i)
            {
                if (formatContext->streams[i]->codecpar->codec_type != mediaType)
                    continue;
                var dict = FFmpegBindings.Interop.FFmpeg.av_dict_get(formatContext->streams[i]->metadata, "variant_bitrate", null, 0);
                if (dict == null)
                    return -1;
                var stringValue = Marshal.PtrToStringAnsi((IntPtr)dict->value);
                if (!ulong.TryParse(stringValue, out var value))
                {
                    Logger.Error($"Expected to received an ulong, but got {stringValue}");
                    continue;
                }

                if (bandwidth >= value) continue;
                streamId = i;
                bandwidth = value;
            }

            return streamId;
        }

        public void EnableStreams(int audioIdx, int videoIdx)
        {
            for (var i = 0; i < formatContext->nb_streams; ++i)
            {
                var enabled = i == audioIdx || i == videoIdx;
                formatContext->streams[i]->discard = enabled ? AVDiscard.AVDISCARD_DEFAULT : AVDiscard.AVDISCARD_ALL;
            }
        }

        public StreamConfig ReadConfig(int idx)
        {
            VerifyStreamIndex(idx);

            var stream = formatContext->streams[idx];
            switch (stream->codec->codec_type)
            {
                case AVMediaType.AVMEDIA_TYPE_AUDIO:
                    return ReadAudioConfig(stream);
                case AVMediaType.AVMEDIA_TYPE_VIDEO:
                    return ReadVideoConfig(stream);
                default:
                    throw new FFmpegException($"Unsupported stream type: {stream->codec->codec_type}");
            }
        }

        public Packet NextPacket(int[] streamIndexes)
        {
            do
            {
                var pkt = new AVPacket();
                FFmpegBindings.Interop.FFmpeg.av_init_packet(&pkt);
                pkt.data = null;
                pkt.size = 0;
                var ret = FFmpegBindings.Interop.FFmpeg.av_read_frame(formatContext, &pkt);
                if (ret == -541478725)
                    return null;
                if (ret < 0)
                    throw new FFmpegException($"Cannot get next packet. Cause {GetErrorText(ret)}");
                var streamIndex = pkt.stream_index;
                if (streamIndexes.All(index => index != streamIndex))
                    continue;

                var stream = formatContext->streams[streamIndex];
                var pts = Rescale(pkt.pts, stream);
                var dts = Rescale(pkt.dts, stream);

                var sideData = FFmpegBindings.Interop.FFmpeg.av_packet_get_side_data(&pkt,
                    AVPacketSideDataType.AV_PKT_DATA_ENCRYPT_INFO, null);
                var packet = sideData != null ? CreateEncryptedPacket(sideData) : new Packet();

                packet.StreamType = stream->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO
                    ? StreamType.Audio
                    : StreamType.Video;
                packet.Pts = pts.Ticks >= 0 ? pts : TimeSpan.Zero;
                packet.Dts = dts.Ticks >= 0 ? dts : TimeSpan.Zero;
                packet.Duration = Rescale(pkt.duration, stream);

                packet.IsKeyFrame = pkt.flags == 1;
                packet.Storage = new FFmpegDataStorage { Packet = pkt, StreamType = packet.StreamType };
                PrependExtraDataIfNeeded(packet, stream);
                return packet;
            } while (true);
        }
        
        private void PrependExtraDataIfNeeded(Packet packet, AVStream* stream)
        {
            if (!packet.IsKeyFrame || packet.StreamType != StreamType.Video)
                return;

            if (!parsedExtraDatas.ContainsKey(stream->index))
            {
                var codec = stream->codec;
                parsedExtraDatas[stream->index] = CodecExtraDataParser.Parse(codec->codec_id, codec->extradata,
                    codec->extradata_size);
            }

            var parsedExtraData = parsedExtraDatas[stream->index];
            if (parsedExtraData != null)
                packet.Prepend(parsedExtraData);
        }

        private TimeSpan Rescale(long ffmpegTime, AVStream* stream)
        {
            var rescalled = FFmpegBindings.Interop.FFmpeg.av_rescale_q(ffmpegTime, stream->time_base, millsBase);
            return TimeSpan.FromMilliseconds(rescalled);
        }

        public void Seek(int idx, TimeSpan time)
        {
            VerifyStreamIndex(idx);
            HandleSeek(idx, time);
        }

        private void VerifyStreamIndex(int idx)
        {
            if (idx < 0 || idx >= formatContext->nb_streams)
                throw new FFmpegException($"Wrong stream index! nb_streams = {formatContext->nb_streams}, idx = {idx}");
        }

        private void HandleSeek(int idx, TimeSpan time)
        {
            var (target, flags) = CalculateTargetAndFlags(idx, time);
            var ret = FFmpegBindings.Interop.FFmpeg.av_seek_frame(formatContext, idx, target, flags);

            if (ret != 0)
                throw new FFmpegException($"av_seek_frame returned {ret}");
        }

        private (long, int) CalculateTargetAndFlags(int idx, TimeSpan time)
        {
            var stream = formatContext->streams[idx];
            var target =
                FFmpegBindings.Interop.FFmpeg.av_rescale_q((long)(time.TotalMilliseconds), millsBase, stream->time_base);

            if (target < stream->first_dts)
                return (stream->first_dts, FFmpegMacros.AVSEEK_FLAG_BACKWARD);

            if (stream->index_entries != null && stream->nb_index_entries > 0)
            {
                var lastTimestamp = stream->index_entries[stream->nb_index_entries - 1].timestamp;
                if (target > lastTimestamp)
                    return (lastTimestamp, FFmpegMacros.AVSEEK_FLAG_BACKWARD);
            }

            if (stream->duration > 0 && target > stream->duration)
                return (stream->duration, FFmpegMacros.AVSEEK_FLAG_BACKWARD);

            return (target, FFmpegMacros.AVSEEK_FLAG_ANY);
        }

        private static Packet CreateEncryptedPacket(byte* sideData)
        {
            var encInfo = (AVEncInfo*)sideData;
            int subsampleCount = encInfo->subsample_count;

            var packet = new EncryptedPacket
            {
                KeyId = encInfo->kid.ToArray(),
                Iv = encInfo->iv.ToArray()
            };
            if (subsampleCount <= 0)
                return packet;
            packet.Subsamples = new EncryptedPacket.Subsample[subsampleCount];

            // structure has sequential layout and the last element is an array
            // due to marshalling error we need to define this as single element
            // so to read this as an array we need to get a pointer to first element
            var subsamples = &encInfo->subsamples;
            for (var i = 0; i < subsampleCount; ++i)
            {
                packet.Subsamples[i].ClearData = subsamples[i].bytes_of_clear_data;
                packet.Subsamples[i].EncData = subsamples[i].bytes_of_enc_data;
            }

            return packet;
        }

        private StreamConfig ReadAudioConfig(AVStream* stream)
        {
            var config = new AudioStreamConfig();
            var sampleFormat = (AVSampleFormat)stream->codecpar->format;
            config.Codec = ConvertAudioCodec(stream->codecpar->codec_id);
            if (stream->codecpar->bits_per_coded_sample > 0)
                config.BitsPerChannel = stream->codecpar->bits_per_coded_sample;
            else
            {
                config.BitsPerChannel = FFmpegBindings.Interop.FFmpeg.av_get_bytes_per_sample(sampleFormat) * 8;
                config.BitsPerChannel /= stream->codecpar->channels;
            }

            config.ChannelLayout = stream->codecpar->channels;
            config.SampleRate = stream->codecpar->sample_rate;
            if (stream->codecpar->extradata_size > 0)
            {
                config.CodecExtraData = new byte[stream->codecpar->extradata_size];
                Marshal.Copy((IntPtr)stream->codecpar->extradata, config.CodecExtraData, 0,
                    stream->codecpar->extradata_size);
            }

            config.BitRate = stream->codecpar->bit_rate;
            return config;
        }

        private StreamConfig ReadVideoConfig(AVStream* stream)
        {
            var config = new VideoStreamConfig
            {
                Codec = ConvertVideoCodec(stream->codecpar->codec_id),
                CodecProfile = stream->codecpar->profile,
                Size = new Size(stream->codecpar->width, stream->codecpar->height),
                FrameRateNum = stream->r_frame_rate.num,
                FrameRateDen = stream->r_frame_rate.den,
                BitRate = stream->codecpar->bit_rate
            };
            if (stream->codecpar->extradata_size > 0)
            {
                config.CodecExtraData = new byte[stream->codecpar->extradata_size];
                Marshal.Copy((IntPtr)stream->codecpar->extradata, config.CodecExtraData, 0,
                    stream->codecpar->extradata_size);
            }

            return config;
        }

        private static string GetErrorText(int returnCode)
        {
            const int errorBufferSize = 1024;
            var errorBuffer = new byte[errorBufferSize];
            try
            {
                fixed (byte* errbuf = errorBuffer)
                {
                    FFmpegBindings.Interop.FFmpeg.av_strerror(returnCode, errbuf, errorBufferSize);
                }
            }
            catch (Exception)
            {
                return "";
            }

            return Encoding.UTF8.GetString(errorBuffer);
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private void ReleaseUnmanagedResources()
        {
            if (formatContext == null) return;
            fixed (AVFormatContext** formatContextPointer = &formatContext)
            {
                FFmpegBindings.Interop.FFmpeg.avformat_close_input(formatContextPointer);
            }
        }

        ~AVFormatContextWrapper()
        {
            ReleaseUnmanagedResources();
        }

        private static AudioCodec ConvertAudioCodec(AVCodecID codec)
        {
            switch (codec)
            {
                case AVCodecID.AV_CODEC_ID_AAC:
                    return AudioCodec.AAC;
                case AVCodecID.AV_CODEC_ID_MP2:
                    return AudioCodec.MP2;
                case AVCodecID.AV_CODEC_ID_MP3:
                    return AudioCodec.MP3;
                case AVCodecID.AV_CODEC_ID_VORBIS:
                    return AudioCodec.VORBIS;
                case AVCodecID.AV_CODEC_ID_FLAC:
                    return AudioCodec.FLAC;
                case AVCodecID.AV_CODEC_ID_AMR_NB:
                    return AudioCodec.AMR_NB;
                case AVCodecID.AV_CODEC_ID_AMR_WB:
                    return AudioCodec.AMR_WB;
                case AVCodecID.AV_CODEC_ID_PCM_MULAW:
                    return AudioCodec.PCM_MULAW;
                case AVCodecID.AV_CODEC_ID_GSM_MS:
                    return AudioCodec.GSM_MS;
                case AVCodecID.AV_CODEC_ID_PCM_S16BE:
                    return AudioCodec.PCM_S16BE;
                case AVCodecID.AV_CODEC_ID_PCM_S24BE:
                    return AudioCodec.PCM_S24BE;
                case AVCodecID.AV_CODEC_ID_OPUS:
                    return AudioCodec.OPUS;
                case AVCodecID.AV_CODEC_ID_EAC3:
                    return AudioCodec.EAC3;
                case AVCodecID.AV_CODEC_ID_DTS:
                    return AudioCodec.DTS;
                case AVCodecID.AV_CODEC_ID_AC3:
                    return AudioCodec.AC3;
                case AVCodecID.AV_CODEC_ID_WMAV1:
                    return AudioCodec.WMAV1;
                case AVCodecID.AV_CODEC_ID_WMAV2:
                    return AudioCodec.WMAV2;
                default:
                    throw new Exception("Unsupported codec: " + codec);
            }
        }

        private static VideoCodec ConvertVideoCodec(AVCodecID codec)
        {
            switch (codec)
            {
                case AVCodecID.AV_CODEC_ID_H264:
                    return VideoCodec.H264;
                case AVCodecID.AV_CODEC_ID_HEVC:
                    return VideoCodec.H265;
                case AVCodecID.AV_CODEC_ID_THEORA:
                    return VideoCodec.THEORA;
                case AVCodecID.AV_CODEC_ID_MPEG4:
                    return VideoCodec.MPEG4;
                case AVCodecID.AV_CODEC_ID_VP8:
                    return VideoCodec.VP8;
                case AVCodecID.AV_CODEC_ID_VP9:
                    return VideoCodec.VP9;
                case AVCodecID.AV_CODEC_ID_MPEG2VIDEO:
                    return VideoCodec.MPEG2;
                case AVCodecID.AV_CODEC_ID_VC1:
                    return VideoCodec.VC1;
                case AVCodecID.AV_CODEC_ID_WMV1:
                    return VideoCodec.WMV1;
                case AVCodecID.AV_CODEC_ID_WMV2:
                    return VideoCodec.WMV2;
                case AVCodecID.AV_CODEC_ID_WMV3:
                    return VideoCodec.WMV3;
                case AVCodecID.AV_CODEC_ID_H263:
                    return VideoCodec.H263;
                case AVCodecID.AV_CODEC_ID_INDEO3:
                    return VideoCodec.INDEO3;
                default:
                    throw new Exception("Unsupported codec: " + codec);
            }
        }
    }
}