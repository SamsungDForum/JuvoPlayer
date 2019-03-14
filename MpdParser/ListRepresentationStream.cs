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

namespace MpdParser.Node.Dynamic
{
    public struct ListItem
    {
        public string Media;
        public string Range;
        public string IndexRange;
        public ulong Time;
        public ulong Duration;

        internal TimeRelation Contains(ulong timepoint)
        {
            if (timepoint < Time)
                return TimeRelation.LATER;
            if ((timepoint - Time) > Duration)
                return TimeRelation.EARLIER;
            return TimeRelation.SPOTON;
        }

        public static ListItem[] FromXml(uint startNumber, TimeSpan startPoint, uint timescale, ulong duration, SegmentURL[] urls, string baseURL)
        {
            ulong start = (ulong)Math.Ceiling(startPoint.TotalSeconds * timescale);
            int size = 0;
            for (int i = 0; i < urls.Length; ++i)
            {
                if (string.IsNullOrEmpty(urls[i].Media))
                {
                    // ISO IEC 23009-1 2014 section 5.3.9.3.2 missing media states:
                    // "If not present, then any BaseURL element is mapped to the @media attribute 
                    // and the range attribute shall be present"
                    // Thus, if there is no MediaRange, skip this entry. Otherwise glue on 
                    // BaseURL
                    if (string.IsNullOrEmpty(urls[i].MediaRange))
                        continue;

                    urls[i].Media = baseURL;
                }

                ++size;
            }
            ListItem[] result = new ListItem[size];

            int pos = 0;
            for (int i = 0; i < urls.Length; ++i)
            {
                if (string.IsNullOrEmpty(urls[i].Media))
                    continue;

                result[pos].Media = urls[i].Media;
                result[pos].Range = urls[i].MediaRange;
                result[pos].IndexRange = urls[i].IndexRange;
                result[pos].Time = start;
                result[pos].Duration = duration;
                ++pos;
                start += duration;
            }

            return result;
        }
    }

    public class ListRepresentationStream : IRepresentationStream
    {
        private ManifestParameters Parameters;
        public ulong PresentationTimeOffset { get; }
        public TimeSpan? TimeShiftBufferDepth { get; }
        public TimeSpan AvailabilityTimeOffset { get; }
        public bool? AvailabilityTimeComplete { get; }

        private Uri baseURL_;
        private uint timescale_;
        private ListItem[] uris_;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public Segment IndexSegment { get; }
        public uint Count { get; }

        public ListRepresentationStream(Uri baseURL, Segment init, uint timescale, ListItem[] uris,
            ulong presentationTimeOffset, TimeSpan? timeShiftBufferDepth,
            TimeSpan availabilityTimeOffset, bool? availabilityTimeComplete)
        {
            baseURL_ = baseURL;
            timescale_ = timescale;
            uris_ = uris ?? new ListItem[] { };

            ulong totalDuration = 0;
            foreach (ListItem item in uris_)
            {
                ulong rightMost = item.Time + item.Duration;
                if (rightMost > totalDuration)
                    totalDuration = rightMost;
            }

            Count = (uint)uris.Length;
            Duration = Scaled(totalDuration - (uris_.Length > 0 ? uris_[0].Time : 0));
            InitSegment = init;

            PresentationTimeOffset = presentationTimeOffset;
            TimeShiftBufferDepth = timeShiftBufferDepth;
            AvailabilityTimeOffset = availabilityTimeOffset;
            AvailabilityTimeComplete = availabilityTimeComplete;
        }

        public void SetDocumentParameters(ManifestParameters docParams)
        {
            Parameters = docParams;
        }

        public ManifestParameters GetDocumentParameters()
        {
            return Parameters;
        }

        private Segment MakeSegment(string media, string range, TimeRange span)
        {
            Uri file;
            if (baseURL_ == null)
                file = new Uri(media, UriKind.RelativeOrAbsolute);
            else if (!Uri.TryCreate(baseURL_, media, out file))
                return null;
            return new Segment(file, range, span);
        }

        private TimeSpan Scaled(ulong point)
        {
            return TimeSpan.FromSeconds((double)point / timescale_);
        }

        private Segment MakeSegment(ListItem item)
        {
            return MakeSegment(item.Media, item.Range, new TimeRange(Scaled(item.Time), Scaled(item.Duration)));
        }

        private uint? GetStartSegmentDynamic()
        {
            var availStart = (Parameters.Document.AvailabilityStartTime ?? DateTime.MinValue);
            var liveTimeIndex = Parameters.PlayClock;

            return MediaSegmentAtTimeDynamic(liveTimeIndex);
        }

        private uint GetStartSegmentStatic()
        {
            return 0;
        }

        public uint? StartSegmentId()
        {
            if (Parameters.Document.IsDynamic)
                return GetStartSegmentDynamic();

            return GetStartSegmentStatic();
        }

        public Segment MediaSegment(uint? segmentId)
        {
            if (segmentId.HasValue && segmentId < uris_.Length)
                return MakeSegment(uris_[segmentId.Value]);

            return null;
        }

        private uint? MediaSegmentAtTimeDynamic(TimeSpan durationSpan)
        {
            throw new NotImplementedException("MediaSegmentAtTime for dynamic content needs implementation");
        }

        private uint? MediaSegmentAtTimeStatic(TimeSpan durationSpan)
        {
            ulong duration = (ulong)Math.Ceiling(durationSpan.TotalSeconds * timescale_);
            for (uint pos = 0; pos < uris_.Length; ++pos)
            {
                if (uris_[pos].Contains(duration) > TimeRelation.EARLIER)
                    return pos;
            }
            return null;
        }
        public uint? SegmentId(TimeSpan durationSpan)
        {
            if (Parameters.Document.IsDynamic)
                return MediaSegmentAtTimeDynamic(durationSpan);

            return MediaSegmentAtTimeStatic(durationSpan);
        }

        public IEnumerable<Segment> MediaSegments()
        {
            foreach (ListItem item in uris_)
            {
                yield return MakeSegment(item);
            }
        }
        public uint? NextSegmentId(uint? segmentId)
        {
            var nextSegmentId = segmentId.HasValue ? segmentId + 1 : Count;

            if (nextSegmentId >= Count)
                return null;

            return nextSegmentId;
        }
        public uint? PreviousSegmentId(uint? segmentId)
        {
            var prevSegmentId = segmentId.HasValue ? (int)segmentId - 1 : -1;

            if (prevSegmentId < 0)
                return null;

            return (uint?)prevSegmentId;
        }

        public uint? NextSegmentId(TimeSpan pointInTime)
        {
            var nextSegmentId = SegmentId(pointInTime);

            return NextSegmentId(nextSegmentId);
        }

        public TimeRange SegmentTimeRange(uint? segmentId)
        {
            if (!segmentId.HasValue || segmentId >= Count)
                return null;

            var item = uris_[(int)segmentId];
            return new TimeRange(Scaled(item.Time), Scaled(item.Duration));
        }

        public bool IsReady()
        {
            return true;
        }

        public void Initialize(CancellationToken token)
        {
        }
    }
}