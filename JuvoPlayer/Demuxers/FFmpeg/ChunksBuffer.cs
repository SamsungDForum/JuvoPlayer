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
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace JuvoPlayer.Demuxers.FFmpeg
{
    /// <summary>
    /// IMPORTANT NOTE: Although this class uses thread-safe collection under the hood, it's not entirely thread-safe.
    /// It's assumed that TakeAsync() (and Take()) method will always be called on the same thread.
    /// </summary>
    public class ChunksBuffer
    {
        private readonly AsyncCollection<byte[]> buffer;
        private byte[] lastChunk;
        private int lastChunkPos;

        public ChunksBuffer()
        {
            buffer = new AsyncCollection<byte[]>();
        }

        public void Add(byte[] item)
        {
            buffer.Add(item);
        }

        public ArraySegment<byte> Take(int size, CancellationToken cancellationToken = new CancellationToken())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();
            if (lastChunk == null)
                lastChunk = buffer.Take(cancellationToken);
            return ConsumeChunk(size);
        }

        public async Task<ArraySegment<byte>> TakeAsync(int size, CancellationToken cancellationToken = new CancellationToken())
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();
            if (lastChunk == null)
                lastChunk = await buffer.TakeAsync(cancellationToken);
            return ConsumeChunk(size);
        }

        private ArraySegment<byte> ConsumeChunk(int size)
        {
            var remainingSize = lastChunk.Length - lastChunkPos;
            var sizeToRead = Math.Min(size, remainingSize);
            var result = new ArraySegment<byte>(lastChunk, lastChunkPos, sizeToRead);

            lastChunkPos += sizeToRead;
            if (lastChunkPos == lastChunk.Length)
            {
                lastChunk = null;
                lastChunkPos = 0;
            }
            return result;
        }

        public void CompleteAdding()
        {
            buffer.CompleteAdding();
        }
    }
}
