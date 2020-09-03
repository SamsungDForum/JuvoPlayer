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
using JuvoPlayer.Common;
using JuvoPlayer.Dash.MPD;
using JuvoPlayer.Demuxers.Utils;
using JuvoPlayer.Util;

namespace JuvoPlayer.Dash
{
    public class IndexChunk : IChunk
    {
        private readonly IDownloader _downloader;
        private readonly string _indexUri;
        private readonly long? _length;

        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly RepresentationWrapper _representationWrapper;
        private readonly long? _start;
        private readonly IThroughputHistory _throughputHistory;
        private CancellationToken _cancellationToken;

        public IndexChunk(
            string uri,
            long? start,
            long? length,
            RepresentationWrapper wrapper,
            IDownloader downloader,
            IThroughputHistory throughputHistory,
            CancellationToken cancellationToken)
        {
            _indexUri = uri;
            _start = start;
            _length = length;
            _representationWrapper = wrapper;
            _downloader = downloader;
            _throughputHistory = throughputHistory;
            _cancellationToken = cancellationToken;
        }

        public async Task Load()
        {
            try
            {
                _logger.Info($"{_indexUri} {_start} {_length} starts");
                var bytes = await _downloader.Download(
                    _indexUri,
                    _start,
                    _length,
                    _throughputHistory,
                    _cancellationToken);
                _cancellationToken.ThrowIfCancellationRequested();

                var sidx = new SidxAtom();
                sidx.ParseAtom(bytes, (ulong)(_start + _length));

                if (sidx.SidxIndexCount > 0)
                    throw new NotImplementedException("Daisy chained / Hierarchical chunks not implemented...");

                var index = new BinarySearchMap<Segment>();
                for (uint i = 0; i < sidx.MovieIndexCount; ++i)
                {
                    ulong lb;
                    ulong hb;
                    TimeSpan startTime;
                    TimeSpan duration;

                    (lb, hb, startTime, duration) = sidx.GetRangeData(i);
                    index.Put(new Segment(
                        startTime,
                        startTime + duration,
                        (long)lb,
                        (long)(hb - lb + 1)));
                }

                _representationWrapper.SegmentIndex =
                    new ChunkSegmentIndex(index);
            }
            finally
            {
                _logger.Info($"{_indexUri} {_start} {_length} ends");
            }
        }

        private class Segment : BinarySearchMap<Segment>.IItem
        {
            public Segment(TimeSpan begin, TimeSpan end, long offset, long length)
            {
                Begin = begin;
                End = end;
                Offset = offset;
                Length = length;
            }

            public TimeSpan End { get; }
            public long Offset { get; }
            public long Length { get; }
            public TimeSpan Begin { get; }

            public int Compare(TimeSpan time)
            {
                return time < Begin ? -1
                    : time >= End ? 1 : 0;
            }
        }

        private class ChunkSegmentIndex : ISegmentIndex
        {
            private readonly BinarySearchMap<Segment> _index;

            public ChunkSegmentIndex(BinarySearchMap<Segment> index)
            {
                _index = index;
            }

            public long? GetSegmentCount(TimeSpan? periodDuration)
            {
                return _index.Count;
            }

            public long GetSegmentNum(TimeSpan time, TimeSpan? periodDuration)
            {
                return _index.Rank(time);
            }

            public TimeSpan GetStartTime(long segmentNum)
            {
                return _index[(int)segmentNum].Begin;
            }

            public TimeSpan? GetDuration(long segmentNum, TimeSpan? periodDuration)
            {
                var segment = _index[(int)segmentNum];
                return segment.End - segment.Begin;
            }

            public RangedUri GetSegmentUrl(long segmentNum)
            {
                var segment = _index[(int)segmentNum];
                return new RangedUri(null, segment.Offset, segment.Length);
            }

            public long GetFirstSegmentNum()
            {
                return 0;
            }
        }
    }
}
