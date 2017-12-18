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
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tizen;

namespace JuvoPlayer.Dash
{
    public class FFmpegDemuxer : IDemuxer
    {
        public event StreamConfigReady StreamConfigReady;
        public event StreamPacketReady StreamPacketReady;

        private int bufferSize = 128 * 1024;
        private unsafe byte* _buffer = null;
        private unsafe AVFormatContext* _formatContext = null;
        private unsafe AVIOContext* _ioContext = null;
        int _audioIdx = -1;
        int _videoIdx = -1;

        private readonly ISharedBuffer _dataBuffer;
        public FFmpegDemuxer(ISharedBuffer dataBuffer, string libPath)
        {
            _dataBuffer = dataBuffer ?? throw new ArgumentNullException(nameof(dataBuffer));

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

        public void Start()
        {
            Log.Info("JuvoPlayer", "StartDemuxer!");

            // Potentially time-consuming part of initialization and demuxation loop will be executed on a detached thread.
            Task.Run(() => DemuxTask()); 
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

        private unsafe void InitEs()
        {
            int ret;
            Log.Info("JuvoPlayer", "INIT");

            _buffer = (byte*)FFmpeg.FFmpeg.av_malloc((ulong)bufferSize);
            _formatContext = FFmpeg.FFmpeg.avformat_alloc_context();
            _ioContext = FFmpeg.FFmpeg.avio_alloc_context(_buffer,
                                                         bufferSize,
                                                         0,
                                                         (void*)GCHandle.ToIntPtr(GCHandle.Alloc(_dataBuffer)),
                                                         (avio_alloc_context_read_packet)ReadPacket,
                                                         (avio_alloc_context_write_packet)WritePacket,
                                                         (avio_alloc_context_seek)Seek);
            _ioContext->seekable = 0;
            _ioContext->write_flag = 0;

            _formatContext->probesize = bufferSize;
            _formatContext->max_analyze_duration = 10 * 1000000;
            _formatContext->flags |= FFmpegMacros.AVFMT_FLAG_CUSTOM_IO;
            _formatContext->pb = _ioContext;

            if (_ioContext == null || _formatContext == null)
            {
                DeallocFFmpeg();
                Log.Info("JuvoPlayer", "Could not create FFmpeg context.!");

                throw new Exception("Could not create FFmpeg context.");
            }

            fixed (AVFormatContext** formatContextPointer = &_formatContext)
            {
                ret = FFmpeg.FFmpeg.avformat_open_input(formatContextPointer, "dummy", null, null);
                //ret = FFmpeg.FFmpeg.avformat_open_input(formatContextPointer, "rtsp://192.168.137.3/h264+mp2.ts", null, null);
            }
            if (ret != 0)
            {
                Log.Info("JuvoPlayer", "Could not parse input data: " + GetErrorText(ret));

                DeallocFFmpeg();
                //FFmpeg.av_free(buffer); // should be freed by avformat_open_input if i recall correctly
                throw new Exception("Could not parse input data: " + GetErrorText(ret));
            }

            ret = FFmpeg.FFmpeg.avformat_find_stream_info(_formatContext, null);
            if (ret < 0)
            {
                Log.Info("JuvoPlayer", "Could not find stream info (error code: " + ret + ")!");

                DeallocFFmpeg();
                throw new Exception("Could not find stream info (error code: " + ret + ")!");
            }

            _audioIdx = FFmpeg.FFmpeg.av_find_best_stream(_formatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
            _videoIdx = FFmpeg.FFmpeg.av_find_best_stream(_formatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (_audioIdx < 0 && _videoIdx < 0)
            {
                DeallocFFmpeg();
                Log.Info("JuvoPlayer", "Could not find video or audio stream: " + _audioIdx + "      " + _videoIdx);
                throw new Exception("Could not find video or audio stream!");
            }

            Log.Info("JuvoPlayer", "streamids: " + _audioIdx + "      " + _videoIdx);
        }

        private unsafe void InitUrl() {
            int ret;
            Log.Info("JuvoPlayer", "INIT");

            _buffer = (byte*)FFmpeg.FFmpeg.av_malloc((ulong)bufferSize);
            _formatContext = FFmpeg.FFmpeg.avformat_alloc_context();

            _formatContext->probesize = bufferSize;
            _formatContext->max_analyze_duration = 10 * 1000000;
            //formatContext->flags |= FFmpegMacros.AVFMT_FLAG_CUSTOM_IO;

            if (_formatContext == null) {
                DeallocFFmpeg();
                Log.Info("JuvoPlayer", "Could not create FFmpeg context!");
                throw new Exception("Could not create FFmpeg context.");
            }

            fixed (AVFormatContext** formatContextPointer = &_formatContext) {
                //string url = "rtsp://192.168.137.3/h264+mp2.ts"; // content from India, served via rtsp using live555MediaServer_v74.exe
                string url = "rtsp://192.168.137.3/test.ts"; // test.mp4 file converted with "ffmpeg -i test.mp4 -acodec copy -vcodec copy test.ts" command and served via rtsp using live555MediaServer_v74.exe
                ret = FFmpeg.FFmpeg.avformat_open_input(formatContextPointer, url, null, null);
                Log.Info("JuvoPlayer", "avformat_open_input(" + url + ") = " + (ret == 0 ? "ok" : ret.ToString()));
            }

            ret = FFmpeg.FFmpeg.avformat_find_stream_info(_formatContext, null);
            if (ret < 0) {
                Log.Info("JuvoPlayer", "Could not find stream info (error code: " + ret + ")!");

                DeallocFFmpeg();
                throw new Exception("Could not find stream info (error code: " + ret + ")!");
            }

            _audioIdx = FFmpeg.FFmpeg.av_find_best_stream(_formatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
            _videoIdx = FFmpeg.FFmpeg.av_find_best_stream(_formatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (_audioIdx < 0 && _videoIdx < 0) {
                DeallocFFmpeg();
                Log.Info("JuvoPlayer", "Could not find video or audio stream: " + _audioIdx + "      " + _videoIdx);
                throw new Exception("Could not find video or audio stream!");
            }
        }

        private unsafe void DemuxTask()
        {
            try
            {
                // Finish more time-consuming init things
                InitUrl();

                ReadAudioConfig();
                ReadVideoConfig();
            }
            catch (Exception e)
            {
                Log.Error("JuvoPlayer", "An error occured: " + e.Message);
            }

            const int kMicrosecondsPerSecond = 1000000;
            const double kOneMicrosecond = 1.0 / kMicrosecondsPerSecond;
            AVRational kMicrosBase = new AVRational
            {
                num = 1,
                den = kMicrosecondsPerSecond
            };
            bool parse = true;

            while (parse)
            {
                AVPacket pkt;
                FFmpeg.FFmpeg.av_init_packet(&pkt);
                int ret = FFmpeg.FFmpeg.av_read_frame(_formatContext, &pkt);
                if (ret >= 0)
                {
                    if (pkt.stream_index != _audioIdx && pkt.stream_index != _videoIdx)
                        continue;

                    //Log.Info("JuvoPlayer", "av_read_frame() = " + ret.ToString());

                    AVStream* s = _formatContext->streams[pkt.stream_index];
                    var data = pkt.data;
                    var dataSize = pkt.size;

                    var pts = FFmpeg.FFmpeg.av_rescale_q(pkt.pts, s->time_base, kMicrosBase) * kOneMicrosecond;
                    var dts = FFmpeg.FFmpeg.av_rescale_q(pkt.dts, s->time_base, kMicrosBase) * kOneMicrosecond;

                    var duration = FFmpeg.FFmpeg.av_rescale_q(pkt.duration, s->time_base, kMicrosBase) * kOneMicrosecond;

                    Log.Info("JuvoPlayer",
                        "data size: " + dataSize +
                        "; pts: " + pts.ToString(CultureInfo.InvariantCulture) + 
                        "; dts: " + dts.ToString(CultureInfo.InvariantCulture) +
                        "; duration:" + duration);

                    var streamPacket = new StreamPacket
                    {
                        StreamType = pkt.stream_index != _audioIdx ? StreamType.Video : StreamType.Audio,
                        Pts = (ulong)pts,
                        Dts = (ulong)dts,
                        Data = new byte[dataSize],
                        IsKeyFrame = (pkt.flags == 1)
                    };
                    Marshal.Copy((IntPtr)data, streamPacket.Data, 0, dataSize);

                    StreamPacketReady?.Invoke(streamPacket);
                }
                else
                {
                    Log.Info("JuvoPlayer", "av_read_frame() = " + ret + " :(");
                    parse = false;
                }

                FFmpeg.FFmpeg.av_packet_unref(&pkt);
            }
        }

        private unsafe void DeallocFFmpeg()
        {
            if (_formatContext != null)
            {
                fixed (AVFormatContext** formatContextPointer = &_formatContext)
                {
                    FFmpeg.FFmpeg.avformat_close_input(formatContextPointer);
                }
                FFmpeg.FFmpeg.avformat_free_context(_formatContext);
                _formatContext = null;
            }
        }

        ~FFmpegDemuxer()
        {
            DeallocFFmpeg();
        }

        private unsafe void ReadAudioConfig()
        {
            if(_audioIdx < 0 || _audioIdx >= _formatContext->nb_streams) {
                Log.Info("JuvoPlayer",
                    "Wrong audio stream index! nb_streams = " + _formatContext->nb_streams +
                    ", audio_idx = " + _audioIdx);
                return;
            }
            var s = _formatContext->streams[_audioIdx];
            var config = new AudioStreamConfig();

            AVSampleFormat sampleFormat = (AVSampleFormat)s->codecpar->format;
//            config.Codec = ConvertAudioCodec(s->codecpar->codec_id);
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

            Log.Info("JuvoPlayer", "Setting audio stream to " + _audioIdx + "/" + _formatContext->nb_streams);
            Log.Info("JuvoPlayer", "  Codec = " + config.Codec);
            Log.Info("JuvoPlayer", "  BitsPerChannel = " + config.BitsPerChannel);
            Log.Info("JuvoPlayer", "  ChannelLayout = " + config.ChannelLayout);
            Log.Info("JuvoPlayer", "  SampleRate = " + config.SampleRate);
            Log.Info("JuvoPlayer", "");

            StreamConfigReady?.Invoke(config);
        }

        private unsafe void ReadVideoConfig()
        {
            if(_videoIdx < 0 || _videoIdx >= _formatContext->nb_streams) {
                Log.Info("JuvoPlayer",
                    "Wrong video stream index! nb_streams = " + _formatContext->nb_streams + 
                    ", video_idx = " + _videoIdx);
                return;
            }
            AVStream* s = _formatContext->streams[_videoIdx];
            VideoStreamConfig config = new VideoStreamConfig
            {
//                Codec = ConvertVideoCodec(s->codecpar->codec_id, s->codecpar->profile),
                Size = new Tizen.Multimedia.Size(s->codecpar->width, s->codecpar->height),
                FrameRate = s->r_frame_rate.num / s->r_frame_rate.den
            };

            Log.Info("JuvoPlayer", "Setting video stream to " + _videoIdx + "/" + _formatContext->nb_streams);
            Log.Info("JuvoPlayer", "  Codec = " + config.Codec);
            Log.Info("JuvoPlayer", "  Size = " + config.Size);
            Log.Info("JuvoPlayer", "  FrameRate = " + config.FrameRate);

            StreamConfigReady?.Invoke(config);
        }

        Tizen.Multimedia.MediaFormatAudioMimeType ConvertAudioCodec(AVCodecID codec)
        {
            switch (codec)
            {
                case AVCodecID.AV_CODEC_ID_AAC:
                    return Tizen.Multimedia.MediaFormatAudioMimeType.Aac;
                case AVCodecID.AV_CODEC_ID_MP2:
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

        public void ChangePid(int pid)
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

        private static unsafe ISharedBuffer RetrieveSharedBufferReference(void* opaque)
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

        private static unsafe int ReadPacket(void* opaque, byte* buf, int bufSize)
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
            byte[] data = sharedBuffer.ReadData(bufSize); // SharedBuffer::ReadData(int size) is blocking - it will block until it has enough data or return less data if EOF is reached
            Marshal.Copy(data, 0, (IntPtr)buf, data.Length);
            return data.Length;
        }

        private static unsafe int WritePacket(void* opaque, byte* buf, int bufSize)
        {
            // TODO(g.skowinski): Implement.
            return 0;
        }

        private static unsafe long Seek(void* opaque, long offset, int whenc)
        {
            // TODO(g.skowinski): Implement.
            return 0;
        }
    }

}