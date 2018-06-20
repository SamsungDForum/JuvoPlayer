// Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using System;
using System.Collections.Generic;
using System.Text;
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
            public int Repeats
            {
                get { return Item.Repeats; }
                set { Item.Repeats = value; }
            }

        }

        private ManifestParameters parameters;

        public ulong PresentationTimeOffset { get; }
        private TimeSpan presentationTimeOffsetScaled;
        public TimeSpan? TimeShiftBufferDepth { get; }
        public TimeSpan AvaliabilityTimeOffset { get; }
        public bool? AvaliabilityTimeComplete { get; }

        private Uri baseUrl;
        private Template media;
        private uint? bandwidth;
        private string reprId;
        private uint timescale;

        /// <summary>
        /// Holds all timeline data as defined in dash manifest in an unwinded form.
        /// </summary>
        private TimelineItemRep[] timelineAll;

        /// <summary>
        /// Segment of timelineAll_ containing a range of segments available for playback
        /// In case of static content, this containes entire definition of timelineAll.
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
        public Segment IndexSegment { get; }

        /// <summary>
        /// Returns number of time available for segments in representation
        /// </summary>
        public uint Count
        {
            get { return (uint)timeline.Count; }
        }

        private uint? templateDuration;
        private ulong averageSegmentDuration;

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        private ILogger customLogger;
        public void SetLogger(ILogger log) { customLogger = log; }

        public TemplateRepresentationStream(Uri baseUrl, Template init, Template media, uint? bandwidth,
            string reprId, uint timescale, TimelineItem[] timeline,
            ulong presentationTimeOffset, TimeSpan? timeShiftBufferDepth,
            TimeSpan avaliabilityTimeOffset, bool? avaliabilityTimeComplete, bool aFromTimeline,
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
            AvaliabilityTimeOffset = avaliabilityTimeOffset;
            AvaliabilityTimeComplete = avaliabilityTimeComplete;


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

                    idx++;
                    repeatCount++;
                } while (repeatCount <= item.Repeats);
            }

            // At this time we have no information about document type yet
            // thus it is impossible to purge content so just create it as if it was
            // static (all segments available)
            // TODO: Review possibility of adding Document Type eariler in creation pipeline
            // so we cold do just one indexing. Currently this will be performed twice, once now
            // and once when document parameters are set (in case od dynamic content)
            //
            timelineAvailable = new ArraySegment<TimelineItemRep>(timelineAll);
            this.timeline = timelineAvailable;

            // Compute average segment duration by taking start time of first segment
            // and start + duration of last segment. Divison of this by number of elements
            // gives an approximation of individual segment duration
            if (Count > 0)
            {
                var justDurations = this.timeline[this.timeline.Count - 1].Time + this.timeline[this.timeline.Count - 1].Duration - this.timeline[0].Time;
                Duration = Scaled(justDurations);
                averageSegmentDuration = justDurations / Count;
            }
            else
            {
                Duration = TimeSpan.Zero;
                averageSegmentDuration = 1;
            }

            InitSegment = init == null ? null : MakeSegment(init.Get(bandwidth, reprId), null);
        }

        public void SetDocumentParameters(ManifestParameters docParams)
        {
            parameters = docParams;

            if (parameters.Document.IsDynamic)
                PurgeUnavailableSegments();
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
                stringBuilder.Append($"No={item.Number} TS={item.TimeScaled}/{item.Time} D={item.DurationScaled} R={item.Repeats}\n");

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
        /// <param name="durationSpan">Current timestamp for which segment is to be retrieved.</param>
        /// <returns></returns>
        private ulong? GetCurrentLiveTemplateNumber(TimeSpan durationSpan)
        {
            var start = (ulong)(segmentStartNumber ?? 0);

            if (templateDuration.HasValue == false ||
                parameters.Document.AvailabilityStartTime.HasValue == false)
                return start;


            var duration = templateDuration.Value;
            var playbackTime = parameters.Document.AvailabilityStartTime.Value + durationSpan;
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
        /// <param name="durationSpan">Timestamp for which Start Segment number is to be retrieved.</param>
        /// <returns></returns>
        private uint? GetStartSegmentDynamic(TimeSpan durationSpan, TimeSpan bufferDepth)
        {
            // Path when timeline was provided in MPD
            return fromTimeline 
                ? GetStartSegmentDynamicFromTimeline(bufferDepth) 
                : GetStartSegmentDynamicFromTemplate(durationSpan, bufferDepth);
        }

        private uint? GetStartSegmentDynamicFromTemplate(TimeSpan durationSpan, TimeSpan bufferDepth)
        {
            var delay = parameters.Document.SuggestedPresentationDelay ?? TimeSpan.Zero;
            var timeShiftBufferDepth = parameters.Document.TimeShiftBufferDepth ?? TimeSpan.Zero;

            if (delay == TimeSpan.Zero || delay > timeShiftBufferDepth)
                delay = timeShiftBufferDepth;

            if (delay < bufferDepth)
                delay = bufferDepth;

            var endSegment = GetCurrentLiveTemplateNumber(durationSpan);

            if (endSegment.HasValue == false)
                return null;

            var count = (ulong) Math.Ceiling(delay.TotalSeconds * timescale) / (templateDuration ?? 1);

            var start = (ulong) (segmentStartNumber ?? 0);
            if (start + count < endSegment.Value)
            {
                start = endSegment.Value - count;
            }

            var buffCount = (ulong) (parameters.Document.MinBufferTime?.Seconds ?? 0);
            buffCount = (ulong) Math.Ceiling((double) buffCount * timescale) + OffsetFromEnd;
            buffCount /= templateDuration ?? 1;

            return (endSegment - start > buffCount) ? (uint) (endSegment.Value - buffCount) : (uint) start;
        }

        private uint? GetStartSegmentDynamicFromTimeline(TimeSpan bufferDepth)
        {
            var maxRange = timeline.Count;
            if (maxRange == 0)
                return null;

            maxRange -= 1;
            // Start by Time is calculated as:
            // End Segment - Offset where
            // Offset = Available Segment Range - Buffer Size in Units of Segments. For this purpose
            // use quad buffer (4x the buffering time). 
            // This will effectively shift us closer to
            // beginning of Live Content rather then to the bleeding edge within data returned by quad buffer size.
            // 1/4 of all available segments is set as a lower limit so we do not play out segments about to
            // time out.
            // Such choice is.... purely personal decision. Any approach is imho just as good as long as we do not overshoot
            // and start getting 404s on future content. 

            var bufferInSegmentUnits = ((ulong) bufferDepth.TotalSeconds * 4 * timescale) / averageSegmentDuration;
            var startByTime = maxRange - (int) Math.Min((ulong) ((maxRange * 3) / 4), bufferInSegmentUnits);

            return (uint?) timeline[startByTime].Number;
        }

        /// <summary>
        /// Method computes available start and end segment.
        /// This information is then used to make an array Segment with available content
        /// and represent is IList for general use in form of timeline_
        /// </summary>
        /// <param name="current"></param>
        private void PurgeUnavailableSegments()
        {
            TimeSpan current;

            current = parameters.Document.DownloadCompleteTime - parameters.Document.AvailabilityStartTime.Value;
            current += parameters.Document.TimeOffset;

            int startIndex = -1;

            // Data is sorted (timewise) in timelineAll_ As such it should be cheaper to run from start
            // excluding timedout segments and once again from the back, excluding not yet available segments

            // TODO: Those two loops could be done using "parallel" mechanisms in c# 
            for (int i = 0; i < timelineAll.Length; i++)
            {
                var availStart = timelineAll[i].TimeScaled + timelineAll[i].DurationScaled - presentationTimeOffsetScaled;
                var availEnd = availStart + (parameters.Document.TimeShiftBufferDepth ?? TimeSpan.Zero) + timelineAll[i].DurationScaled;

                if (availStart <= current && current < availEnd)
                {
                    startIndex = i;
                    break;
                }
            }

            int endIndex = -1;

            for (int i = timelineAll.Length - 1; i >= 0; i--)
            {
                var availStart = timelineAll[i].TimeScaled + timelineAll[i].DurationScaled - presentationTimeOffsetScaled;
                var availEnd = availStart + (parameters.Document.TimeShiftBufferDepth ?? TimeSpan.Zero) + timelineAll[i].DurationScaled;

                if (availStart <= current && current < availEnd)
                {
                    endIndex = i;
                    break;
                }
            }

            if (startIndex == -1 || endIndex == -1)
                timelineAvailable = new ArraySegment<TimelineItemRep>(Array.Empty<TimelineItemRep>());
            else
                timelineAvailable = new ArraySegment<TimelineItemRep>(timelineAll, startIndex, endIndex - startIndex);

            timeline = timelineAvailable;
        }

        private uint GetStartSegmentStatic(TimeSpan durationSpan)
        {
            return (uint)timelineAll[0].Number;
        }
        public uint? GetStartSegment(TimeSpan durationSpan, TimeSpan bufferDepth)
        {
            durationSpan += parameters.Document.TimeOffset;

            if (parameters.Document.IsDynamic)
                return GetStartSegmentDynamic(durationSpan, bufferDepth);

            return GetStartSegmentStatic(durationSpan);
        }

        public Segment MediaSegmentAtPos(uint pos)
        {
            if (timelineAvailable.Count == 0)
                return null;

            var searcher = new TimelineSearchSegmentNumber();
            TimelineItemRep lookFor = new TimelineItemRep
            {
                Number = pos
            };

            var idx = Array.BinarySearch(timelineAll, timelineAvailable.Offset, timelineAvailable.Count,
                lookFor, searcher);

            if (idx < 0)
            {
                Logger.Info($"Failed to find segment @pos. FA={timeline[0].Number} Pos={pos} LA={timeline[timeline.Count - 1].Number}");
                return null;
            }

            return MakeSegment(timelineAll[idx].Item, 0);
        }

        public uint? MediaSegmentAtTime(TimeSpan durationSpan)
        {
            if (timelineAvailable.Count == 0)
                return null;

            var searcher = new TimelineSearchStartTimeDuration();
            TimelineItemRep lookFor = new TimelineItemRep
            {
                TimeScaled = durationSpan
            };

            var idx = Array.BinarySearch(timelineAll, timelineAvailable.Offset, timelineAvailable.Count,
                lookFor, searcher);

            if (idx < 0)
            {
                Logger.Info($"Failed to find segment in @time. FA={timeline[0].TimeScaled}/{timeline[0].Time} Req={durationSpan} LA={timeline[timeline.Count - 1].TimeScaled}/{timeline[timeline.Count - 1].TimeScaled}/{timeline[0].Time}");
                return null;
            }

            return (uint?)timelineAll[idx].Number;
        }

        public IEnumerable<Segment> MediaSegments()
        {
            foreach (var item in timeline)
            {
                yield return MakeSegment(item.Item, 0);
            }
        }
    }
}
