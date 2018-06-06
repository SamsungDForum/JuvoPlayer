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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.Demuxers.FFmpeg.Interop;
using JuvoPlayer.SharedBuffers;
using JuvoPlayer.Common.Utils;
using System.Diagnostics;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    internal sealed class FFmpegDemuxer : IDemuxer
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public event Common.ClipDurationChanged ClipDuration;
        public event Common.DRMInitDataFound DRMInitDataFound;
        public event Common.StreamConfigReady StreamConfigReady;
        public event Common.PacketReady PacketReady;
        public event DemuxerError DemuxerError;

        private const int BufferSize = 128 * 1024;
        private unsafe byte* buffer = null;
        private unsafe AVFormatContext* formatContext = null;
        private unsafe AVIOContext* ioContext = null;
        private int audioIdx = -1;
        private int videoIdx = -1;
        private bool parse = true;
        private bool resetting;

        private Task demuxTask;

        private const int MicrosecondsPerSecond = 1000000;
        private readonly AVRational microsBase = new AVRational
        {
            num = 1,
            den = MicrosecondsPerSecond
        };

        // delegates for custom io callbacks
        private readonly avio_alloc_context_read_packet readFunctionDelegate;
        private readonly avio_alloc_context_write_packet writeFunctionDelegate;
        private readonly avio_alloc_context_seek seekFunctionDelegate;

        private readonly ISharedBuffer dataBuffer;
        public bool IsPaused { get; private set; }
        private readonly AutoResetEvent pausedEvent = new AutoResetEvent(false);
        private bool isDisposed;

        public unsafe FFmpegDemuxer(string libPath, ISharedBuffer dataBuffer = null)
        {
            this.dataBuffer = dataBuffer;
            try
            {
                Interop.FFmpeg.Initialize(libPath);
                Interop.FFmpeg.av_register_all(); // TODO(g.skowinski): Is registering multiple times unwanted or doesn't it matter?

                Interop.FFmpeg.av_log_set_level(FFmpegMacros.AV_LOG_WARNING);
                av_log_set_callback_callback logCallback = (p0, level, format, vl) =>
                {
                    if (level > Interop.FFmpeg.av_log_get_level()) return;

                    const int lineSize = 1024;
                    var lineBuffer = stackalloc byte[lineSize];
                    var printPrefix = 1;
                    Interop.FFmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                    var line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer);

                    Logger.Info(line);
                };
                Interop.FFmpeg.av_log_set_callback(logCallback);
            }
            catch (Exception e)
            {
                Logger.Info("Could not load and register FFmpeg library");
                throw new DemuxerException("Could not load and register FFmpeg library", e);
            }

            // we need to make sure that delegate lifetime is the same as our demuxer class
            readFunctionDelegate = new avio_alloc_context_read_packet(ReadPacket);
            writeFunctionDelegate = new avio_alloc_context_write_packet(WritePacket);
            seekFunctionDelegate = new avio_alloc_context_seek(Seek);
        }

        public void StartForExternalSource(InitializationMode initMode)
        {
            Logger.Info("StartDemuxer!");
            if (dataBuffer == null)
                throw new InvalidOperationException("dataBuffer cannot be null");

            // Potentially time-consuming part of initialization and demuxation loop will be executed on a detached thread.
            demuxTask = Task.Run(() => DemuxTask(InitES, initMode));

            //Add error handler task if demuxer fails
            demuxTask.ContinueWith(res => OnError(GetErrorMessage(res)),TaskContinuationOptions.OnlyOnFaulted);
        }

        public void StartForUrl(string url)
        {
            Logger.Info("StartDemuxer!");

            // Potentially time-consuming part of initialization and demuxation loop will be executed on a detached thread.
            demuxTask = Task.Run(() => DemuxTask(() => InitURL(url), InitializationMode.Full));

            //Add error handler task if demuxer fails
            demuxTask.ContinueWith(res => OnError(GetErrorMessage(res)), TaskContinuationOptions.OnlyOnFaulted);
        }

        [Conditional("DEBUG")]
        private unsafe void DumpBuffer(byte* bytes, int size)
        {
            // BufferSize is fixed size, however, it may be changed at will at dev. time,
            // as such do buffer size / dump size verification to avoid odd crashes in case
            // buffer size will be set to less then 512.
            var dumpSize = size > 512 ? 512 : size;
            var data = new byte[dumpSize];
            Marshal.Copy((IntPtr)bytes, data, 0, dumpSize);
            Logger.Debug($"Buffer:\n{HexDumper.HexDumpFirstN(data, dumpSize)}");
        }

        private unsafe void InitES()
        {
            Logger.Info("INIT");

            buffer = (byte*)Interop.FFmpeg.av_mallocz((ulong)BufferSize); // let's try AllocHGlobal later on
            var readFunction = new avio_alloc_context_read_packet_func { Pointer = Marshal.GetFunctionPointerForDelegate(readFunctionDelegate) };
            var writeFunction = new avio_alloc_context_write_packet_func { Pointer = IntPtr.Zero };
            var seekFunction = new avio_alloc_context_seek_func { Pointer = IntPtr.Zero };

            ioContext = Interop.FFmpeg.avio_alloc_context(buffer,
                                                 BufferSize,
                                                 0,
                                                 (void*)GCHandle.ToIntPtr(GCHandle.Alloc(dataBuffer)), // TODO(g.skowinski): Check if allocating memory used by ffmpeg with Marshal.AllocHGlobal helps!
                                                 readFunction,
                                                 writeFunction,
                                                 seekFunction);
            formatContext = Interop.FFmpeg.avformat_alloc_context(); // it was before avio_alloc_context before, but I'm changing ordering so it's like in LiveTVApp

            if (ioContext == null || formatContext == null)
            {
                DeallocFFmpeg();
                Logger.Info("Could not create FFmpeg context.!");

                throw new Exception("Could not create FFmpeg context.");
            }

            ioContext->seekable = 0;
            ioContext->write_flag = 0;

            formatContext->probesize = 128 * 1024;
            formatContext->max_analyze_duration = 10 * 1000000;
            formatContext->flags |= FFmpegMacros.AVFMT_FLAG_CUSTOM_IO;
            formatContext->pb = ioContext;

            int ret = -1;
            fixed (AVFormatContext** formatContextPointer = &formatContext)
            {
                ret = Interop.FFmpeg.avformat_open_input(formatContextPointer, null, null, null);
            }
            if (ret != 0)
            {
                Logger.Info("Could not parse input data: " + GetErrorText(ret));
                DumpBuffer(buffer, BufferSize);

                DeallocFFmpeg();
                //FFmpeg.av_free(buffer); // should be freed by avformat_open_input if i recall correctly
                throw new Exception("Could not parse input data: " + GetErrorText(ret));
            }
        }

        private unsafe void InitURL(string url)
        {
            Logger.Info("INIT");

            Interop.FFmpeg.avformat_network_init();

            buffer = (byte*)Interop.FFmpeg.av_mallocz((ulong)BufferSize);
            formatContext = Interop.FFmpeg.avformat_alloc_context();

            formatContext->probesize = 128 * 1024;
            formatContext->max_analyze_duration = 10 * 1000000;

            if (formatContext == null)
            {
                Logger.Info("Could not create FFmpeg context!");

                DeallocFFmpeg();
                throw new Exception("Could not create FFmpeg context.");
            }

            fixed (AVFormatContext** formatContextPointer = &formatContext)
            {
                var ret = Interop.FFmpeg.avformat_open_input(formatContextPointer, url, null, null);
                Logger.Info("avformat_open_input(" + url + ") = " + (ret == 0 ? "ok" : ret + " (" + GetErrorText(ret) + ")"));
            }
        }

        private unsafe void FindStreamsInfo()
        {
            int ret = Interop.FFmpeg.avformat_find_stream_info(formatContext, null);
            if (ret < 0)
            {
                Logger.Info("Could not find stream info (error code: " + ret + ")!");

                DeallocFFmpeg();
                throw new Exception("Could not find stream info (error code: " + ret + ")!");
            }

            if (formatContext->duration > 0)
                ClipDuration?.Invoke(TimeSpan.FromMilliseconds(formatContext->duration / 1000));

            audioIdx = FindBestStream(AVMediaType.AVMEDIA_TYPE_AUDIO);
            videoIdx = FindBestStream(AVMediaType.AVMEDIA_TYPE_VIDEO);

            if (audioIdx < 0 && videoIdx < 0)
            {
                Logger.Fatal($"Neither video ({videoIdx}) nor audio stream ({audioIdx}) found");

                DeallocFFmpeg();
                throw new Exception("Neither video nor audio stream found");
            }

            // disable not used streams
            for (int i = 0; i < formatContext->nb_streams; ++i)
            {
                var enabled = i == audioIdx || i == videoIdx;
                formatContext->streams[i]->discard = enabled ? AVDiscard.AVDISCARD_DEFAULT : AVDiscard.AVDISCARD_ALL;
            }
        }

        private unsafe int FindBestStream(AVMediaType mediaType)
        {
            var streamId = FindBestBandwidthStream(mediaType);

            if (streamId >= 0)
                return streamId;

            return Interop.FFmpeg.av_find_best_stream(formatContext, mediaType, -1, -1, null, 0);
        }

        private unsafe int FindBestBandwidthStream(AVMediaType mediaType)
        {
            ulong bandwidth = 0;
            var streamId = -1;
            for (var i = 0; i < formatContext->nb_streams; ++i)
            {
                if (formatContext->streams[i]->codecpar->codec_type != mediaType)
                    continue;

                var dict = Interop.FFmpeg.av_dict_get(formatContext->streams[i]->metadata, "variant_bitrate", null, 0);
                if (dict == null)
                    return -1;

                var stringValue = Marshal.PtrToStringAnsi((IntPtr)dict->value);
                if (!ulong.TryParse(stringValue, out var value))
                    return -1;

                if (bandwidth < value)
                {
                    streamId = i;
                    bandwidth = value;
                }
            }

            return streamId;
        }

        private static string GetErrorMessage(Task response)
        {
            return response.Exception?.Flatten().InnerException.Message;
        }

        private void OnError(string errorMessage)
        {
            // Have handler. Inform without exception throwup.
            DemuxerError?.Invoke(errorMessage);
        }

        private unsafe void DemuxTask(Action initAction, InitializationMode initMode)
        {
            try
            {
                InitializeDemuxer(initAction, initMode);
            }
            catch (Exception e)
            {
                Logger.Error("An error occured: " + e.Message);

                throw new DemuxerException("Couldn't initialize demuxer", e);
            }

            while (parse)
            {
                while (IsPaused)
                    pausedEvent.WaitOne();

                AVPacket pkt = new AVPacket();
                Interop.FFmpeg.av_init_packet(&pkt);
                pkt.data = null;
                pkt.size = 0;
                int ret = Interop.FFmpeg.av_read_frame(formatContext, &pkt);
                try
                {
                    if (resetting)
                        return;

                    if (ret >= 0)
                    {
                        if (pkt.stream_index != audioIdx && pkt.stream_index != videoIdx)
                        {
                            continue;
                        }

                        AVStream* s = formatContext->streams[pkt.stream_index];
                        var data = pkt.data;
                        var dataSize = pkt.size;
                        var pts = Interop.FFmpeg.av_rescale_q(pkt.pts, s->time_base, microsBase) / 1000;
                        var dts = Interop.FFmpeg.av_rescale_q(pkt.dts, s->time_base, microsBase) / 1000;

                        var sideData = Interop.FFmpeg.av_packet_get_side_data(&pkt, AVPacketSideDataType.@AV_PKT_DATA_ENCRYPT_INFO, null);

                        Packet packet;
                        if (sideData != null)
                            packet = CreateEncryptedPacket(sideData);
                        else
                            packet = new Packet();

                        packet.StreamType = pkt.stream_index != audioIdx ? StreamType.Video : StreamType.Audio;
                        packet.Pts = TimeSpan.FromMilliseconds(pts >= 0 ? pts : 0);
                        packet.Dts = TimeSpan.FromMilliseconds(dts >= 0 ? dts : 0);
                        packet.Data = new byte[dataSize];
                        packet.IsKeyFrame = (pkt.flags == 1);

                        CopyPacketData(data, dataSize, packet, sideData == null);
                        PacketReady?.Invoke(packet);
                    }
                    else
                    {
                        if (ret == -541478725 && parse)
                        {
                            // null means EOF
                            PacketReady?.Invoke(null);
                        }
                        Logger.Info("DEMUXER: ----DEMUXING----AV_READ_FRAME----ERROR---- av_read_frame()=" + ret + " (" + GetErrorText(ret) + ")");
                        parse = false;
                    }
                }
                finally
                {
                    Interop.FFmpeg.av_packet_unref(&pkt);
                }
            }
        }

        private void InitializeDemuxer(Action initAction, InitializationMode initMode)
        {
            // Finish more time-consuming init things
            initAction();

            if (initMode != InitializationMode.Full) return;

            FindStreamsInfo();
            ReadAudioConfig();
            ReadVideoConfig();
            UpdateContentProtectionConfig();
        }

        private static unsafe Packet CreateEncryptedPacket(byte* sideData)
        {
            AVEncInfo* encInfo = (AVEncInfo*)sideData;

            int subsampleCount = encInfo->subsample_count;
            byte[] keyId = encInfo->kid.ToArray();
            byte[] iv = new byte[encInfo->iv_size];
            Buffer.BlockCopy(encInfo->iv.ToArray(), 0, iv, 0, encInfo->iv_size);

            var packet = new EncryptedPacket()
            {
                KeyId = keyId,
                Iv = iv,
            };

            if (subsampleCount <= 0)
                return packet;

            packet.Subsamples = new EncryptedPacket.Subsample[subsampleCount];

            // structure has sequential layout and the last element is an array
            // due to marshalling error we need to define this as single element
            // so to read this as an array we need to get a pointer to first element
            var subsamples = &encInfo->subsamples;
            for (int i = 0; i < subsampleCount; ++i)
            {
                packet.Subsamples[i].ClearData = subsamples[i].bytes_of_clear_data;
                packet.Subsamples[i].EncData = subsamples[i].bytes_of_enc_data;
            }

            return packet;
        }

        private static unsafe int CopyPacketData(byte* source, int size, Packet packet, bool removeSuffixPES = true)
        {
            byte[] suffixPES = // NOTE(g.skowinski): It seems like ffmpeg leaves PES headers as suffixes to some packets and SMPlayer can't handle data with such suffixes
                (packet.StreamType == StreamType.Audio) ?
                new byte[] { 0xC0, 0x00, 0x00, 0x00, 0x01, 0xCE, 0x8C, 0x4D, 0x9D, 0x10, 0x8E, 0x25, 0xE9, 0xFE } :
                new byte[] { 0xE0, 0x00, 0x00, 0x00, 0x01, 0xCE, 0x8C, 0x4D, 0x9D, 0x10, 0x8E, 0x25, 0xE9, 0xFE };
            bool suffixPresent = false;
            if (removeSuffixPES && size >= suffixPES.Length)
            {
                suffixPresent = true;
                for (int i = 0, dataOffset = size - suffixPES.Length; i < suffixPES.Length && i + dataOffset < size; ++i)
                {
                    if (source[i + dataOffset] != suffixPES[i])
                    {
                        suffixPresent = false;
                        break;
                    }
                }
            }
            if (removeSuffixPES && suffixPresent)
            {
                packet.Data = new byte[size - suffixPES.Length];
                Marshal.Copy((IntPtr)source, packet.Data, 0, size - suffixPES.Length);
            }
            else
            {
                //packet.Data = new byte[size]; // should be already initialized
                Marshal.Copy((IntPtr)source, packet.Data, 0, size);
            }
            return 0;
        }

        // NOTE(g.skowinski): DEBUG HELPER METHOD
        private static unsafe string GetErrorText(int returnCode) // -1094995529 = -0x41444E49 = "INDA" = AVERROR_INVALID_DATA
        {
            const int errorBufferSize = 1024;
            byte[] errorBuffer = new byte[errorBufferSize];
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
            return System.Text.Encoding.UTF8.GetString(errorBuffer);
        }

        // NOTE(g.skowinski): DEBUG HELPER METHOD
        private static void DumpPacketToFile(Packet packet, string filename)
        {
            AppendAllBytes(filename, packet.Data);
            AppendAllBytes(filename, new byte[] { 0xde, 0xad, 0xbe, 0xef, 0xde, 0xad, 0xbe, 0xef, 0xde, 0xad, 0xbe, 0xef, 0xde, 0xad, 0xbe, 0xef });
        }

        // NOTE(g.skowinski): DEBUG HELPER METHOD
        private static void AppendAllBytes(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Append))
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private unsafe void DeallocFFmpeg()
        {
            if (formatContext != null)
            {
                fixed (AVFormatContext** formatContextPointer = &formatContext)
                {
                    Interop.FFmpeg.avformat_close_input(formatContextPointer);
                }
                Interop.FFmpeg.avformat_free_context(formatContext);
                formatContext = null;
            }
            //note(m.rybinski): from the avio_alloc_context() docs:
            //"It [buffer] may be freed and replaced with a new buffer by libavformat.
            // AVIOContext.buffer holds the buffer currently in use,
            // which must be later freed with av_free()."
            if (ioContext != null)
            {
                Interop.FFmpeg.av_free((*ioContext).buffer);
                fixed (AVIOContext** ioContextPtr = &ioContext)
                {
                    Interop.FFmpeg.avio_context_free(ioContextPtr); //also sets to null
                }
                buffer = null;
            }
            else if (buffer != null)
            {
                //note(m.rybinski): might be better to just use av_freep()
                Interop.FFmpeg.av_free(buffer);
                buffer = null;
            }
        }

        private unsafe void UpdateContentProtectionConfig()
        {
            if (formatContext->protection_system_data_count <= 0)
                return;

            for (uint i = 0; i < formatContext->protection_system_data_count; ++i)
            {
                AVProtectionSystemSpecificData systemData = formatContext->protection_system_data[i];
                if (systemData.pssh_box_size > 0)
                {
                    var drmData = new DRMInitData
                    {
                        SystemId = systemData.system_id.ToArray(),
                        InitData = new byte[systemData.pssh_box_size]
                    };

                    Marshal.Copy((IntPtr)systemData.pssh_box, drmData.InitData, 0, (int)systemData.pssh_box_size);

                    DRMInitDataFound?.Invoke(drmData);
                }
            }
        }

        private unsafe void ReadAudioConfig()
        {
            if (audioIdx < 0 || audioIdx >= formatContext->nb_streams)
            {
                Logger.Info("Wrong audio stream index! nb_streams = " + formatContext->nb_streams + ", audio_idx = " + audioIdx);
                return;
            }

            AVStream* s = formatContext->streams[audioIdx];
            AudioStreamConfig config = new AudioStreamConfig();

            AVSampleFormat sampleFormat = (AVSampleFormat)s->codecpar->format;
            config.Codec = ConvertAudioCodec(s->codecpar->codec_id);
            //config.sample_format = ConvertSampleFormat(sample_format);
            if (s->codecpar->bits_per_coded_sample > 0)
            {
                config.BitsPerChannel = s->codecpar->bits_per_coded_sample; // per channel? not per sample? o_O
            }
            else
            {
                config.BitsPerChannel = Interop.FFmpeg.av_get_bytes_per_sample(sampleFormat) * 8;
                config.BitsPerChannel /= s->codecpar->channels;
            }
            config.ChannelLayout = s->codecpar->channels;
            config.SampleRate = s->codecpar->sample_rate;

            if (s->codecpar->extradata_size > 0)
            {
                config.CodecExtraData = new byte[s->codecpar->extradata_size];
                Marshal.Copy((IntPtr)s->codecpar->extradata, config.CodecExtraData, 0, s->codecpar->extradata_size);
            }

            // these could be useful:
            // ----------------------
            // s->codec->block_align
            // s->codec->bit_rate
            // s->codec->codec_tag
            // s->time_base.den
            // s->time_base.num

            Logger.Info("Setting audio stream to " + audioIdx + "/" + formatContext->nb_streams);
            Logger.Info("  Codec = " + config.Codec);
            Logger.Info("  BitsPerChannel = " + config.BitsPerChannel);
            Logger.Info("  ChannelLayout = " + config.ChannelLayout);
            Logger.Info("  SampleRate = " + config.SampleRate);
            Logger.Info("");

            StreamConfigReady?.Invoke(config);
        }

        private unsafe void ReadVideoConfig()
        {
            if (videoIdx < 0 || videoIdx >= formatContext->nb_streams)
            {
                Logger.Info("Wrong video stream index! nb_streams = " + formatContext->nb_streams + ", video_idx = " + videoIdx);
                return;
            }

            AVStream* s = formatContext->streams[videoIdx];
            var config = new VideoStreamConfig
            {
                Codec = ConvertVideoCodec(s->codecpar->codec_id),
                CodecProfile = s->codecpar->profile,
                Size = new Tizen.Multimedia.Size(s->codecpar->width, s->codecpar->height),
                FrameRateNum = s->r_frame_rate.num,
                FrameRateDen = s->r_frame_rate.den
            };

            if (s->codecpar->extradata_size > 0)
            {
                config.CodecExtraData = new byte[s->codecpar->extradata_size];
                Marshal.Copy((IntPtr)s->codecpar->extradata, config.CodecExtraData, 0, s->codecpar->extradata_size);
            }

            // these could be useful:
            // ----------------------
            // s->codec->codec_tag
            // s->time_base.den
            // s->time_base.num

            Logger.Info("Setting video stream to " + videoIdx + "/" + formatContext->nb_streams);
            Logger.Info("  Codec = " + config.Codec);
            Logger.Info("  Size = " + config.Size);
            Logger.Info("  FrameRate = (" + config.FrameRateNum + "/" + config.FrameRateDen + ")");

            StreamConfigReady?.Invoke(config);
        }

        AudioCodec ConvertAudioCodec(AVCodecID codec)
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

        public void ChangePID(int pid)
        {
            // TODO(g.skowinski): Implement.
        }

        public void Reset()
        {
            Logger.Info("DEMUX Reset Start");
            resetting = true;

            dataBuffer.ClearData();
            dataBuffer.WriteData(null, true);
            
            Resume();
            demuxTask?.Wait();

            // Clear EOS from buffer after demux task termination so there 
            // will not be any data reads of EOS after restart.
            dataBuffer.ClearData();
            DeallocFFmpeg();

            resetting = false;
            Logger.Info("DEMUX Reset Complete");
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
            pausedEvent.Set();
        }

        private static unsafe ISharedBuffer RetrieveSharedBufferReference(void* @opaque)
        {
            ISharedBuffer sharedBuffer;
            try
            {
                var handle = GCHandle.FromIntPtr((IntPtr)opaque);
                sharedBuffer = (ISharedBuffer)handle.Target;
            }
            catch (Exception)
            {
                Logger.Info("Retrieveing ISharedBuffer reference failed!");
                throw;
            }
            return sharedBuffer;
        }

        private unsafe int ReadPacket(void* @opaque, byte* @buf, int bufSize)
        {
            ISharedBuffer sharedBuffer;
            try
            {
                sharedBuffer = RetrieveSharedBufferReference(opaque);
            }
            catch (Exception)
            {
                return 0;
            }

            // ISharedBuffer::ReadData(int size) is blocking - it will block until it has data or return 0 if EOF is reached
            var data = sharedBuffer.ReadData(bufSize);
            if (data.HasValue)
                Marshal.Copy(data.Value.Array, data.Value.Offset, (IntPtr)buf, data.Value.Count);

            // in case of length 0 return EOF
            return data?.Count ?? -541478725;

        }

        private static unsafe int WritePacket(void* @opaque, byte* @buf, int bufSize)
        {
            // TODO(g.skowinski): Implement.
            return 0;
        }

        private static unsafe long Seek(void* @opaque, long @offset, int @whenc)
        {
            // TODO(g.skowinski): Implement.
            return 0;
        }

        private void ReleaseUnmanagedResources()
        {
            DeallocFFmpeg();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            parse = false;

            dataBuffer?.WriteData(null, true);
            Resume();
            pausedEvent.Dispose();
            try
            {
                demuxTask?.Wait();
            }
            catch (Exception e)
            {
                Logger.Error($"Demux task faulted: {e}");
            }

            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);

            isDisposed = true;
        }

        ~FFmpegDemuxer()
        {
            ReleaseUnmanagedResources();
        }
    }
}
