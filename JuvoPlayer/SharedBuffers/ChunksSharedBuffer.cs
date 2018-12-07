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
using System.Linq;
using System.Threading;

namespace JuvoPlayer.SharedBuffers
{
    internal class ChunksSharedBuffer : ISharedBuffer
    {
        private readonly object locker = new object();
        private readonly LinkedList<byte[]> buffer = new LinkedList<byte[]>();
        private int readPos;

        public bool EndOfData { get; set; }

        public ChunksSharedBuffer()
        {
            EndOfData = false;
        }

        public ulong Length()
        {
            lock (locker)
            {
                return (ulong) buffer.Sum(o => o.Length);
            }
        }

        public void ClearData()
        {
            lock (locker)
            {
                buffer.Clear();
                readPos = 0;
                EndOfData = false;
            }
        }

        public void WriteData(byte[] data, bool endOfData = false)
        { // endOfData=true should be atomic with writing last bit of data
            lock (locker)
            {
                if (data != null)
                {
                    buffer.AddLast(data);
                }

                if (endOfData)
                    EndOfData = true;
                Monitor.PulseAll(locker);
            }
        }

        // ChunksSharedBuffer::ReadData(int size) is blocking - it will block until buffer is not empty or if EOF is reached.
        // size is the maximum amount of data that can be returned from buffer.
        public ArraySegment<byte>? ReadData(int size)
        {
            lock (locker)
            {
                while (true)
                {
                    if (buffer.Count > 0)
                    {
                        var bytesLeft = buffer.First.Value.Length - readPos;
                        var read = Math.Min(bytesLeft, size);

                        var segment = new ArraySegment<byte>(buffer.First.Value, readPos, read);
                        readPos += read;
                        if (readPos >= buffer.First.Value.Length)
                        {
                            readPos = 0;
                            buffer.RemoveFirst();
                        }
                        return segment;
                    }
                    if (EndOfData)
                    {
                        return null;
                    }

                    Monitor.Wait(locker); // lock is released while waiting
                }
            }
        }
    }
}
