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
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;

namespace MpdParser.Node.Dynamic
{
    public class BaseRepresentationStream : IRepresentationStream
    {
        /// <summary>
        /// Custom IComparer for searching Segment array array
        /// by start & Duration
        ///
        /// Segment.Period.Start = Time to look for
        ///
        /// Time to look for has to match exactly segment start time
        ///
        /// </summary>
        internal class IndexSearchStartTime : IComparer<Segment>
        {
            public int Compare(Segment x, Segment y)
            {
                if (x.Period.Start <= y.Period.Start)
                {
                    if (x.Period.Start + x.Period.Duration > y.Period.Start)
                        return 0;
                    return -1;
                }

                return 1;
            }
        }

        public BaseRepresentationStream(Segment init, Segment media_,
            ulong presentationTimeOffset, TimeSpan? timeShiftBufferDepth,
            TimeSpan availabilityTimeOffset, bool? availabilityTimeComplete,
            Segment index = null)
        {
            media = media_;
            InitSegment = init;
            indexSegment = index;

            PresentationTimeOffset = presentationTimeOffset;
            TimeShiftBufferDepth = timeShiftBufferDepth;
            AvailabilityTimeOffset = availabilityTimeOffset;
            AvailabilityTimeComplete = availabilityTimeComplete;
        }

        protected static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private ManifestParameters parameters;

        private readonly Segment media;

        public Segment InitSegment { get; }

        private readonly Segment indexSegment;

        public ulong PresentationTimeOffset { get; }
        public TimeSpan? TimeShiftBufferDepth { get; }
        public TimeSpan AvailabilityTimeOffset { get; }
        public bool? AvailabilityTimeComplete { get; }

        private Task indexingTask;
        private static readonly List<Segment> EmptyIndex = new List<Segment>();
        private List<Segment> fMp4Index;
        private TimeSpan fMp4IndexDuration;

        public uint Count => (uint)(indexSegment == null ? 1 : Segments.Count);

        private IList<Segment> Segments => indexSegment == null ?
            EmptyIndex : GetSegments().Result;

        public TimeSpan? Duration => indexSegment == null ?
            media?.Period?.Duration : GetDuration().Result;

        private readonly object initLock = new object();

        private async Task<IList<Segment>> GetSegments()
        {
            await indexingTask.ConfigureAwait(false);
            return fMp4Index;
        }

        private async Task<TimeSpan?> GetDuration()
        {
            await indexingTask.ConfigureAwait(false);
            return fMp4IndexDuration;
        }

        public void SetDocumentParameters(ManifestParameters docParams)
        {
            parameters = docParams;
        }

        public ManifestParameters GetDocumentParameters()
        {
            return parameters;
        }

        public Segment MediaSegment(uint? segmentId)
        {
            if (media == null || !segmentId.HasValue)
                return null;

            // Non Indexed base representation. segment Id verified to prevent
            // repeated playback of same segment with different IDs
            //
            if (indexSegment == null && segmentId == 0)
                return media;

            // Indexed Segments. Require segment information to be available.
            // If does not exists (Count==0), do not return segment.
            // Count - Segment ID Argument shall handle index validation
            //
            if (Segments.Count - segmentId > 0)
                return Segments[(int)segmentId];

            return null;
        }

        public uint? SegmentId(TimeSpan pointInTime)
        {
            if (media == null)
                return null;

            // Non indexed case
            //
            if (indexSegment == null)
            {
                if (media.Contains(pointInTime) <= TimeRelation.EARLIER)
                    return null;

                return 0;
            }

            var idx = GetIndexSegmentIndex(pointInTime);

            if (idx < 0)
                return null;

            return (uint)idx;
        }

        private uint? GetStartSegmentDynamic()
        {
            var availStart = (parameters.Document.AvailabilityStartTime ?? DateTime.MinValue);
            var liveTimeIndex = parameters.PlayClock;

            return SegmentId(liveTimeIndex);
        }

        private uint? GetStartSegmentStatic()
        {
            // Non indexed case
            //
            if (indexSegment == null)
                return 0;

            // Index Case.
            // Prepare stream had to be called first.
            return 0;
        }

        public uint? StartSegmentId()
        {
            if (media == null)
                return null;

            if (parameters.Document.IsDynamic)
                return GetStartSegmentDynamic();

            return GetStartSegmentStatic();
        }

        public IEnumerable<Segment> MediaSegments()
        {
            if (Segments.Count == 0 && media != null)
                return new List<Segment>() { media };
            return Segments;
        }

        public uint? NextSegmentId(uint? segmentId)
        {
            // Non Index case has no next segment. Just one - start
            // so return no index.
            // Sanity check included (all ORs)
            if (indexSegment == null || media == null || !segmentId.HasValue)
                return null;

            var nextSegmentId = segmentId + 1;

            if (nextSegmentId >= Segments.Count)
                return null;

            return nextSegmentId;
        }

        public uint? PreviousSegmentId(uint? segmentId)
        {
            // Non Index case has no next segment. Just one - start
            // so return no index.
            // Sanity check included (all ORs)
            if (indexSegment == null || media == null || !segmentId.HasValue)
                return null;

            var prevSegmentId = (int)segmentId - 1;

            if (prevSegmentId < 0)
                return null;

            return (uint?)prevSegmentId;
        }

        public uint? NextSegmentId(TimeSpan pointInTime)
        {
            var nextSegmentId = SegmentId(pointInTime);

            if (nextSegmentId.HasValue == false)
                return null;

            return NextSegmentId(nextSegmentId.Value);
        }

        public TimeRange SegmentTimeRange(uint? segmentId)
        {
            if (!segmentId.HasValue || media == null)
                return null;

            // Non indexed case
            if (indexSegment == null && segmentId == 0)
                return media.Period.Copy();

            if (segmentId >= Segments.Count)
                return null;

            // Returned TimeRange via a copy. Intentional.
            // If Manifest gets updated it is undesired to have weird values in it.
            //
            return Segments[(int)segmentId].Period.Copy();
        }

        private int GetIndexSegmentIndex(TimeSpan pointInTime)
        {
            var searcher = new IndexSearchStartTime();
            var searchFor = new Segment(null, null, new TimeRange(pointInTime, TimeSpan.Zero));
            var idx = fMp4Index.BinarySearch(0, Segments.Count, searchFor, searcher);

            if (idx < 0 && pointInTime == Duration)
                idx = Segments.Count - 1;

            if (idx < 0)
                Logger.Warn(
                    $"Failed to find index segment in @time. FA={Segments[0].Period.Start} Req={pointInTime} LA={Segments[Segments.Count - 1].Period.Start}, Duration={Duration}");

            return idx;
        }

        public bool IsReady()
        {
            // Not initLock protected. Exact status is not needed.
            if (indexSegment == null)
                return true;

            if (indexingTask?.IsCanceled == true || indexingTask?.IsFaulted == true)
                return false;

            return indexingTask?.IsCompleted == true;
        }

        public void Initialize(CancellationToken token)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            if (indexSegment == null)
                return;

            lock (initLock)
            {
                if (!(indexingTask == null || indexingTask.Status >= TaskStatus.Canceled))
                    return;

                indexingTask = Task.Factory.StartNew(
                        async () =>
                        {
                            fMp4Index = (await FMp4Indexer.Download(indexSegment, media.Url, token)) as List<Segment>;

                            if (fMp4Index?.Count > 0)
                            {
                                var lastPeriod = fMp4Index[fMp4Index.Count - 1].Period;
                                fMp4IndexDuration = lastPeriod.Start + lastPeriod.Duration;
                            }

                        }, token).Unwrap();
            }
        }
    }
}
