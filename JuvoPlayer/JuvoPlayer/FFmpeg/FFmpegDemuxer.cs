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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Tizen;
using Tizen.Applications;

namespace JuvoPlayer.FFmpeg
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
            try {
                FFmpeg.Initialize(libPath);
                FFmpeg.av_register_all(); // TODO(g.skowinski): Is registering multiple times unwanted or doesn't it matter?
            }
            catch (Exception) {
                Log.Info("JuvoPlayer", "Could not load and register FFmpeg library!");
                throw;
            }
        }

        public unsafe void Start()
        {
            Log.Info("JuvoPlayer", "StartDemuxer!");
            Task.Run(() => DemuxTask()); // Potentially time-consuming part of initialization and demuxation loop will be executed on a detached thread.
        }

        unsafe private void InitES()
        {
            int ret = -1;
            Log.Info("JuvoPlayer", "INIT");

            buffer = (byte*)FFmpeg.av_mallocz((ulong)bufferSize); // let's try AllocHGlobal later on
            ioContext = FFmpeg.avio_alloc_context(buffer,
                                                         bufferSize,
                                                         0,
                                                         (void*)GCHandle.ToIntPtr(GCHandle.Alloc(dataBuffer)), // TODO(g.skowinski): Check if allocating memory used by ffmpeg with Marshal.AllocHGlobal helps!
                                                         (avio_alloc_context_read_packet)ReadPacket,
                                                         (avio_alloc_context_write_packet)WritePacket,
                                                         (avio_alloc_context_seek)Seek);
            formatContext = FFmpeg.avformat_alloc_context(); // it was before avio_alloc_context before, but I'm changing ordering so it's like in LiveTVApp
            ioContext->seekable = 0;
            ioContext->write_flag = 0;

            formatContext->probesize = bufferSize;
            formatContext->max_analyze_duration = 10 * 1000000;
            formatContext->flags |= FFmpegMacros.AVFMT_FLAG_CUSTOM_IO;
            formatContext->pb = ioContext;

            if (ioContext == null || formatContext == null) {
                DeallocFFmpeg();
                Log.Info("JuvoPlayer", "Could not create FFmpeg context.!");

                throw new Exception("Could not create FFmpeg context.");
            }

            fixed (AVFormatContext** formatContextPointer = &formatContext) {
                ret = FFmpeg.avformat_open_input(formatContextPointer, "", null, null);
            }
            if (ret != 0) {
                Log.Info("JuvoPlayer", "Could not parse input data: " + GetErrorText(ret));

                DeallocFFmpeg();
                //FFmpeg.av_free(buffer); // should be freed by avformat_open_input if i recall correctly
                throw new Exception("Could not parse input data: " + GetErrorText(ret));
            }

            ret = FFmpeg.avformat_find_stream_info(formatContext, null);
            if (ret < 0) {
                Log.Info("JuvoPlayer", "Could not find stream info (error code: " + ret.ToString() + ")!");

                DeallocFFmpeg();
                throw new Exception("Could not find stream info (error code: " + ret.ToString() + ")!");
            }

            audio_idx = FFmpeg.av_find_best_stream(formatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
            video_idx = FFmpeg.av_find_best_stream(formatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (audio_idx < 0 && video_idx < 0) {
                DeallocFFmpeg();
                Log.Info("JuvoPlayer", "Could not find video or audio stream: " + audio_idx.ToString() + "      " + video_idx.ToString());
                throw new Exception("Could not find video or audio stream!");
            }

            Log.Info("JuvoPlayer", "streamids: " + audio_idx.ToString() + "      " + video_idx.ToString());
        }

        unsafe private void InitURL()
        {
            int ret = -1;
            Log.Info("JuvoPlayer", "INIT");

            buffer = (byte*)FFmpeg.av_mallocz((ulong)bufferSize);
            formatContext = FFmpeg.avformat_alloc_context();

            formatContext->probesize = bufferSize;
            formatContext->max_analyze_duration = 10 * 1000000;

            if (formatContext == null) {
                DeallocFFmpeg();
                Log.Info("JuvoPlayer", "Could not create FFmpeg context!");
                throw new Exception("Could not create FFmpeg context.");
            }

            fixed (AVFormatContext** formatContextPointer = &formatContext) {
                string url = "";

                // LOCAL CONTENT
                //url = "/opt/media/USBDriveA1/test2.ts";

                // RTSP CONTENT
                url = "rtsp://192.168.137.3/test2.ts"; // test.mp4 file converted with "ffmpeg -i test.mp4 -acodec copy -vcodec copy test.ts" command and served via rtsp using live555MediaServer_v74.exe

                // HLS CONTENT
                //url = "http://bitdash-a.akamaihd.net/content/MI201109210084_1/m3u8s/f08e80da-bf1d-4e3d-8899-f0f6155f6efa.m3u8"; // changed from https to http - single frame is displayed
                //url = "http://devimages.apple.com/iphone/samples/bipbop/gear4/prog_index.m3u8"; // single frame is displayed

                ret = FFmpeg.avformat_open_input(formatContextPointer, url, null, null);
                Log.Info("JuvoPlayer", "avformat_open_input(" + url + ") = " + (ret == 0 ? "ok" : ret.ToString() + " (" + GetErrorText(ret) + ")"));
            }

            ret = FFmpeg.avformat_find_stream_info(formatContext, null);
            if (ret < 0) {
                Log.Info("JuvoPlayer", "Could not find stream info (error code: " + ret.ToString() + ")!");

                DeallocFFmpeg();
                throw new Exception("Could not find stream info (error code: " + ret.ToString() + ")!");
            }

            audio_idx = FFmpeg.av_find_best_stream(formatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
            video_idx = FFmpeg.av_find_best_stream(formatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (audio_idx < 0 && video_idx < 0) {
                DeallocFFmpeg();
                Log.Info("JuvoPlayer", "Could not find video or audio stream: " + audio_idx.ToString() + "      " + video_idx.ToString());
                throw new Exception("Could not find video or audio stream!");
            }
        }

        unsafe private void DemuxTask()
        {
            try {
                InitURL(); // Finish more time-consuming init things
                ReadAudioConfig();
                ReadVideoConfig();
            }
            catch (Exception e) {
                Log.Error("JuvoPlayer", "An error occured: " + e.Message);
            }

            const int kMicrosecondsPerSecond = 1000000;
            const double kOneMicrosecond = 1.0 / kMicrosecondsPerSecond;
            AVRational kMicrosBase = new AVRational {
                num = 1,
                den = kMicrosecondsPerSecond
            };
            AVPacket pkt;
            bool parse = true;

            while (parse) {
                FFmpeg.av_init_packet(&pkt);
                pkt.data = null;
                pkt.size = 0;
                int ret = FFmpeg.av_read_frame(formatContext, &pkt);
                if (ret >= 0) {
                    if (pkt.stream_index != audio_idx && pkt.stream_index != video_idx)
                        continue;
                    AVStream* s = formatContext->streams[pkt.stream_index];
                    var data = pkt.data;
                    var dataSize = pkt.size;
                    var pts = FFmpeg.av_rescale_q(pkt.pts, s->time_base, kMicrosBase) * kOneMicrosecond;
                    var dts = FFmpeg.av_rescale_q(pkt.dts, s->time_base, kMicrosBase) * kOneMicrosecond;
                    var duration = FFmpeg.av_rescale_q(pkt.duration, s->time_base, kMicrosBase) * kOneMicrosecond;
                    var streamPacket = new StreamPacket {
                        StreamType = pkt.stream_index != audio_idx ? StreamType.Video : StreamType.Audio,
                        Pts = pts >= 0 ? (System.UInt64)(pts * 1000000000) : 0, // gstreamer needs nanoseconds, value cannot be negative
                        Dts = dts >= 0 ? (System.UInt64)(dts * 1000000000) : 0, // gstreamer needs nanoseconds, value cannot be negative
                        Data = new byte[dataSize],
                        IsKeyFrame = (pkt.flags == 1)
                    };
                    Log.Info("JuvoPlayer", "DEMUXER (" + (streamPacket.StreamType == StreamType.Audio ? "A" : "V") + "): data size: " + dataSize.ToString() + "; pts: " + pts.ToString() + "; dts: " + dts.ToString() + "; duration:" + duration + "; ret: " + ret.ToString());
                    CopyPacketData(data, 0, dataSize, streamPacket, true); //Marshal.Copy((IntPtr)data, streamPacket.Data, 0, dataSize);
                    StreamPacketReady(streamPacket);
                }
                else {
                    if (ret == -541478725) {
                        // send End of File event.
                    }
                    Log.Info("JuvoPlayer", "DEMUXER: ----DEMUXING----AV_READ_FRAME----ERROR---- av_read_frame()=" + ret.ToString() + " (" + GetErrorText(ret) + ")");
                    parse = false;
                }
                FFmpeg.av_packet_unref(&pkt);
            }
        }

        unsafe private static int CopyPacketData(byte* source, int offset, int size, StreamPacket packet, bool removeSuffixPES = true)
        {
            byte[] suffixPES = // NOTE(g.skowinski): It seems like ffmpeg leaves PES headers as suffixes to some packets and SMPlayer can't handle data with such suffixes
                (packet.StreamType == StreamType.Audio) ?
                new byte[] { 0xC0, 0x00, 0x00, 0x00, 0x01, 0xCE, 0x8C, 0x4D, 0x9D, 0x10, 0x8E, 0x25, 0xE9, 0xFE } :
                new byte[] { 0xE0, 0x00, 0x00, 0x00, 0x01, 0xCE, 0x8C, 0x4D, 0x9D, 0x10, 0x8E, 0x25, 0xE9, 0xFE };
            bool suffixPresent = false;
            if (removeSuffixPES && size >= suffixPES.Length) {
                suffixPresent = true;
                for (int i = 0, dataOffset = size - suffixPES.Length; i < suffixPES.Length && i + dataOffset < size; ++i) {
                    if (source[i + dataOffset] != suffixPES[i]) {
                        suffixPresent = false;
                        break;
                    }
                }
            }
            if (removeSuffixPES && suffixPresent) {
                packet.Data = new byte[size - suffixPES.Length];
                Marshal.Copy((IntPtr)source, packet.Data, 0, size - suffixPES.Length);
            }
            else {
                //packet.Data = new byte[size]; // should be already initialized
                Marshal.Copy((IntPtr)source, packet.Data, 0, size);
            }
            return 0;
        }

        // NOTE(g.skowinski): DEBUG HELPER METHOD
        private unsafe string GetErrorText(int returnCode) // -1094995529 = -0x41444E49 = "INDA" = AVERROR_INVALID_DATA
        {
            const int errorBufferSize = 1024;
            byte[] errorBuffer = new byte[errorBufferSize];
            try {
                fixed (byte* errbuf = errorBuffer) {
                    FFmpeg.av_strerror(returnCode, errbuf, errorBufferSize);
                }
            }
            catch (Exception) {
                return "";
            }
            return System.Text.Encoding.UTF8.GetString(errorBuffer);
        }

        // NOTE(g.skowinski): DEBUG HELPER METHOD
        private void dLog(string log, int level = 0)
        {
            if (level > 0) {
                Log.Info("JuvoDemuxer", log);
            }
        }

        // NOTE(g.skowinski): DEBUG HELPER METHOD
        private static void DumpPacketToFile(StreamPacket packet, string filename)
        {
            AppendAllBytes(filename, packet.Data);
            AppendAllBytes(filename, new byte[] { 0xde, 0xad, 0xbe, 0xef, 0xde, 0xad, 0xbe, 0xef, 0xde, 0xad, 0xbe, 0xef, 0xde, 0xad, 0xbe, 0xef });
        }

        // NOTE(g.skowinski): DEBUG HELPER METHOD
        private static void AppendAllBytes(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Append)) {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        unsafe private void DeallocFFmpeg()
        {
            if (formatContext != null) {
                fixed (AVFormatContext** formatContextPointer = &formatContext) {
                    FFmpeg.avformat_close_input(formatContextPointer);
                }
                FFmpeg.avformat_free_context(formatContext);
                formatContext = null;
            }
            if (buffer != null) {
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
            if (audio_idx < 0 || audio_idx >= formatContext->nb_streams) {
                Log.Info("JuvoPlayer", "Wrong audio stream index! nb_streams = " + formatContext->nb_streams.ToString() + ", audio_idx = " + audio_idx.ToString());
                return;
            }
            AVStream* s = formatContext->streams[audio_idx];
            AudioStreamConfig config = new AudioStreamConfig();

            AVSampleFormat sampleFormat = (AVSampleFormat)s->codecpar->format;
            config.Codec = ConvertAudioCodec(s->codecpar->codec_id);
            //config.sample_format = ConvertSampleFormat(sample_format);
            if (s->codecpar->bits_per_coded_sample > 0) {
                config.BitsPerChannel = s->codecpar->bits_per_coded_sample; // per channel? not per sample? o_O
            }
            else {
                config.BitsPerChannel = FFmpeg.av_get_bytes_per_sample(sampleFormat) * 8;
                config.BitsPerChannel /= s->codecpar->channels;
            }
            config.ChannelLayout = s->codecpar->channels;
            config.SampleRate = s->codecpar->sample_rate;

            // these could be useful:
            // ----------------------
            // s->codec->block_align
            // s->codec->bit_rate
            // s->codec->extradata_size
            // s->codec->extradata
            // s->codec->codec_tag
            // s->time_base.den
            // s->time_base.num

            Log.Info("JuvoPlayer", "Setting audio stream to " + audio_idx.ToString() + "/" + formatContext->nb_streams.ToString());
            Log.Info("JuvoPlayer", "  Codec = " + config.Codec.ToString());
            Log.Info("JuvoPlayer", "  BitsPerChannel = " + config.BitsPerChannel.ToString());
            Log.Info("JuvoPlayer", "  ChannelLayout = " + config.ChannelLayout.ToString());
            Log.Info("JuvoPlayer", "  SampleRate = " + config.SampleRate.ToString());
            Log.Info("JuvoPlayer", "");

            StreamConfigReady?.Invoke(config);
        }

        unsafe private void ReadVideoConfig()
        {
            if (video_idx < 0 || video_idx >= formatContext->nb_streams) {
                Log.Info("JuvoPlayer", "Wrong video stream index! nb_streams = " + formatContext->nb_streams.ToString() + ", video_idx = " + video_idx.ToString());
                return;
            }
            AVStream* s = formatContext->streams[video_idx];
            VideoStreamConfig config = new VideoStreamConfig();
            config.Codec = ConvertVideoCodec(s->codecpar->codec_id);
            config.CodecProfile = s->codecpar->profile;
            config.Size = new Tizen.Multimedia.Size(s->codecpar->width, s->codecpar->height);
            config.FrameRate = s->r_frame_rate.num / s->r_frame_rate.den;
            config.FrameRateNum = s->r_frame_rate.num;
            config.FrameRateDen = s->r_frame_rate.den;

            // these could be useful:
            // ----------------------
            // s->codec->extradata_size
            // s->codec->extradata
            // s->codec->codec_tag
            // s->time_base.den
            // s->time_base.num

            Log.Info("JuvoPlayer", "Setting video stream to " + video_idx.ToString() + "/" + formatContext->nb_streams.ToString());
            Log.Info("JuvoPlayer", "  Codec = " + config.Codec.ToString());
            Log.Info("JuvoPlayer", "  Size = " + config.Size.ToString());
            Log.Info("JuvoPlayer", "  FrameRate = " + config.FrameRate.ToString() + " (" + config.FrameRateNum + "/" + config.FrameRateDen + ")");

            StreamConfigReady?.Invoke(config);
        }

        AudioCodec ConvertAudioCodec(AVCodecID codec)
        {
            switch (codec) {
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
                    throw new Exception("Unsupported codec: " + codec.ToString());
            }
        }

        VideoCodec ConvertVideoCodec(AVCodecID codec)
        {
            switch (codec) {
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
                    throw new Exception("Unsupported codec: " + codec.ToString());
            }
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
            try {
                GCHandle handle = GCHandle.FromIntPtr((IntPtr)opaque);
                sharedBuffer = (ISharedBuffer)handle.Target;
            }
            catch (Exception) {
                Log.Info("JuvoPlayer", "Retrieveing SharedBuffer reference failed!");
                throw;
            }
            return sharedBuffer;
        }

        static private unsafe int ReadPacket(void* @opaque, byte* @buf, int @buf_size)
        {
            ISharedBuffer sharedBuffer;
            try {
                sharedBuffer = RetrieveSharedBufferReference(opaque);
            }
            catch (Exception) {
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