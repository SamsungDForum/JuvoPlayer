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
using System.Threading;

namespace JuvoPlayer.SharedBuffers
{
    internal class FramesSharedBuffer : ISharedBuffer
    {
        private class ByteArrayQueue
        {
            private byte[] buffer;
            private int head;
            private int tail;
            private readonly int initialBufferSize;
            private int Size
            {
                get
                {
                    return head <= tail ? tail - head : (buffer.Length - head) + tail;
                }
            }
            private readonly object locker = new object();

            public ByteArrayQueue(int initialBufferSize = 2048)
            {
                this.initialBufferSize = initialBufferSize;
                buffer = new byte[this.initialBufferSize];
                head = 0;
                tail = 0;
            }

            public void Clear()
            {
                lock (locker)
                {
                    head = 0;
                    tail = 0;
                    Resize(initialBufferSize);
                }
            }

            public int Length => Size;

            private void Resize(int newSize)
            {
                if (buffer.Length == newSize)
                {
                    return;
                }
                byte[] newBuffer = new byte[newSize];
                int oldSize = Size;
                if (oldSize > 0)
                {
                    if (head < tail)
                    {
                        Buffer.BlockCopy(buffer, head, newBuffer, 0, oldSize);
                    }
                    else
                    {
                        Buffer.BlockCopy(buffer, head, newBuffer, 0, buffer.Length - head);
                        Buffer.BlockCopy(buffer, 0, newBuffer, buffer.Length - head, tail);
                    }
                }
                head = 0;
                tail = oldSize;
                buffer = newBuffer;
            }

            public void Push(byte[] buffer, int offset, int size)
            {
                lock (locker)
                {
                    if ((Size + size) >= this.buffer.Length)
                    {
                        Resize(1 << ((int)Math.Ceiling(Math.Log(Size + size, 2)) + 1)); // resize to double the lowest power of 2 that is higher than required size
                    }
                    if (head < tail)
                    {
                        int length = this.buffer.Length - tail;
                        if (length >= size)
                        {
                            Buffer.BlockCopy(buffer, offset, this.buffer, tail, size);
                        }
                        else
                        {
                            Buffer.BlockCopy(buffer, offset, this.buffer, tail, length);
                            Buffer.BlockCopy(buffer, offset + length, this.buffer, 0, size - length);
                        }
                    }
                    else
                    {
                        Buffer.BlockCopy(buffer, offset, this.buffer, tail, size);
                    }
                    tail = (tail + size) % this.buffer.Length;
                }
            }

            public int Pop(byte[] buffer, int offset, int size)
            {
                lock (locker)
                {
                    size = Math.Min(size, Size);
                    if (head < tail)
                    {
                        Buffer.BlockCopy(this.buffer, head, buffer, offset, size);
                    }
                    else
                    {
                        int length = this.buffer.Length - head;
                        if (length >= size)
                        {
                            Buffer.BlockCopy(this.buffer, head, buffer, offset, size);
                        }
                        else
                        {
                            Buffer.BlockCopy(this.buffer, head, buffer, offset, length);
                            Buffer.BlockCopy(this.buffer, 0, buffer, offset + length, size - length);
                        }
                    }
                    head = (head + size) % this.buffer.Length;
                    if (Size == 0)
                    {
                        head = 0;
                        tail = 0;
                    }
                    if (this.buffer.Length > Size * 2 && Size * 2 > initialBufferSize)
                    {
                        Resize(this.buffer.Length / 2);
                    }
                    return size;
                }
            }
        }

        private readonly object locker = new object();
        private readonly ByteArrayQueue buffer = new ByteArrayQueue();

        public bool EndOfData { get; set; }

        public FramesSharedBuffer()
        {
            EndOfData = false;
        }

        public ulong Length()
        {
            lock (locker)
            {
                return (ulong)buffer.Length;
            }
        }

        public void ClearData()
        {
            lock (locker)
            {
                buffer.Clear();
                EndOfData = false;
            }
        }

        public void WriteData(byte[] data, bool endOfData = false)
        { // endOfData=true should be atomic with writing last bit of data
            lock (locker)
            {
                if (data != null)
                {
                    buffer.Push(data, 0, data.Length);
                }

                EndOfData = endOfData;
                Monitor.PulseAll(locker);
            }
        }

        // FramesSharedBuffer::ReadData(int size) is blocking - it will block until buffer has enough data or if EOF is reached.
        // It may return less data then requested if there is not enough data in the buffer and EOF is reached.
        public ArraySegment<byte>? ReadData(int size)
        {
            lock (locker)
            {
                while (true)
                {
                    if (buffer.Length >= size || EndOfData == true)
                    {
                        long dsize = Math.Min(buffer.Length, size);
                        var temp = new byte[dsize]; // should be optimized later by removing excessive copying
                        if (temp.Length > 0)
                            buffer.Pop(temp, 0, (int)dsize);

                        return new ArraySegment<byte>(temp);
                    }
                    Monitor.Wait(locker); // lock is released while waiting
                }
            }
        }
    }
}
