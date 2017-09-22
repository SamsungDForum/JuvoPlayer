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

using System;
using System.IO;
using Tizen.Applications;
using JuvoPlayer.FFmpeg;
using System.Runtime.InteropServices;
using Tizen;

namespace JuvoPlayer.RTSP
{
    public class FFmpegDemuxer : IDemuxer
    {

        public unsafe FFmpegDemuxer(ISharedBuffer dataBuffer)
        {
            int ret = -1;
            string ffmpegLibdir = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Application.Current.ApplicationInfo.ExecutablePath)), "lib");
            try
            {
                FFmpeg.FFmpeg.Initialize(ffmpegLibdir);
                FFmpeg.FFmpeg.av_register_all();
            }
            catch(Exception)
            {
                // TODO(g.skowinski): Handle ("Could not load and register FFmpeg library!").
                throw;
            }

            int bufferSize = 128 * 1024;
            byte* buffer = (byte*) FFmpeg.FFmpeg.av_malloc((ulong)bufferSize);
            AVFormatContext* formatContext = FFmpeg.FFmpeg.avformat_alloc_context();
            AVIOContext* ioContext = FFmpeg.FFmpeg.avio_alloc_context(buffer,
                                                                      bufferSize,
                                                                      0,
                                                                      (void*)GCHandle.ToIntPtr(GCHandle.Alloc(dataBuffer)),
                                                                      (avio_alloc_context_read_packet)ReadPacket,
                                                                      (avio_alloc_context_write_packet)WritePacket,
                                                                      (avio_alloc_context_seek)Seek);
            ioContext->seekable = 0;
            ioContext->write_flag = 0;

            formatContext->probesize = 128 * 1024;
            formatContext->max_analyze_duration = 10 * 1000000;
            formatContext->flags |= FFmpegMacros.AVFMT_FLAG_CUSTOM_IO;
            formatContext->pb = ioContext;

            AVProbeData probeData;
            probeData.buf = buffer;
            probeData.buf_size = bufferSize;
            try
            {
                formatContext->iformat = FFmpeg.FFmpeg.av_probe_input_format(&probeData, 1);
            }
            catch (Exception)
            {
                // TODO(g.skowinski): Handle.
                throw;
            }

            //context->video_codec_id = ;
            //context->audio_codec_id = ;

            if (ioContext == null || formatContext == null)
            {
                throw new Exception("Could not create FFmpeg context."); // TODO(g.skowinski): Handle.
            }

            ret = FFmpeg.FFmpeg.avformat_open_input(&formatContext, null, null, null);
            if (ret != 0) // -1094995529 = -0x41444E49 = "INDA" = AVERROR_INVALID_DATA
            {
                try
                {
                    const int errorBufferSize = 1024;
                    byte[] errorBuffer = new byte[errorBufferSize];
                    fixed (byte* errbuf = errorBuffer)
                    {
                        FFmpeg.FFmpeg.av_strerror(ret, errbuf, errorBufferSize);
                    }
                    throw new Exception("Error info: " + System.Text.Encoding.UTF8.GetString(errorBuffer));
                }
                catch (Exception)
                {
                    throw;
                }
                //FFmpeg.av_free(buffer);
                throw new Exception("Could not parse input data!"); // TODO(g.skowinski): Handle.
            }

            ret = FFmpeg.FFmpeg.avformat_find_stream_info(formatContext, null);
            if (ret < 0)
            {
                throw new Exception("Could not find stream info (error code: " + ret.ToString() + ")!"); // TODO(g.skowinski): Handle.
            }

            var audio_idx = ret = FFmpeg.FFmpeg.av_find_best_stream(formatContext, AVMediaType.AVMEDIA_TYPE_AUDIO, -1, -1, null, 0);
            var video_idx = ret = FFmpeg.FFmpeg.av_find_best_stream(formatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
            if (audio_idx < 0 || video_idx < 0)
            {
                throw new Exception("Could not find video or audio stream!"); // TODO(g.skowinski): Handle.
            }

            const int kMicrosecondsPerSecond = 1000000;
            const double kOneMicrosecond = 1.0 / kMicrosecondsPerSecond;
            AVRational kMicrosBase = new AVRational();
            kMicrosBase.num = 1;
            kMicrosBase.den = kMicrosecondsPerSecond;

            AVPacket pkt;
            bool parse = true;
            while (parse)
            {
                FFmpeg.FFmpeg.av_init_packet(&pkt);
                ret = FFmpeg.FFmpeg.av_read_frame(formatContext, &pkt);
                if (ret >= 0)
                {
                    if (pkt.stream_index == audio_idx || pkt.stream_index == video_idx)
                    {
                        AVStream* s = formatContext->streams[pkt.stream_index];
                        var data = pkt.data;
                        var data_size = pkt.size;

                        var pts = FFmpeg.FFmpeg.av_rescale_q(pkt.pts, s->time_base, kMicrosBase) * kOneMicrosecond;
                        var dts = FFmpeg.FFmpeg.av_rescale_q(pkt.dts, s->time_base, kMicrosBase) * kOneMicrosecond;

                        Log.Info("Tag", "data size: " + data_size.ToString() + "; pts: " + pts.ToString() + "; dts: " + dts.ToString());
                    }
                }
                else
                {
                    parse = false;
                }

                FFmpeg.FFmpeg.av_packet_unref(&pkt);
            }

            FFmpeg.FFmpeg.avformat_close_input(&formatContext);
            FFmpeg.FFmpeg.avformat_free_context(formatContext);
            //FFmpeg.av_free(buffer); // TODO(g.skowinski): causes segfault - investigate
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

        static private unsafe ISharedBuffer GetSharedBufferFromOpaque(void* @opaque)
        {
            ISharedBuffer sharedBuffer;
            try
            {
                GCHandle handle = GCHandle.FromIntPtr((IntPtr)opaque);
                sharedBuffer = (ISharedBuffer)handle.Target;
            }
            catch (Exception)
            {
                throw;
            }
            return sharedBuffer;
        }

        static private unsafe int ReadPacket(void* @opaque, byte* @buf, int @buf_size)
        {
            ISharedBuffer sharedBuffer = GetSharedBufferFromOpaque(opaque);
            byte[] data = sharedBuffer.ReadData(buf_size);
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