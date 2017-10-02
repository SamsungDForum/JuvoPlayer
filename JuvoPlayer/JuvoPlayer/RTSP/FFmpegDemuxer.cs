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
using JuvoPlayer.FFmpeg;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Tizen;
using Tizen.Applications;

namespace JuvoPlayer.RTSP
{
    public class FFmpegDemuxer : IDemuxer
    {
        public event Common.StreamConfigReady StreamConfigReady;
        public event Common.StreamPacketReady StreamPacketReady;

        private int bufferSize = 128 * 1024;
        private unsafe byte* buffer = null;
        private unsafe AVFormatContext* formatContext = null;
        private unsafe AVIOContext* ioContext = null;
        int audio_idx = -1;
        int video_idx = -1;

        private ISharedBuffer dataBuffer;
        public unsafe FFmpegDemuxer(ISharedBuffer dataBuffer, string libPath)
        {
            this.dataBuffer = dataBuffer ?? throw new ArgumentNullException("dataBuffer cannot be null");
            try
            {
                FFmpeg.FFmpeg.Initialize(libPath);
                FFmpeg.FFmpeg.av_register_all(); // TODO(g.skowinski): Is registering multiple times unwanted or doesn't it matter?
            }
            catch(Exception)
            {
                Log.Info("JuvoPlayer", "Could not load and register FFmpeg library!");
                throw;
            }
        }

        public unsafe void Start()
        {
            Log.Info("JuvoPlayer", "StartDemuxer!");

            Task.Factory.StartNew(DemuxTask); // Potentially time-consuming part of initialization and demuxation loop will be executed on a detached thread.
        }

        private unsafe string GetErrorText(int returnCode) // -1094995529 = -0x41444E49 = "INDA" = AVERROR_INVALID_DATA
        {
            const int errorBufferSize = 1024;
            byte[] errorBuffer = new byte[errorBufferSize];
            try
            {
                fixed (byte* errbuf = errorBuffer)
                {
                    FFmpeg.FFmpeg.av_strerror(returnCode, errbuf, errorBufferSize);
                }
            }
            catch (Exception)
            {
                return "";
            }
            return System.Text.Encoding.UTF8.GetString(errorBuffer);
        }

        unsafe private void Init()
        {
            int ret = -1;
            Log.Info("JuvoPlayer", "INIT");

            buffer = (byte*)FFmpeg.FFmpeg.av_malloc((ulong)bufferSize);
            formatContext = FFmpeg.FFmpeg.avformat_alloc_context();
            ioContext = FFmpeg.FFmpeg.avio_alloc_context(buffer,
                                                         bufferSize,
                                                         0,
                                                         (void*)GCHandle.ToIntPtr(GCHandle.Alloc(dataBuffer)),
                                                         (avio_alloc_context_read_packet)ReadPacket,
                                                         (avio_alloc_context_write_packet)WritePacket,
                                                         (avio_alloc_context_seek)Seek);
            ioContext->seekable = 0;
            ioContext->write_flag = 0;

            formatContext->probesize = bufferSize;
            formatContext->max_analyze_duration = 10 * 1000000;
            formatContext->flags |= FFmpegMacros.AVFMT_FLAG_CUSTOM_IO;
            formatContext->pb = ioContext;

            AVProbeData probeData;
            probeData.buf = buffer;
            probeData.buf_size = bufferSize;

            if (ioContext == null || formatContext == null)
            {
                DeallocFFmpeg();
                Log.Info("JuvoPlayer", "Could not create FFmpeg context.!");

                throw new Exception("Could not create FFmpeg context.");
            }

            fixed (AVFormatContext** formatContextPointer = &formatContext)
            {
                ret = FFmpeg.FFmpeg.avformat_open_input(formatContextPointer, "dummy", null, null);
                //ret = FFmpeg.FFmpeg.avformat_open_input(formatContextPointer, "rtsp://192.168.137.200/2kkk.ts", null, null);
            }
            if (ret != 0)
            {
                Log.Info("JuvoPlayer", "Could not parse input data: " + GetErrorText(ret));

                DeallocFFmpeg();
                //FFmpeg.av_free(buffer); // should be freed by avformat_open_input if i recall correctly
                throw new Exception("Could not parse input data: " + GetErrorText(ret));
            }

            ret = FFmpeg.FFmpeg.avformat_find_stream_info(formatContext, null);
            if (ret < 0)
            {
                Log.Info("JuvoPlayer", "Could not find stream info (error code: " + ret.ToString() + ")!");

                DeallocFFmpeg();
                throw new Exception("Could not find stream info (error code: " + ret.ToString() + ")!");
            }

            audio_idx = FFmpeg.FFmpeg.av_find_best_stream(formatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
            video_idx = FFmpeg.FFmpeg.av_find_best_stream(formatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (audio_idx < 0 && video_idx < 0)
            {
                DeallocFFmpeg();
                Log.Info("JuvoPlayer", "Could not find video or audio stream: " + audio_idx.ToString() + "      " + video_idx.ToString());
                throw new Exception("Could not find video or audio stream!");
            }

            Log.Info("JuvoPlayer", "streamids: " + audio_idx.ToString() + "      " + video_idx.ToString());
        }

        unsafe private void DemuxTask()
        {
            // Finish more time-consuming init things
            Init();

            ReadAudioConfig();
            ReadVideoConfig();

            const int kMicrosecondsPerSecond = 1000000;
            const double kOneMicrosecond = 1.0 / kMicrosecondsPerSecond;
            AVRational kMicrosBase = new AVRational
            {
                num = 1,
                den = kMicrosecondsPerSecond
            };
            AVPacket pkt;
            bool parse = true;

            while (parse)
            {
                FFmpeg.FFmpeg.av_init_packet(&pkt);
                if (FFmpeg.FFmpeg.av_read_frame(formatContext, &pkt) >= 0)
                {
                    if (pkt.stream_index == audio_idx || pkt.stream_index == video_idx)
                    {
                        // TODO(g.skowinski): Write output data to packet object/stream

                        AVStream* s = formatContext->streams[pkt.stream_index]; // :784
                        var data = pkt.data; // :781
                        var data_size = pkt.size; // :781

                        var pts = FFmpeg.FFmpeg.av_rescale_q(pkt.pts, s->time_base, kMicrosBase) * kOneMicrosecond; // :789
                        var dts = FFmpeg.FFmpeg.av_rescale_q(pkt.dts, s->time_base, kMicrosBase) * kOneMicrosecond; // :790

                        Log.Info("JuvoPlayer", "data size: " + data_size.ToString() + "; pts: " + pts.ToString() + "; dts: " + dts.ToString());

                        var duration = FFmpeg.FFmpeg.av_rescale_q(pkt.duration, s->time_base, kMicrosBase) * kOneMicrosecond; // :786
                        var key_frame = (pkt.flags == 1); // :787
                        var timestamp = 0; // :791-801 - should I check for timestamp/pts/dts inconsistencies?

                        // AVEncInfo* enc_info = (AVEncInfo*)FFmpeg.FFmpeg.av_packet_get_side_data(pkt, AV_PKT_DATA_ENCRYPT_INFO, null); // :807
                        // :808-816

                        /*if(video_idx != -1 && pkt.stream_index == video_idx)
                        {
                            FFmpeg.FFmpeg.avcodec_decode_video2();
                        }*/
                    }
                }
                else
                {
                    parse = false;
                }

                FFmpeg.FFmpeg.av_packet_unref(&pkt);
            }
        }

        unsafe private void DeallocFFmpeg()
        {
            if (formatContext != null)
            {
                fixed (AVFormatContext** formatContextPointer = &formatContext)
                {
                    FFmpeg.FFmpeg.avformat_close_input(formatContextPointer);
                }
                FFmpeg.FFmpeg.avformat_free_context(formatContext);
                formatContext = null;
            }
            if (buffer != null)
            {
                //FFmpeg.FFmpeg.av_free(buffer); // TODO(g.skowinski): causes segfault - investigate
                buffer = null;
            }
        }

        unsafe ~FFmpegDemuxer()
        {
            DeallocFFmpeg();
        }

        unsafe private void ReadAudioConfig()
        {
            AVStream* s = formatContext->streams[audio_idx];
            AudioStreamConfig config = new AudioStreamConfig();

            AVSampleFormat sampleFormat = (AVSampleFormat)s->codecpar->format;
            config.Codec = ConvertAudioCodec(s->codecpar->codec_id);
            //config.sample_format = ConvertSampleFormat(sample_format);
            if (s->codecpar->bits_per_coded_sample > 0)
            {
                config.BitsPerChannel = s->codecpar->bits_per_coded_sample;
            }
            else
            {
                config.BitsPerChannel = FFmpeg.FFmpeg.av_get_bytes_per_sample(sampleFormat) * 8;
                config.BitsPerChannel /= s->codecpar->channels;
            }
            config.ChannelLayout = s->codecpar->channels;
            config.SampleRate = s->codecpar->sample_rate;

            StreamConfigReady(config);
        }

        unsafe private void ReadVideoConfig()
        {
            AVStream* s = formatContext->streams[audio_idx];
            VideoStreamConfig config = new VideoStreamConfig();
            config.Codec = ConvertVideoCodec(s->codecpar->codec_id, s->codecpar->profile);
            config.Size = new Tizen.Multimedia.Size(s->codecpar->width, s->codecpar->height);
            config.FrameRate = s->r_frame_rate.num / s->r_frame_rate.den;

            StreamConfigReady(config);
        }

        Tizen.Multimedia.MediaFormatAudioMimeType ConvertAudioCodec(AVCodecID codec)
        {
            switch (codec)
            {
                case AVCodecID.AV_CODEC_ID_AAC:
                    return Tizen.Multimedia.MediaFormatAudioMimeType.Aac;
                default:
                   throw new Exception("Unsupported codec: " + codec.ToString());
            }
        }

        Tizen.Multimedia.MediaFormatVideoMimeType ConvertVideoCodec(AVCodecID codec, int profile)
        {
            switch (codec)
            {
                case AVCodecID.AV_CODEC_ID_H264:
                    return ConvertH264VideoCodecProfile(profile);
                default:
                    throw new Exception("Unsupported codec: " + codec.ToString());
            }
        }

        Tizen.Multimedia.MediaFormatVideoMimeType ConvertH264VideoCodecProfile(int profile)
        {
            profile &= ~FFmpegMacros.FF_PROFILE_H264_CONSTRAINED;
            profile &= ~FFmpegMacros.FF_PROFILE_H264_INTRA;
            if (profile == FFmpegMacros.FF_PROFILE_H264_BASELINE)
                return Tizen.Multimedia.MediaFormatVideoMimeType.H264SP;
            else if (profile == FFmpegMacros.FF_PROFILE_H264_MAIN)
                return Tizen.Multimedia.MediaFormatVideoMimeType.H264MP;
            else
                return Tizen.Multimedia.MediaFormatVideoMimeType.H264HP;
        }

        public void ChangePID(int pid)
        {
            // TODO(g.skowinski): Implement.
        }

        public void Reset()
        {
            // TODO(g.skowinski): Implement.
        }

        public void Seek(double position)
        {
            // TODO(g.skowinski): Implement.
        }

        static private unsafe ISharedBuffer RetrieveSharedBufferReference(void* @opaque)
        {
            ISharedBuffer sharedBuffer;
            try
            {
                GCHandle handle = GCHandle.FromIntPtr((IntPtr)opaque);
                sharedBuffer = (ISharedBuffer)handle.Target;
            }
            catch (Exception)
            {
                Log.Info("JuvoPlayer", "Retrieveing SharedBuffer reference failed!");
                throw;
            }
            return sharedBuffer;
        }

        static private unsafe int ReadPacket(void* @opaque, byte* @buf, int @buf_size)
        {
            ISharedBuffer sharedBuffer;
            try
            {
                sharedBuffer = RetrieveSharedBufferReference(opaque);
            }
            catch(Exception)
            {
                return 0;
            }
            byte[] data = sharedBuffer.ReadData(buf_size); // SharedBuffer::ReadData(int size) is blocking - it will block until it has enough data or return less data if EOF is reached
            Marshal.Copy(data, 0, (IntPtr)buf, data.Length);
            return data.Length;
        }

        static private unsafe int WritePacket(void* @opaque, byte* @buf, int @buf_size)
        {
            // TODO(g.skowinski): Implement.
            return 0;
        }

        static private unsafe long Seek(void* @opaque, long @offset, int @whenc)
        {
            // TODO(g.skowinski): Implement.
            return 0;
        }
    }

}