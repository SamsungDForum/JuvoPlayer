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
using System.Runtime.InteropServices;
using FFmpegBindings.Interop;

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
            buffer = (byte*) FFmpegBindings.Interop.FFmpeg.av_mallocz(bufferSize);
            this.readPacket = readPacket;
            readFunctionDelegate = ReadPacket;
            var readFunction = new avio_alloc_context_read_packet_func
                {Pointer = Marshal.GetFunctionPointerForDelegate(readFunctionDelegate)};
            var writeFunction = new avio_alloc_context_write_packet_func {Pointer = IntPtr.Zero};
            var seekFunction = new avio_alloc_context_seek_func {Pointer = IntPtr.Zero};

            context = FFmpegBindings.Interop.FFmpeg.avio_alloc_context(buffer,
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
            if (data.Array == null) return -541478725;
            Marshal.Copy(data.Array, data.Offset, (IntPtr) buf, data.Count);
            return data.Count;
        }

        private void ReleaseUnmanagedResources()
        {
            if (context != null)
            {
                FFmpegBindings.Interop.FFmpeg.av_free((*context).buffer);
                fixed (AVIOContext** ioContextPtr = &context)
                {
                    FFmpegBindings.Interop.FFmpeg.avio_context_free(ioContextPtr); // also sets to null
                }

                buffer = null;
            }

            if (buffer != null)
            {
                FFmpegBindings.Interop.FFmpeg.av_free(buffer);
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
