using System;
using System.Runtime.InteropServices;
using JuvoLogger;
using JuvoPlayer.Demuxers.FFmpeg.Interop;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public unsafe class AVIOContextWrapper : IAVIOContext
    {
        private AVIOContext* context;
        private byte* buffer;
        private readonly avio_alloc_context_read_packet readFunctionDelegate;
        private readonly ReadPacket readPacket;

        public AVIOContextWrapper(ulong bufferSize, ReadPacket readPacket)
        {
            buffer = (byte*) Interop.FFmpeg.av_mallocz(bufferSize);
            this.readPacket = readPacket;
            readFunctionDelegate = ReadPacket;
            var readFunction = new avio_alloc_context_read_packet_func
                {Pointer = Marshal.GetFunctionPointerForDelegate(readFunctionDelegate)};
            var writeFunction = new avio_alloc_context_write_packet_func {Pointer = IntPtr.Zero};
            var seekFunction = new avio_alloc_context_seek_func {Pointer = IntPtr.Zero};

            context = Interop.FFmpeg.avio_alloc_context(buffer,
                (int) bufferSize,
                0,
                (void*) GCHandle.ToIntPtr(
                    GCHandle.Alloc(
                        this)),
                readFunction,
                writeFunction,
                seekFunction);

            if (context == null)
                throw new FFmpegException("Cannot allocate AVIOContext");

            context->seekable = 0;
            context->write_flag = 0;
        }

        internal AVIOContext* Context => context;

        public bool Seekable
        {
            get => context->seekable != 0;
            set => context->seekable = value ? 1 : 0;
        }

        public bool WriteFlag
        {
            get => context->write_flag != 0;
            set => context->write_flag = value ? 1 : 0;
        }

        private int ReadPacket(void* @opaque, byte* @buf, int bufSize)
        {
            var handle = GCHandle.FromIntPtr((IntPtr) opaque);
            var wrapper = (AVIOContextWrapper) handle.Target;
            var data = wrapper.readPacket(bufSize);
            if (data != null)
                Marshal.Copy(data.Value.Array, data.Value.Offset, (IntPtr) buf, data.Value.Count);
            return data?.Count ?? -541478725;
        }

        private void ReleaseUnmanagedResources()
        {
            if (context != null)
            {
                Interop.FFmpeg.av_free((*context).buffer);
                fixed (AVIOContext** ioContextPtr = &context)
                {
                    Interop.FFmpeg.avio_context_free(ioContextPtr); // also sets to null
                }

                buffer = null;
            }

            if (buffer != null)
            {
                Interop.FFmpeg.av_free(buffer);
                buffer = null;
            }
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~AVIOContextWrapper()
        {
            ReleaseUnmanagedResources();
        }
    }
}