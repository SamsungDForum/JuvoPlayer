/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2020, Samsung Electronics Co., Ltd
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
 *
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using ffmpeg = FFmpegBindings.Interop.FFmpeg;

namespace JuvoPlayer.Dash
{
    public class SegmentBuffer
    {
        private readonly IList<byte[]> _chunks = new List<byte[]>();
        private readonly AsyncCollection<byte[]> _toRead = new AsyncCollection<byte[]>();
        private byte[] _currentChunk;
        private int _lastChunkPos;
        private int _offset;
        private int _pos;

        public void SetOffset(int offset)
        {
            _offset = offset;
        }

        public void CompleteAdding()
        {
            _toRead.CompleteAdding();
        }

        public void Add(byte[] chunk)
        {
            _toRead.Add(chunk);
        }

        public ArraySegment<byte> Read(int size, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();

            if (_currentChunk == null)
            {
                _currentChunk = _toRead.Take(cancellationToken);
                _chunks.Add(_currentChunk);
            }

            return ConsumeChunk(size);
        }

        private ArraySegment<byte> ConsumeChunk(int size)
        {
            var remainingSize = _currentChunk.Length - _lastChunkPos;
            if (remainingSize <= 0)
                throw new InvalidOperationException();
            var sizeToRead = Math.Min(size, remainingSize);
            var result = new ArraySegment<byte>(_currentChunk, _lastChunkPos, sizeToRead);

            _lastChunkPos += sizeToRead;
            _pos += sizeToRead;
            if (_lastChunkPos == _currentChunk.Length)
            {
                _currentChunk = null;
                _lastChunkPos = 0;
            }

            return result;
        }

        public int Seek(long newPos, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();

            if (newPos >= Int32.MaxValue) return 0;
            _pos = (int) newPos - _offset;
            var index = 0;
            var soughtBytesCount = 0;
            _currentChunk = null;
            _lastChunkPos = 0;
            while (true)
            {
                if (index < _chunks.Count)
                {
                    _currentChunk = _chunks[index];
                }
                else
                {
                    _chunks.Add(_toRead.Take(cancellationToken));
                    continue;
                }

                if (_pos <= _currentChunk.Length + soughtBytesCount)
                {
                    _lastChunkPos = _pos - soughtBytesCount;
                    break;
                }

                index++;
                soughtBytesCount += _currentChunk.Length;
            }

            return 0;
        }
    }
}