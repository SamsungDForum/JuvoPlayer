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
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers.FFmpeg.Interop;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public unsafe class AvFormatContextWrapper : IAvFormatContext
    {
        private const int MillisecondsPerSecond = 1000;
        private static readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly AVRational _millsBase = new AVRational {num = 1, den = MillisecondsPerSecond};

        private readonly Dictionary<int, byte[]> _parsedExtraDatas = new Dictionary<int, byte[]>();
        private AvioContextWrapper _avioContext;

        private AVFormatContext* _formatContext;

        public AvFormatContextWrapper()
        {
            _formatContext = Interop.FFmpeg.avformat_alloc_context();
            if (_formatContext == null)
                throw new FFmpegException("Cannot allocate AVFormatContext");
        }

        public long ProbeSize
        {
            get => _formatContext->probesize;
            set => _formatContext->probesize = value;
        }

        public TimeSpan MaxAnalyzeDuration
        {
            get => TimeSpan.FromMilliseconds(_formatContext->max_analyze_duration);
            set => _formatContext->max_analyze_duration = (long) value.TotalMilliseconds;
        }

        public IAvioContext AvioContext
        {
            get => _avioContext;
            set
            {
                if (value == null)
                {
                    _formatContext->pb = null;
                }
                else
                {
                    if (value.GetType() != typeof(AvioContextWrapper))
                        throw new FFmpegException($"Unexpected context type. Got {value.GetType()}");
                    _avioContext = (AvioContextWrapper) value;
                    _formatContext->pb = _avioContext.Context;
                }
            }
        }

        public TimeSpan Duration => _formatContext->duration > 0
            ? TimeSpan.FromMilliseconds(_formatContext->duration / 1000)
            : TimeSpan.Zero;

        public DrmInitData[] DrmInitData => GetDrmInitData();

        public void Open()
        {
            _formatContext->flags |= FFmpegMacros.AVFMT_FLAG_CUSTOM_IO;
            Open(null);
        }

        public void Open(string url)
        {
            fixed
                (AVFormatContext** formatContextPointer = &_formatContext)
            {
                var ret = Interop.FFmpeg.avformat_open_input(formatContextPointer, url, null, null);
                if (ret != 0)
                    throw new FFmpegException("Cannot open AVFormatContext");
            }
        }

        public void FindStreamInfo()
        {
            var ret = Interop.FFmpeg.avformat_find_stream_info(_formatContext, null);
            if (ret < 0)
                throw new FFmpegException($"Could not find stream info (error code: {ret})!");
        }

        public int FindBestStream(AVMediaType mediaType)
        {
            return Interop.FFmpeg.av_find_best_stream(_formatContext, mediaType, -1, -1, null, 0);
        }

        public int FindBestBandwidthStream(AVMediaType mediaType)
        {
            ulong bandwidth = 0;
            var streamId = -1;
            for (var i = 0; i < _formatContext->nb_streams; ++i)
            {
                if (_formatContext->streams[i]->codecpar->codec_type != mediaType)
                    continue;
                var dict = Interop.FFmpeg.av_dict_get(_formatContext->streams[i]->metadata, "variant_bitrate", null, 0);
                if (dict == null)
                    return -1;
                var stringValue = Marshal.PtrToStringAnsi((IntPtr) dict->value);
                if (!ulong.TryParse(stringValue, out var value))
                {
                    _logger.Error($"Expected to received an ulong, but got {stringValue}");
                    continue;
                }

                if (bandwidth >= value)
                    continue;
                streamId = i;
                bandwidth = value;
            }

            return streamId;
        }

        public void EnableStreams(int audioIdx, int videoIdx)
        {
            for (var i = 0; i < _formatContext->nb_streams; ++i)
            {
                var enabled = i == audioIdx || i == videoIdx;
                _formatContext->streams[i]->discard = enabled ? AVDiscard.AVDISCARD_DEFAULT : AVDiscard.AVDISCARD_ALL;
            }
        }

        public StreamConfig ReadConfig(int idx)
        {
            VerifyStreamIndex(idx);

            var stream = _formatContext->streams[idx];
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
                var pkt = default(AVPacket);
                Interop.FFmpeg.av_init_packet(&pkt);
                pkt.data = null;
                pkt.size = 0;
                var ret = Interop.FFmpeg.av_read_frame(_formatContext, &pkt);
                if (ret == -541478725)
                    return null;
                if (ret < 0)
                    throw new FFmpegException($"Cannot get next packet. Cause {GetErrorText(ret)}");
                var streamIndex = pkt.stream_index;
                if (streamIndexes.All(index => index != streamIndex))
                    continue;

                var stream = _formatContext->streams[streamIndex];
                var pts = Rescale(pkt.pts, stream);
                var dts = Rescale(pkt.dts, stream);

                var sideData = Interop.FFmpeg.av_packet_get_side_data(&pkt,
                    AVPacketSideDataType.AV_PKT_DATA_ENCRYPT_INFO, null);
                var packet = sideData != null ? CreateEncryptedPacket(sideData) : new Packet();

                packet.StreamType = stream->codec->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO
                    ? StreamType.Audio
                    : StreamType.Video;
                packet.Pts = pts.Ticks >= 0 ? pts : TimeSpan.Zero;
                packet.Dts = dts.Ticks >= 0 ? dts : TimeSpan.Zero;
                packet.Duration = Rescale(pkt.duration, stream);

                packet.IsKeyFrame = pkt.flags == 1;
                packet.Storage = new FFmpegDataStorage {Packet = pkt, StreamType = packet.StreamType};
                PrependExtraDataIfNeeded(packet, stream);
                return packet;
            } while (true);
        }

        public void Seek(int idx, TimeSpan time)
        {
            VerifyStreamIndex(idx);
            HandleSeek(idx, time);
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private DrmInitData[] GetDrmInitData()
        {
            var result = new List<DrmInitData>();
            if (_formatContext->protection_system_data_count <= 0)
                return result.ToArray();
            for (uint i = 0; i < _formatContext->protection_system_data_count; ++i)
            {
                var systemData = _formatContext->protection_system_data[i];
                if (systemData.pssh_box_size <= 0)
                    continue;

                var drmData = new DrmInitData
                {
                    SystemId = systemData.system_id.ToArray(),
                    InitData = new byte[systemData.pssh_box_size],
                    DataType = DrmInitDataType.Pssh,
                    KeyIDs = null // Key are embedded in DataType.
                };

                Marshal.Copy((IntPtr) systemData.pssh_box, drmData.InitData, 0, (int) systemData.pssh_box_size);
                result.Add(drmData);
            }

            return result.ToArray();
        }

        private void PrependExtraDataIfNeeded(Packet packet, AVStream* stream)
        {
            if (!packet.IsKeyFrame || packet.StreamType != StreamType.Video)
                return;

            if (!_parsedExtraDatas.ContainsKey(stream->index))
            {
                var codec = stream->codec;
                _parsedExtraDatas[stream->index] = CodecExtraDataParser.Parse(codec->codec_id, codec->extradata,
                    codec->extradata_size);
            }

            var parsedExtraData = _parsedExtraDatas[stream->index];
            if (parsedExtraData != null)
                packet.Prepend(parsedExtraData);
        }

        private TimeSpan Rescale(long ffmpegTime, AVStream* stream)
        {
            var rescalled = Interop.FFmpeg.av_rescale_q(ffmpegTime, stream->time_base, _millsBase);
            return TimeSpan.FromMilliseconds(rescalled);
        }

        private void VerifyStreamIndex(int idx)
        {
            if (idx < 0 || idx >= _formatContext->nb_streams)
            {
                throw new FFmpegException(
                    $"Wrong stream index! nb_streams = {_formatContext->nb_streams}, idx = {idx}");
            }
        }

        private void HandleSeek(int idx, TimeSpan time)
        {
            var (target, flags) = CalculateTargetAndFlags(idx, time);
            var ret = Interop.FFmpeg.av_seek_frame(_formatContext, idx, target, flags);

            if (ret != 0)
                throw new FFmpegException($"av_seek_frame returned {ret}");
        }

        private (long, int) CalculateTargetAndFlags(int idx, TimeSpan time)
        {
            var stream = _formatContext->streams[idx];
            var target =
                Interop.FFmpeg.av_rescale_q((long) time.TotalMilliseconds, _millsBase, stream->time_base);

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
            var encInfo = (AVEncInfo*) sideData;
            int subsampleCount = encInfo->subsample_count;

            var packet = new EncryptedPacket {KeyId = encInfo->kid.ToArray(), Iv = encInfo->iv.ToArray()};
            if (subsampleCount <= 0)
                return packet;
            packet.SubSamples = new EncryptedPacket.Subsample[subsampleCount];

            // structure has sequential layout and the last element is an array
            // due to marshalling error we need to define this as single element
            // so to read this as an array we need to get a pointer to first element
            var subsamples = &encInfo->subsamples;
            for (var i = 0; i < subsampleCount; ++i)
            {
                packet.SubSamples[i].ClearData = subsamples[i].bytes_of_clear_data;
                packet.SubSamples[i].EncData = subsamples[i].bytes_of_enc_data;
            }

            return packet;
        }

        private StreamConfig ReadAudioConfig(AVStream* stream)
        {
            var config = new AudioStreamConfig();
            var sampleFormat = (AVSampleFormat) stream->codecpar->format;
            config.Codec = ConvertAudioCodec(stream->codecpar->codec_id);
            if (stream->codecpar->bits_per_coded_sample > 0)
            {
                config.BitsPerChannel = stream->codecpar->bits_per_coded_sample;
            }
            else
            {
                config.BitsPerChannel = Interop.FFmpeg.av_get_bytes_per_sample(sampleFormat) * 8;
                config.BitsPerChannel /= stream->codecpar->channels;
            }

            config.ChannelLayout = stream->codecpar->channels;
            config.SampleRate = stream->codecpar->sample_rate;
            if (stream->codecpar->extradata_size > 0)
            {
                config.CodecExtraData = new byte[stream->codecpar->extradata_size];
                Marshal.Copy((IntPtr) stream->codecpar->extradata, config.CodecExtraData, 0,
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
                Marshal.Copy((IntPtr) stream->codecpar->extradata, config.CodecExtraData, 0,
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
                    Interop.FFmpeg.av_strerror(returnCode, errbuf, errorBufferSize);
                }
            }
            catch (Exception)
            {
                return "";
            }

            return Encoding.UTF8.GetString(errorBuffer);
        }

        private void ReleaseUnmanagedResources()
        {
            if (_formatContext == null)
                return;
            fixed (AVFormatContext** formatContextPointer = &_formatContext)
            {
                Interop.FFmpeg.avformat_close_input(formatContextPointer);
            }
        }

        ~AvFormatContextWrapper()
        {
            ReleaseUnmanagedResources();
        }

        private static AudioCodec ConvertAudioCodec(AVCodecID codec)
        {
            switch (codec)
            {
                case AVCodecID.AV_CODEC_ID_AAC:
                    return AudioCodec.Aac;
                case AVCodecID.AV_CODEC_ID_MP2:
                    return AudioCodec.Mp2;
                case AVCodecID.AV_CODEC_ID_MP3:
                    return AudioCodec.Mp3;
                case AVCodecID.AV_CODEC_ID_VORBIS:
                    return AudioCodec.Vorbis;
                case AVCodecID.AV_CODEC_ID_FLAC:
                    return AudioCodec.Flac;
                case AVCodecID.AV_CODEC_ID_AMR_NB:
                    return AudioCodec.AmrNb;
                case AVCodecID.AV_CODEC_ID_AMR_WB:
                    return AudioCodec.AmrWb;
                case AVCodecID.AV_CODEC_ID_PCM_MULAW:
                    return AudioCodec.PcmMulaw;
                case AVCodecID.AV_CODEC_ID_GSM_MS:
                    return AudioCodec.GsmMs;
                case AVCodecID.AV_CODEC_ID_PCM_S16BE:
                    return AudioCodec.PcmS16Be;
                case AVCodecID.AV_CODEC_ID_PCM_S24BE:
                    return AudioCodec.PcmS24Be;
                case AVCodecID.AV_CODEC_ID_OPUS:
                    return AudioCodec.Opus;
                case AVCodecID.AV_CODEC_ID_EAC3:
                    return AudioCodec.Eac3;
                case AVCodecID.AV_CODEC_ID_DTS:
                    return AudioCodec.Dts;
                case AVCodecID.AV_CODEC_ID_AC3:
                    return AudioCodec.Ac3;
                case AVCodecID.AV_CODEC_ID_WMAV1:
                    return AudioCodec.Wmav1;
                case AVCodecID.AV_CODEC_ID_WMAV2:
                    return AudioCodec.Wmav2;
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
                    return VideoCodec.Theora;
                case AVCodecID.AV_CODEC_ID_MPEG4:
                    return VideoCodec.Mpeg4;
                case AVCodecID.AV_CODEC_ID_VP8:
                    return VideoCodec.Vp8;
                case AVCodecID.AV_CODEC_ID_VP9:
                    return VideoCodec.Vp9;
                case AVCodecID.AV_CODEC_ID_MPEG2VIDEO:
                    return VideoCodec.Mpeg2;
                case AVCodecID.AV_CODEC_ID_VC1:
                    return VideoCodec.Vc1;
                case AVCodecID.AV_CODEC_ID_WMV1:
                    return VideoCodec.Wmv1;
                case AVCodecID.AV_CODEC_ID_WMV2:
                    return VideoCodec.Wmv2;
                case AVCodecID.AV_CODEC_ID_WMV3:
                    return VideoCodec.Wmv3;
                case AVCodecID.AV_CODEC_ID_H263:
                    return VideoCodec.H263;
                case AVCodecID.AV_CODEC_ID_INDEO3:
                    return VideoCodec.Indeo3;
                default:
                    throw new Exception("Unsupported codec: " + codec);
            }
        }
    }
}