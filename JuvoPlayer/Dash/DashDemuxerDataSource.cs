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
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Demuxers;
using Nito.AsyncEx;

namespace JuvoPlayer.Dash
{
    public class DashDemuxerDataSource : IDemuxerDataSource
    {
        private SegmentBuffer _currentSegment;
        private AsyncCollection<SegmentBuffer> _segments = new AsyncCollection<SegmentBuffer>();

        public int Offset { get; set; }

        public bool Completed { get; private set; }

        public void Initialize()
        {
            Offset = 0;
            _currentSegment = null;
            _segments = new AsyncCollection<SegmentBuffer>();
        }

        public AsyncCollection<SegmentBuffer> GetSegmentBuffers()
        {
            return _segments;
        }

        public void AddSegmentBuffer(SegmentBuffer segmentBuffer)
        {
            Log.Info();
            if (_segments == null)
                throw new InvalidOperationException("AddSegmentBuffer called before Initialize() or after Reset()");
            segmentBuffer.SetOffset(Offset);
            _segments.Add(segmentBuffer);
        }

        public ArraySegment<byte> Read(int size, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();

            ArraySegment<byte> result;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException();

                if (_currentSegment == null) _currentSegment = _segments.Take(cancellationToken);

                try
                {
                    result = _currentSegment.Read(size, cancellationToken);
                    break;
                }
                catch (Exception e)
                {
                    Log.Warn(e);
                    _currentSegment = null;
                }
            }

            return result;
        }

        public void Seek(long pos, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();

            Log.Info();
            _currentSegment?.Seek(pos, cancellationToken);
        }

        public void CompleteAdding()
        {
            Offset = 0;
            _currentSegment?.CompleteAdding();
            _segments?.CompleteAdding();
            Completed = true;
        }

        public void Reset()
        {
            Offset = 0;
            _currentSegment?.CompleteAdding();
            _segments?.CompleteAdding();
            _currentSegment = null;
            _segments = null;
            Completed = true;
        }
    }
}