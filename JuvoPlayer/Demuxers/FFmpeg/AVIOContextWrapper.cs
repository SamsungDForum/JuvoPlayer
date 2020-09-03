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
using JuvoPlayer.Demuxers.FFmpeg.Interop;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    public unsafe class AvioContextWrapper : IAvioContext
    {
        private readonly avio_alloc_context_read_packet _readFunctionDelegate;
        private readonly ReadPacket _readPacket;
        private byte* _buffer;

        public AvioContextWrapper(ulong bufferSize, ReadPacket readPacket)
        {
            _buffer = (byte*)Interop.FFmpeg.av_mallocz(bufferSize);
            _readPacket = readPacket;
            _readFunctionDelegate = ReadPacket;
            var readFunction = new avio_alloc_context_read_packet_func
            {
                Pointer = Marshal.GetFunctionPointerForDelegate(_readFunctionDelegate)
            };
            var writeFunction = new avio_alloc_context_write_packet_func {Pointer = IntPtr.Zero};
            var seekFunction = new avio_alloc_context_seek_func {Pointer = IntPtr.Zero};

            _context = Interop.FFmpeg.avio_alloc_context(_buffer,
                (int)bufferSize,
                0,
                (void*)GCHandle.ToIntPtr(
                    GCHandle.Alloc(
                        this)),
                readFunction,
                writeFunction,
                seekFunction);

            if (Context == null)
                throw new FFmpegException("Cannot allocate AVIOContext");

            Context->seekable = 0;
            Context->write_flag = 0;
        }

        private AVIOContext* _context;

        internal AVIOContext* Context => _context;

        public bool Seekable
        {
            get => Context->seekable != 0;
            set => Context->seekable = value ? 1 : 0;
        }

        public bool WriteFlag
        {
            get => Context->write_flag != 0;
            set => Context->write_flag = value ? 1 : 0;
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private int ReadPacket(void* opaque, byte* buf, int bufSize)
        {
            var handle = GCHandle.FromIntPtr((IntPtr)opaque);
            var wrapper = (AvioContextWrapper)handle.Target;

            var data = wrapper._readPacket(bufSize);
            if (data.Array == null)
                return -541478725;
            Marshal.Copy(data.Array, data.Offset, (IntPtr)buf, data.Count);
            return data.Count;
        }

        private void ReleaseUnmanagedResources()
        {
            if (Context != null)
            {
                fixed (AVIOContext** ioContextPtr = &_context)
                {
                    Interop.FFmpeg.avio_context_free(ioContextPtr); // also sets to null
                }

                _buffer = null;
            }

            if (_buffer != null)
            {
                Interop.FFmpeg.av_free(_buffer);
                _buffer = null;
            }
        }

        ~AvioContextWrapper()
        {
            ReleaseUnmanagedResources();
        }
    }
}
