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
using System.Text;
using System.Threading;
using JuvoLogger;

namespace MpdParser.Node.Dynamic
{
    public class TemplateRepresentationStream : IRepresentationStream
    {
        /// <summary>
        /// Custom IComparer for searching TimelineItemRep[] array
        /// by start time only.
        ///
        /// TimelineItemRep.TimeScaled = Time to look for
        ///
        /// Time to look for has to match exactly segment start time
        ///
        /// </summary>
        internal class TimelineSearchStartTime : IComparer<TimelineItemRep>
        {
            public int Compare(TimelineItemRep x, TimelineItemRep y)
            {
                if (x.TimeScaled < y.TimeScaled)
                    return -1;
                if (x.TimeScaled > y.TimeScaled)
                    return 1;
                return 0;
            }
        }

        /// <summary>
        /// Custom IComparer for searching TimelineItemRep[] array
        /// by start time and duration.
        ///
        /// TimelineItemRep.TimeScaled = Time to look for
        ///
        /// Time To Look for has to fall withing Segment Start and its duration
        ///
        /// </summary>
        internal class TimelineSearchStartTimeDuration : IComparer<TimelineItemRep>
        {
            public int Compare(TimelineItemRep x, TimelineItemRep y)
            {
                if (x.TimeScaled <= y.TimeScaled)
                {
                    if (x.TimeScaled + x.DurationScaled > y.TimeScaled)
                        return 0;
                    return -1;
                }

                return 1;
            }
        }

        /// <summary>
        /// Custom IComparer for searching TimelineItemRep[] array
        /// by segment number
        ///
        /// TimelineItemRep.Number = Segment ID to find
        ///
        /// Segment ID to find has to match exactly segment number in timelineItemRe[] array
        ///
        /// </summary>
        internal class TimelineSearchSegmentNumber : IComparer<TimelineItemRep>
        {
            public int Compare(TimelineItemRep x, TimelineItemRep y)
            {
                return (int)(x.Number - y.Number);
            }
        }

        /// <summary>
        /// Internal representation of a TimeLineItem entry.
        /// Internally relies on TimeLineItem structure used by majority of code
        /// +internal helper information build at object creation
        /// </summary>
        internal struct TimelineItemRep
        {
            public TimelineItem Item;
            public TimeSpan TimeScaled;
            public TimeSpan DurationScaled;
            public int StorageIndex;
            public TimeSpan TimeToLive;
            public ulong Number
            {
                get { return Item.Number; }
                set { Item.Number = value; }
            }
            public ulong Time
            {
                get { return Item.Time; }
                set { Item.Time = value; }
            }
            public ulong Duration
            {
                get { return Item.Duration; }
                set { Item.Duration = value; }
            }
            public ulong Repeats
            {
                get { return Item.Repeats; }
                set { Item.Repeats = value; }
            }

        }

        private ManifestParameters parameters;

        public ulong PresentationTimeOffset { get; }
        private TimeSpan presentationTimeOffsetScaled;
        public TimeSpan? TimeShiftBufferDepth { get; }
        public TimeSpan AvailabilityTimeOffset { get; }
        public bool? AvailabilityTimeComplete { get; }

        private Uri baseUrl;
        private Template media;
        private uint? bandwidth;
        private string reprId;
        private uint timescale;
        private bool timelinePurged;

        /// <summary>
        /// Holds all timeline data as defined in dash manifest in an unwinded form.
        /// </summary>
        private TimelineItemRep[] timelineAll;

        /// <summary>
        /// Segment of timelineAll_ containing a range of segments available for playback
        /// In case of static content, this contains entire definition of timelineAll.
        /// For dynamic reloadable content, it will contain currently available segments.
        /// </summary>
        private ArraySegment<TimelineItemRep> timelineAvailable;

        /// <summary>
        /// IList accessor for available content
        /// </summary>
        private IList<TimelineItemRep> timeline;

        /// <summary>
        /// Flag indicating how template was constructed.
        /// true - build from timeline data
        /// false - build from duration.
        /// </summary>
        private readonly bool fromTimeline;

        /// <summary>
        /// Contains Internal Segment start number as defined in dash manifest.
        /// </summary>
        private readonly uint? segmentStartNumber;

        private const uint OffsetFromEnd = 3;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }

        /// <summary>
        /// For dynamic content, representationWallClock contains base time used for availability
        /// in time calculations. Currently available segments may be verified against availability
        /// by adding a difference between:
        /// Now.UTC - DocumentParameters.DownloadCompleteTime
        /// </summary>
        private TimeSpan representationWallClock = TimeSpan.Zero;

        /// <summary>
        /// Returns number of time available for segments in representation
        /// </summary>
        public uint Count => (uint)timeline.Count;

        private uint? templateDuration;

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        public TemplateRepresentationStream(Uri baseUrl, Template init, Template media, uint? bandwidth,
            string reprId, uint timescale, TimelineItem[] timeline,
            ulong presentationTimeOffset, TimeSpan? timeShiftBufferDepth,
            TimeSpan availabilityTimeOffset, bool? availabilityTimeComplete, bool aFromTimeline,
            uint startSegment, uint? aTemplateDuration)
        {
            this.baseUrl = baseUrl;
            this.media = media;
            this.bandwidth = bandwidth;
            this.reprId = reprId;
            this.timescale = timescale;

            fromTimeline = aFromTimeline;
            segmentStartNumber = startSegment;
            templateDuration = aTemplateDuration;

            PresentationTimeOffset = presentationTimeOffset;
            presentationTimeOffsetScaled = Scaled(PresentationTimeOffset);
            TimeShiftBufferDepth = timeShiftBufferDepth;
            AvailabilityTimeOffset = availabilityTimeOffset;
            AvailabilityTimeComplete = availabilityTimeComplete;


            uint entryCount = (uint)timeline.Length;

            // Get the number of elements after unwinding all repeats.
            foreach (TimelineItem item in timeline)
                entryCount += (uint)item.Repeats;

            // Unwind timeline so there are no repeats which are just a pain in the anus...
            timelineAll = new TimelineItemRep[entryCount];

            int idx = 0;

            foreach (TimelineItem item in timeline)
            {
                uint repeatCount = 0;
                do
                {
                    timelineAll[idx].Number = item.Number + repeatCount;
                    timelineAll[idx].Time = item.Time + (item.Duration * repeatCount);
                    timelineAll[idx].TimeScaled = Scaled(timelineAll[idx].Time);
                    timelineAll[idx].Duration = item.Duration;
                    timelineAll[idx].DurationScaled = Scaled(timelineAll[idx].Duration);
                    timelineAll[idx].Repeats = 0;
                    timelineAll[idx].StorageIndex = idx;
                    timelineAll[idx].TimeToLive = TimeSpan.Zero;

                    idx++;
                    repeatCount++;
                } while (repeatCount <= item.Repeats);
            }

            // At this time we have no information about document type yet
            // thus it is impossible to purge content so just create it as if it was
            // static (all segments available)
            // Auto purge should be avoided. Will extend manifest processing (Stream Construction)
            // in case of multi representation media.
            //
            timelineAvailable = new ArraySegment<TimelineItemRep>(timelineAll);
            this.timeline = timelineAvailable;

            // Compute average segment duration by taking start time of first segment
            // and start + duration of last segment. Division of this by number of elements
            // gives an approximation of individual segment duration
            if (Count > 0)
            {
                var justDurations = this.timeline[this.timeline.Count - 1].Time + this.timeline[this.timeline.Count - 1].Duration - this.timeline[0].Time;
                Duration = Scaled(justDurations);
            }
            else
            {
                Duration = null;
            }

            InitSegment = init == null ? null : MakeSegment(init.Get(bandwidth, reprId), null);
        }

        public void SetDocumentParameters(ManifestParameters docParams)
        {
            parameters = docParams;

            // Setting new doc parameters for dynamic content require
            // data purge.
            timelinePurged = !parameters.Document.IsDynamic;
        }

        public ManifestParameters GetDocumentParameters()
        {
            return parameters;
        }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append($"\nAll={timelineAll.Length} Available={Count} {timelineAvailable.Offset}-{timelineAvailable.Offset + timelineAvailable.Count}\n");

            stringBuilder.Append("Data:\n");
            foreach (var item in timelineAll)
                stringBuilder.Append($"No={item.Number} TS={item.TimeScaled}/{item.Time} D={item.DurationScaled} R={item.Repeats} TTL={item.TimeToLive}\n");

            return stringBuilder.ToString();
        }

        private Segment MakeSegment(string url, TimeRange span)
        {
            Uri file;
            if (baseUrl == null)
                file = new Uri(url, UriKind.RelativeOrAbsolute);
            else if (!Uri.TryCreate(baseUrl, url, out file))
                return null;
            return new Segment(file, null, span);
        }

        private TimeSpan Scaled(ulong point)
        {
            return TimeSpan.FromSeconds((double)point / timescale);
        }

        private Segment MakeSegment(TimelineItem item, uint repeat)
        {
            ulong start = item.Time + (item.Duration * repeat);
            string uri = media.Get(bandwidth, reprId, item.Number + repeat, start);
            return MakeSegment(uri, new TimeRange(Scaled(start), Scaled(item.Duration)));
        }

        /// <summary>
        /// Retrieves live template number based on current timestamp.
        /// </summary>
        /// <returns>Start Segment number for live template</returns>
        private ulong? GetCurrentLiveTemplateNumber()
        {
            var start = (ulong)(segmentStartNumber ?? 0);

            if (templateDuration.HasValue == false ||
                parameters.Document.AvailabilityStartTime.HasValue == false)
                return start;


            var duration = templateDuration.Value;
            var playbackTime = parameters.Document.AvailabilityStartTime.Value + parameters.Document.TimeOffset;
            var streamStart = parameters.Document.AvailabilityStartTime.Value +
                              (parameters.Period.Start ?? TimeSpan.Zero);

            // errr... yes...stream starts before "now" :-/
            if (playbackTime < streamStart)
                return null;

            var elapsedTime = (ulong)Math.Ceiling((double)(playbackTime - streamStart).Seconds * timescale);
            start += elapsedTime / duration;

            return start;
        }

        /// <summary>
        /// Retrieves segment ID for initial live playback.
        /// </summary>
        /// <returns>Start Segment for dynamic playlist</returns>
        private uint? GetStartSegmentDynamic()
        {
            // Path when timeline was provided in MPD
            return fromTimeline
                ? GetStartSegmentDynamicFromTimeline()
                : GetStartSegmentDynamicFromTemplate();
        }

        private uint? GetStartSegmentDynamicFromTemplate()
        {
            var delay = parameters.Document.SuggestedPresentationDelay ?? TimeSpan.Zero;
            var timeShiftBufferDepth = parameters.Document.TimeShiftBufferDepth ?? TimeSpan.Zero;

            if (delay == TimeSpan.Zero || delay > timeShiftBufferDepth)
                delay = timeShiftBufferDepth;

            var endSegment = GetCurrentLiveTemplateNumber();

            if (endSegment.HasValue == false)
                return null;

            var count = (ulong)Math.Ceiling(delay.TotalSeconds * timescale) / (templateDuration ?? 1);

            var start = (ulong)(segmentStartNumber ?? 0);
            if (start + count < endSegment.Value)
            {
                start = endSegment.Value - count;
            }

            var buffCount = (ulong)(parameters.Document.MinBufferTime?.Seconds ?? 0);
            buffCount = (ulong)Math.Ceiling((double)buffCount * timescale) + OffsetFromEnd;
            buffCount /= templateDuration ?? 1;

            return (endSegment - start > buffCount) ? (uint)(endSegment.Value - buffCount) : (uint)start;
        }

        private uint? GetStartSegmentDynamicFromTimeline()
        {
            if (timeline.Count == 0)
                return null;

            var timeShiftBufferDepth = parameters.Document.TimeShiftBufferDepth ?? TimeSpan.Zero;

            // Start by Time is calculated as:
            // Start Segment Time = Representation Start Time +
            //                      First available segment - First segment in playlist +
            //                      1/4*timeShiftBufferDepth
            //
            // This implies that MAX buffer time for dynamic content is 3/4*timeShiftBufferDepth
            //
            var startTime = timeline[0].TimeScaled;
            startTime += TimeSpan.FromSeconds(timeShiftBufferDepth.TotalSeconds / 4);

            var searcher = new TimelineSearchStartTimeDuration();
            TimelineItemRep lookFor = new TimelineItemRep
            {
                TimeScaled = startTime
            };

            var idx = Array.BinarySearch(timelineAll, 0, timelineAll.Length,
                lookFor, searcher);

            if (idx < 0)
            {
                var last = timelineAll.Length - 1;
                Logger.Error($"Failed to find start segment @time. FAll={timelineAll[0].Number}/{timelineAll[0].TimeScaled} T={startTime} LAll={timelineAll[last].Number}/{timelineAll[last].TimeScaled}");
                return null;
            }

            // Sanity check for availability
            if (timelineAll[idx].TimeToLive == TimeSpan.Zero)
            {
                var last = timelineAll.Length - 1;
                Logger.Error($"Start segment found {timelineAll[idx].Number} is unavailable FAll={timelineAll[0].Number}/{timelineAll[0].TimeScaled} T={startTime} TTL={timelineAll[idx].TimeToLive} LAll={timelineAll[last].Number}/{timelineAll[last].TimeScaled}");
                return null;
            }
            return (uint?)timelineAll[idx].Number;
        }

        /// <summary>
        /// Method computes available start and end segment.
        /// This information is then used to make an array Segment with available content
        /// and represent is IList for general use in form of timeline_
        /// </summary>
        private void PurgeUnavailableSegments()
        {
            representationWallClock = parameters.Document.DownloadCompleteTime - parameters.Document.AvailabilityStartTime.Value;
            representationWallClock += parameters.Document.TimeOffset;

            int startIndex = -1;
            int endIndex = -1;

            // Data is sorted (timewise) in timelineAll_ As such it should be cheaper to run from start
            // excluding timed out segments and once again from the back, excluding not yet available segments
            var dataLength = timelineAll.Length - 1;
            var timeshiftBuffer = parameters.Document.TimeShiftBufferDepth ?? TimeSpan.Zero;

            for (int i = 0; i <= dataLength; i++)
            {
                var availStart = timelineAll[i].TimeScaled + timelineAll[i].DurationScaled - presentationTimeOffsetScaled;
                var availEnd = availStart + timeshiftBuffer + timelineAll[i].DurationScaled;

                if (availStart <= representationWallClock && representationWallClock < availEnd)
                {
                    startIndex = i;
                    break;
                }
            }

            for (int i = dataLength; i >= 0; i--)
            {
                var availStart = timelineAll[i].TimeScaled + timelineAll[i].DurationScaled - presentationTimeOffsetScaled;
                var availEnd = availStart + timeshiftBuffer + timelineAll[i].DurationScaled;

                if (availStart <= representationWallClock && representationWallClock < availEnd)
                {
                    endIndex = i;
                    break;
                }
            }

            if (startIndex == -1 || endIndex == -1)
                timelineAvailable = new ArraySegment<TimelineItemRep>(Array.Empty<TimelineItemRep>());
            else
            {
                // Fill in Time To Live information for all available segments
                //
                for (int i = startIndex; i <= endIndex; i++)
                {
                    var availStart = timelineAll[i].TimeScaled + timelineAll[i].DurationScaled - presentationTimeOffsetScaled;
                    var availEnd = availStart + timeshiftBuffer + timelineAll[i].DurationScaled;

                    timelineAll[i].TimeToLive = availEnd - representationWallClock;
                }

                timelineAvailable = new ArraySegment<TimelineItemRep>(timelineAll, startIndex, endIndex - startIndex);
            }

            timeline = timelineAvailable;
        }

        private uint GetStartSegmentStatic()
        {
            return (uint)timeline[0].Number;
        }

        public uint? StartSegmentId()
        {
            if (parameters.Document.IsDynamic)
                return GetStartSegmentDynamic();

            return GetStartSegmentStatic();
        }

        public Segment MediaSegment(uint? segmentId)
        {
            var idx = segmentId.HasValue ? GetSegmentIndex(segmentId.Value) : -1;
            if (idx < 0)
                return null;

            return MakeSegment(timeline[idx].Item, 0);
        }

        public uint? SegmentId(TimeSpan pointInTime)
        {
            var idx = GetSegmentIndex(pointInTime);
            if (idx < 0)
                return null;

            return (uint)timeline[idx].Number;
        }

        public IEnumerable<Segment> MediaSegments()
        {
            foreach (var item in timeline)
            {
                yield return MakeSegment(item.Item, 0);
            }
        }

        public uint? NextSegmentId(uint? segmentId)
        {

            var idx = segmentId.HasValue ? GetSegmentIndex(segmentId.Value) : -1;
            if (idx < 0)
                return null;

            idx++;
            if (idx >= Count)
                return null;

            return (uint?)timeline[idx].Number;
        }
        public uint? PreviousSegmentId(uint? segmentId)
        {

            var idx = segmentId.HasValue ? GetSegmentIndex(segmentId.Value) : -1;
            if (idx < 0)
                return null;

            idx--;
            if (idx < 0)
                return null;

            return (uint?)timeline[idx].Number;
        }

        public uint? NextSegmentId(TimeSpan pointInTime)
        {
            var idx = GetSegmentIndex(pointInTime);
            if (idx < 0)
                return null;

            // Workaround for overlapping segments.
            // Keep incrementing segment index till its timing information
            // exceeds searched point in time
            //
            while (timeline[idx].TimeScaled <= pointInTime)
            {
                idx++;

                // Temper array out of bounds aspirations.
                // We may try to get nex Segment index that's unavailable.
                if (idx >= Count)
                    return null;
            }

            return (uint)timeline[idx].Number;
        }

        public TimeRange SegmentTimeRange(uint? segmentId)
        {
            var idx = segmentId.HasValue ? GetSegmentIndex(segmentId.Value) : -1;
            if (idx < 0)
                return null;

            var item = timeline[idx];
            return new TimeRange(item.TimeScaled, item.DurationScaled);
        }

        private int GetSegmentIndex(uint segmentId)
        {
            if (Count == 0)
                return -1;

            var searcher = new TimelineSearchSegmentNumber();
            TimelineItemRep lookFor = new TimelineItemRep
            {
                Number = segmentId
            };

            var idx = Array.BinarySearch(timelineAll, timelineAvailable.Offset, timelineAvailable.Count,
                lookFor, searcher);

            if (idx < 0)
                Logger.Error($"Failed to find segment @pos. FA={timeline[0].Number} Pos={segmentId} LA={timeline[(int)Count - 1].Number}");


            // Index Search is based on timelineAll. Access is done on timeline thus offset subtraction
            // In fail case, simply larger negative value will be returned.
            //
            return (idx - timelineAvailable.Offset);
        }

        private int GetSegmentIndex(TimeSpan pointInTime)
        {
            if (Count == 0)
                return -1;

            var searcher = new TimelineSearchStartTimeDuration();
            TimelineItemRep lookFor = new TimelineItemRep
            {
                TimeScaled = pointInTime
            };

            var idx = Array.BinarySearch(timelineAll, timelineAvailable.Offset, timelineAvailable.Count,
                lookFor, searcher);

            if (idx < 0)
                Logger.Error($"Failed to find segment in @time. FA={timeline[0].TimeScaled} Req={pointInTime} LA={timeline[(int)Count - 1].TimeScaled}");


            // Index Search is based on timelineAll. Access is done on timeline thus offset subtraction
            // In fail case, simply larger negative value will be returned.
            //
            return (idx - timelineAvailable.Offset);
        }

        public bool IsReady()
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            return timelinePurged;
        }

        public void Initialize(CancellationToken token)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            // If purged - no need to initialize
            if (timelinePurged) return;

            PurgeUnavailableSegments();
            timelinePurged = true;
        }
    }
}
