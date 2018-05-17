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
using System.Collections;
using System.Collections.Generic;

using MpdParser;
using MpdParser.Network;
using MpdParser.Node.Atom;
using System.Net;
using System.Threading;

using JuvoLogger;

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

    public class BaseRepresentationStream : IRepresentationStream
    {
        public BaseRepresentationStream(Segment init, Segment media,
          ulong presentationTimeOffset, TimeSpan? timeShiftBufferDepth,
          TimeSpan avaliabilityTimeOffset, bool? avaliabilityTimeComplete,
          Segment index = null)
        {
            media_ = media;
            InitSegment = init;
            IndexSegment = index;

            PresentationTimeOffset = presentationTimeOffset;
            TimeShiftBufferDepth = timeShiftBufferDepth;
            AvaliabilityTimeOffset = avaliabilityTimeOffset;
            AvaliabilityTimeComplete = avaliabilityTimeComplete;

            Count = media == null ? 0u : 1u;
            Duration = media?.Period?.Duration;
        }

        protected static LoggerManager LogManager = LoggerManager.GetInstance();
        protected static ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        private ManifestParameters Parameters;

        private bool indexDownloaded;
        private Segment media_;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public Segment IndexSegment { get; }

        public ulong PresentationTimeOffset { get; }
        public TimeSpan? TimeShiftBufferDepth { get; }
        public TimeSpan AvaliabilityTimeOffset { get; }
        public bool? AvaliabilityTimeComplete { get; }

        public uint Count { get; }
        private List<Segment> segments_ = new List<Segment>();

        public void SetDocumentParameters(ManifestParameters docParams)
        {
            Parameters = docParams;
        }

        public ManifestParameters GetDocumentParameters()
        {
            return Parameters;
        }

        private void DownloadIndexOnce()
        {
            //Create index storage only if index segment is provided
            if (IndexSegment == null || this.indexDownloaded)
            {
                return;
            }

            ByteRange range = new ByteRange(IndexSegment.ByteRange);

            try
            {
                Logger.Debug($"Downloading Index Segment {IndexSegment.Url} {range}");
                byte[] data = Downloader.DownloadData(IndexSegment.Url, range);
                Logger.Debug($"Downloaded successfully Index Segment {IndexSegment.Url} {range}");
                ProcessIndexData(data, (UInt64)range.High);
                indexDownloaded = true;
            }
            catch (WebException e)
            {
                Logger.Error($"Downloading Index Segment FAILED {IndexSegment.Url} ({e.Status}):\n{e}");
                //todo(m.rybinski): what now? Retry? How many times? Show an error to the user?
            }
        }


        private void ProcessIndexData(byte[] data, ulong dataStart)
        {
            var sidx = new SIDXAtom();
            sidx.ParseAtom(data, dataStart + 1);

            //TODO:
            //SIDXAtom.SIDX_index_entry should contain a list of other sidx atoms containing
            //with index information. They could be loaded by updating range info in current
            //streamSegment and recursively calling DownloadIndexSegment - but about that we can worry later...
            //TO REMEMBER:
            //List of final sidxs should be "sorted" from low to high. In case of one it is not an issue,
            //it may be in case of N index boxes in hierarchy order (daisy chain should be ok too I think...)
            //so just do sanity check if we have such chunks
            if (sidx.SIDXIndexCount > 0)
            {
                throw new NotImplementedException("Daisy chained / Hierarchical chunks not implemented...");
            }

            for (uint i = 0; i < sidx.MovieIndexCount; ++i)
            {
                UInt64 lb;
                UInt64 hb;
                TimeSpan starttime;
                TimeSpan duration;

                (lb, hb, starttime, duration) = sidx.GetRangeData(i);
                if (lb != hb)
                {
                    string rng = lb.ToString() + "-" + hb.ToString();

                    segments_.Add(new Segment(media_.Url, rng, new TimeRange(starttime, duration)));
                }
            }
        }

        public Segment MediaSegmentAtPos(uint pos)
        {
            Logger.Info(string.Format("MediaSegmentAtPos {0}", pos));

            if (media_ == null)
                return null;

            DownloadIndexOnce();

            if (segments_.Count == 0)
            {
                Logger.Info(string.Format("No index data for {0}", media_.Url.ToString()));
                return media_;
            }

            if (pos >= segments_.Count)
                return null;

            return segments_[(int)pos];
        }

        public uint? MediaSegmentAtTime(TimeSpan duration)
        {
            Logger.Info(string.Format("MediaSegmentAtTime {0}", duration));
            if (media_ == null)
                return null;

            if (media_.Contains(duration) <= TimeRelation.EARLIER)
                return null;

            DownloadIndexOnce();

            for (int i = 0; i < segments_.Count; ++i)
            {
                if (segments_[i].Period.Start + segments_[i].Period.Duration >= duration)
                    return (uint)i;
            }
            return null;
        }

        private uint? GetStartSegmentDynamic(TimeSpan durationSpan)
        {
            var availStart = (Parameters.Document.AvailabilityStartTime ?? DateTime.MinValue);
            var liveTimeIndex = (availStart + durationSpan + Parameters.PlayClock) - availStart;

            return MediaSegmentAtTime(liveTimeIndex);
        }

        private uint GetStartSegmentStatic(TimeSpan durationSpan)
        {
            return 0;
        }

        public uint? GetStartSegment(TimeSpan durationSpan, TimeSpan bufferDepth)
        {
            if (media_ == null)
                return null;

            DownloadIndexOnce();

            //TODO: Take into account @startNumber if available
            if (Parameters.Document.IsDynamic == true)
                return GetStartSegmentDynamic(durationSpan);

            return GetStartSegmentStatic(durationSpan);
        }

        public IEnumerable<Segment> MediaSegments()
        {
            if (segments_.Count == 0 && media_ != null)
                return new List<Segment>() { media_ };
            return segments_;
        }
    }

    public class TemplateRepresentationStream : IRepresentationStream
    {
        internal class TimelineSearch 
        {
            public enum Comparison { Start, StartDuration, Number };

            public TimeSpan Start { get; }
            public TimeSpan Duration { get; }
            public ulong Number { get; }

            public Comparison CompareType {get; }
            public TimelineSearch(TimeSpan start)
            {
                CompareType = Comparison.Start;
                Start = start;
            }

            public TimelineSearch(TimeSpan start, TimeSpan duration)
            {
                CompareType = Comparison.StartDuration;
                Start = start;
                Duration = duration;
            }

            public TimelineSearch(ulong number)
            {
                CompareType = Comparison.Number;
                Number = number;
            }
            
        }
        /// <summary>
        /// Internal representation of a TimeLineItem entry.
        /// Internally relies on TimeLineItem structure used by majority of code
        /// +internal helper information build at object creation
        /// </summary>
        internal struct TimelineItemRep : IComparable
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

            public int CompareTo(object obj)
            {
                int res=-1;

                Logger.Info($"CompareTo: {obj} {obj.GetType()}");

                var lookFor = obj as TimelineSearch;

                Logger.Info($"CompareTo: {obj} {obj.GetType()} {lookFor}");
                switch (lookFor?.CompareType)
                {
                    case TimelineSearch.Comparison.Start:
                        if (this.TimeScaled < lookFor.Start)
                            res = -1;
                        else if (this.TimeScaled > lookFor.Start)
                            res = 1;
                        else
                            res = 0;

                        break;
                    case TimelineSearch.Comparison.StartDuration:
                        if (this.TimeScaled < lookFor.Start)
                            res = -1;
                        else if (this.TimeScaled + this.DurationScaled > lookFor.Start)
                            res = 1;
                        else
                            res = 0;
                        break;
                    case TimelineSearch.Comparison.Number:
                        res = (int)(this.Number - lookFor.Number);
                        break;
                    default:
                        // Manages "unsupported object"
                        Logger.Debug($"{obj} {obj.GetType()} is unsupported. TimelineSearch only");
                        break;
                }

                return res;
            }

        }

        private ManifestParameters Parameters;

        public ulong PresentationTimeOffset { get; }
        private TimeSpan PresentationTimeOffsetScaled;
        public TimeSpan? TimeShiftBufferDepth { get; }
        public TimeSpan AvaliabilityTimeOffset { get; }
        public bool? AvaliabilityTimeComplete { get; }

        private Uri baseURL_;
        private Template media_;
        private uint? bandwidth_;
        private string reprId_;
        private uint timescale_;

        /// <summary>
        /// Holds all timeline data as defined in dash manifest in an unwinded form.
        /// </summary>
        private TimelineItemRep[] timelineAll_;

        /// <summary>
        /// Empty timeline placeholder. Used for "clearing" of timelineAvailable_ providing
        /// "no segments" information.
        /// </summary>
        private readonly TimelineItemRep[] timelineEmpty_;

        /// <summary>
        /// Segment of timelineAll_ containing a range of segments available for playback
        /// In case of static content, this containes entire definition of timelineAll.
        /// For dynamic reloadable content, it will contain currently available segments.
        /// </summary>
        private ArraySegment<TimelineItemRep> timelineAvailable_;

        /// <summary>
        /// IList accessor for available content
        /// </summary>
        private IList<TimelineItemRep> timeline_;

        /// <summary>
        /// Indexer for segment search by time index. 
        /// Maps playback start time to internal segment ID
        /// </summary>
        //private SortedList<Tuple<TimeSpan,TimeSpan>, ulong> timelineTimeIndex_;

        /// <summary>
        /// Indexer for segment search by internal segment ID. 
        /// Maps internal segment ID to physical location in timelineAll_
        /// 
        /// TODO:
        /// Convert this into binary search.
        /// timelineAll_ is already sorted by time & by number. As such, this data structure
        /// could be used without any indexers to quickly search for information
        /// without any additional helper structures.
        /// </summary>
        private SortedList<ulong, int> timelineNumberIndex_;

        /// <summary>
        /// Flag indicating how template was constructed.
        /// true - build from timeline data
        /// false - build from duration.
        /// </summary>
        private bool fromTimeline;

        /// <summary>
        /// Contains Internal Segment start number as defined in dash manifest.
        /// </summary>
        private uint? segmentStartNumber;

        private static readonly uint offsetFromEnd = 3;
        private static readonly TimeSpan availabilitySearchSpan = TimeSpan.FromSeconds(20);

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public Segment IndexSegment { get; }

        /// <summary>
        /// Returns number of time available for segments in representation
        /// </summary>
        public uint Count
        {
            get { return (uint)timeline_.Count; }
        }

        private uint? TemplateDuration;
        private ulong AverageSegmentDuration;

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);
        public TemplateRepresentationStream(Uri baseURL, Template init, Template media, uint? bandwidth,
            string reprId, uint timescale, TimelineItem[] timeline,
            ulong presentationTimeOffset, TimeSpan? timeShiftBufferDepth,
            TimeSpan avaliabilityTimeOffset, bool? avaliabilityTimeComplete, bool aFromTimeline,
            uint startSegment, uint? aTemplateDuration)
        {
            baseURL_ = baseURL;
            media_ = media;
            bandwidth_ = bandwidth;
            reprId_ = reprId;
            timescale_ = timescale;

            fromTimeline = aFromTimeline;
            segmentStartNumber = startSegment;
            TemplateDuration = aTemplateDuration;

            PresentationTimeOffset = presentationTimeOffset;
            PresentationTimeOffsetScaled = Scaled(PresentationTimeOffset);
            TimeShiftBufferDepth = timeShiftBufferDepth;
            AvaliabilityTimeOffset = avaliabilityTimeOffset;
            AvaliabilityTimeComplete = avaliabilityTimeComplete;

                
            uint entryCount = (uint)timeline.Length;
            ulong justDurations = 0;

            // Get the number of elements after unwinding all repeats.    
            foreach (TimelineItem item in timeline)
            {
                entryCount += (uint)item.Repeats;
            }

            // Unwind timeline so there are no repeats which are just a pain in the anus...
            timelineAll_ = new TimelineItemRep[entryCount];

            // Indexes will never be longer then unwinded data count, so start them with initial
            // size of entryCount so there will be no resizing later
            //timelineTimeIndex_ = new SortedList<Tuple<TimeSpan,TimeSpan>, ulong>((Int32)entryCount);
            timelineNumberIndex_ = new SortedList<ulong, int>((Int32)entryCount);
            int idx = 0;

            foreach (TimelineItem item in timeline)
            {
                uint repeatCount = 0;
                do
                {
                    timelineAll_[idx].Number = item.Number + repeatCount;
                    timelineAll_[idx].Time = item.Time + (item.Duration * repeatCount);
                    timelineAll_[idx].TimeScaled = Scaled(timelineAll_[idx].Time);
                    timelineAll_[idx].Duration = item.Duration;
                    timelineAll_[idx].DurationScaled = Scaled(timelineAll_[idx].Duration);
                    timelineAll_[idx].Repeats = 0;
                    timelineAll_[idx].StorageIndex = idx;

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
            timelineAvailable_ = new ArraySegment<TimelineItemRep>(timelineAll_);
            timeline_ = timelineAvailable_ as IList<TimelineItemRep>;

            BuildIndexes();

            // Compute average segment duration by taking start time of first segment
            // and start + duration of last segment. Divison of this by number of elements
            // gives an approximation of individual segment duration
            if (Count > 0)
            {
                justDurations = timeline_[timeline_.Count - 1].Time + timeline_[timeline_.Count - 1].Duration - timeline_[0].Time;
                Duration = Scaled(justDurations);
                AverageSegmentDuration = justDurations / Count;
            }
            else
            {
                Duration = TimeSpan.Zero;
                AverageSegmentDuration = 1;
            }

            InitSegment = init == null ? null : MakeSegment(init.Get(bandwidth, reprId), null);
        }

        public void SetDocumentParameters(ManifestParameters docParams)
        {
            Parameters = docParams;

            if (Parameters.Document.IsDynamic == true)
                PurgeUnavailableSegments();

        }

        public ManifestParameters GetDocumentParameters()
        {
            return Parameters;
        }

        public override string ToString()
        {
            string res;

            res = $"All={timelineAll_.Length} Available={Count} {timelineAvailable_.Offset}-{timelineAvailable_.Offset+timelineAvailable_.Count}\r";

            res += "Data:\r";
            Array.ForEach(timelineAll_, item => { res += $"No={item.Number} Time={item.TimeScaled}/{item.Time} Duration={item.DurationScaled} R={item.Repeats}"; } );

 
            return res;
        }
      

        private Segment MakeSegment(string url, TimeRange span)
        {
            Uri file;
            if (baseURL_ == null)
                file = new Uri(url, UriKind.RelativeOrAbsolute);
            else if (!Uri.TryCreate(baseURL_, url, out file))
                return null;
            return new Segment(file, null, span);
        }

        private TimeSpan Scaled(ulong point)
        {
            return TimeSpan.FromSeconds((double)point / timescale_);
        }
        private Segment MakeSegment(TimelineItem item, uint repeat)
        {
            ulong start = item.Time + (item.Duration * repeat);
            string uri = media_.Get(bandwidth_, reprId_, item.Number + repeat, start);
            return MakeSegment(uri, new TimeRange(Scaled(start), Scaled(item.Duration)));
        }

        public Segment MediaSegmentAtPos(uint pos)
        {
            if (timelineNumberIndex_.TryGetValue(pos, out int item) == false)
            {
                Logger.Debug($"Failed to find segment @pos. FA={timeline_[0].Number} Pos={pos} LA={timeline_[timeline_.Count - 1].Number}");
                return null;
            }

            return MakeSegment(timelineAll_[item].Item, 0);

        }

        /// <summary>
        /// Retrieves live template number based on current timestamp.
        /// </summary>
        /// <param name="durationSpan">Current timestamp for which segment is to be retrieved.</param>
        /// <returns></returns>
        private ulong? GetCurrentLiveTemplateNumber(TimeSpan durationSpan)
        {
            var start = (ulong)(segmentStartNumber ?? 0);

            if (TemplateDuration.HasValue == false ||
                Parameters.Document.AvailabilityStartTime.HasValue == false)
                return start;


            var duration = TemplateDuration.Value;
            var playbackTime = Parameters.Document.AvailabilityStartTime.Value + durationSpan;
            var streamStart = Parameters.Document.AvailabilityStartTime.Value +
                              (Parameters.Period.Start ?? TimeSpan.Zero);

            // errr... yes...stream starts before "now" :-/
            if (playbackTime < streamStart)
                return null;

            var elapsedTime = (ulong)Math.Ceiling((double)(playbackTime - streamStart).Seconds * timescale_);
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
            if (fromTimeline == true)
            {
                var maxRange = timeline_.Count;
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

                var bufferInSegmentUnits = ((ulong)bufferDepth.TotalSeconds * 4 * timescale_) / AverageSegmentDuration;
                var startByTime = maxRange - (int)Math.Min((ulong)((maxRange*3) / 4), bufferInSegmentUnits);
            
                return (uint?)timeline_[startByTime].Number;
            }
            else //If timeline was build from duration.
            {
                var delay = Parameters.Document.SuggestedPresentationDelay ?? TimeSpan.Zero;
                var timeShiftBufferDepth = Parameters.Document.TimeShiftBufferDepth ?? TimeSpan.Zero;

                if (delay == TimeSpan.Zero || delay > timeShiftBufferDepth)
                    delay = timeShiftBufferDepth;

                if (delay < bufferDepth)
                    delay = bufferDepth;

                var endSegment = GetCurrentLiveTemplateNumber(durationSpan);

                if (endSegment.HasValue == false)
                    return null;

                var count = (ulong)Math.Ceiling(delay.TotalSeconds * timescale_) / (TemplateDuration ?? 1);

                var start = (ulong)(segmentStartNumber ?? 0);
                if (start + count < endSegment.Value)
                {
                    start = endSegment.Value - count;
                }

                var buffCount = (ulong)(Parameters.Document.MinBufferTime?.Seconds ?? 0);
                buffCount = (ulong)Math.Ceiling((double)buffCount * timescale_) + offsetFromEnd;
                buffCount /= TemplateDuration ?? 1;

                return (endSegment - start > buffCount) ? (uint)(endSegment.Value - buffCount) : (uint)start;

            }
        }

        /// <summary>
        /// Method rebuilds internal indexing data, time index and number index
        /// </summary>
        private void BuildIndexes()
        {
            // Recreate index information
            timelineNumberIndex_.Clear();

            foreach (var item in timeline_)
            {
                timelineNumberIndex_.Add(item.Number, item.StorageIndex);
            }
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

            current = Parameters.Document.DownloadCompleteTime - Parameters.Document.AvailabilityStartTime.Value;
            current += Parameters.Document.TimeOffset;
        
            int startIndex = -1;

            // Data is sorted (timewise) in timelineAll_ As such it should be cheaper to run from start
            // excluding timedout segments and once again from the back, excluding not yet available segments

            // TODO: Those two loops could be done using "parallel" mechanisms in c# 
            for (int i =0; i < timelineAll_.Length;i++)
            { 
                var availStart = timelineAll_[i].TimeScaled+ timelineAll_[i].DurationScaled - PresentationTimeOffsetScaled;
                var availEnd = availStart + (Parameters.Document.TimeShiftBufferDepth ?? TimeSpan.Zero) + timelineAll_[i].DurationScaled;

                if(availStart <= current && current < availEnd)
                {
                    startIndex = i;
                    break;
                }
            }

            int endIndex = -1;

            for (int i = timelineAll_.Length-1;i>=0;i--)
            {
                var availStart = timelineAll_[i].TimeScaled + timelineAll_[i].DurationScaled - PresentationTimeOffsetScaled;
                var availEnd = availStart + (Parameters.Document.TimeShiftBufferDepth ?? TimeSpan.Zero) + timelineAll_[i].DurationScaled;

                if (availStart <= current && current < availEnd)
                {
                    endIndex = i;
                    break;
                }
            }
            
            if(startIndex == -1 || endIndex == -1)
            {
                timelineAvailable_ = new ArraySegment<TimelineItemRep>(Array.Empty<TimelineItemRep>());
            }
            else
            {
                timelineAvailable_ = new ArraySegment<TimelineItemRep>(timelineAll_,startIndex,endIndex-startIndex);
            }

            timeline_ = timelineAvailable_ as IList<TimelineItemRep>;

        }

        private uint GetStartSegmentStatic(TimeSpan durationSpan)
        {
            return (uint)timelineAll_[0].Number;
        }
        public uint? GetStartSegment(TimeSpan durationSpan, TimeSpan bufferDepth)
        {
            durationSpan += Parameters.Document.TimeOffset;

            if (Parameters.Document.IsDynamic == true)
                return GetStartSegmentDynamic(durationSpan, bufferDepth);

            return GetStartSegmentStatic(durationSpan);
        }

        public uint? MediaSegmentAtTime(TimeSpan durationSpan)
        {

            if (timelineAvailable_.Count == 0)
                return null;

            var idx = Array.FindIndex<TimelineItemRep>(timelineAll_, timelineAvailable_.Offset,timelineAvailable_.Count,
                 i => ( i.TimeScaled <= durationSpan && i.TimeScaled + i.DurationScaled > durationSpan));

            //var idx = Array.BinarySearch(timelineAll_, timelineAvailable_.Offset, timelineAvailable_.Count,
            //    new TimelineSearch(durationSpan, TimeSpan.Zero));

            if (idx < 0)
            {
                Logger.Debug($"Failed to find segment @time. FA={timeline_[0].Time}/{timeline_[0].TimeScaled} Req={durationSpan} LA={timeline_[timeline_.Count-1].Time}/{timeline_[timeline_.Count - 1].TimeScaled}");
                return null;
            }

            return (uint?)timelineAll_[idx].Number;
        }

        public IEnumerable<Segment> MediaSegments()
        {
            foreach (var item in timeline_)
            {
                yield return MakeSegment(item.Item, 0);
            }
        }
    }

    public class ListRepresentationStream : IRepresentationStream
    {
        private ManifestParameters Parameters;
        public ulong PresentationTimeOffset { get; }
        public TimeSpan? TimeShiftBufferDepth { get; }
        public TimeSpan AvaliabilityTimeOffset { get; }
        public bool? AvaliabilityTimeComplete { get; }

        private Uri baseURL_;
        private uint timescale_;
        private ListItem[] uris_;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public Segment IndexSegment { get; }
        public uint Count { get; }

        public ListRepresentationStream(Uri baseURL, Segment init, uint timescale, ListItem[] uris,
            ulong presentationTimeOffset, TimeSpan? timeShiftBufferDepth,
            TimeSpan avaliabilityTimeOffset, bool? avaliabilityTimeComplete)
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
            AvaliabilityTimeOffset = avaliabilityTimeOffset;
            AvaliabilityTimeComplete = avaliabilityTimeComplete;
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



        private uint? GetStartSegmentDynamic(TimeSpan durationSpan)
        {
            var availStart = (Parameters.Document.AvailabilityStartTime ?? DateTime.MinValue);
            var liveTimeIndex = (durationSpan + Parameters.PlayClock);

            return MediaSegmentAtTimeDynamic(liveTimeIndex);
        }

        private uint GetStartSegmentStatic(TimeSpan durationSpan)
        {
            return 0;
        }

        public uint? GetStartSegment(TimeSpan durationSpan, TimeSpan bufferDepth)
        {
            //TODO: Take into account @startNumber if available
            if (Parameters.Document.IsDynamic == true)
                return GetStartSegmentDynamic(durationSpan);

            return GetStartSegmentStatic(durationSpan);
        }

        public Segment MediaSegmentAtPos(uint pos)
        {
            if (pos < uris_.Length)
                return MakeSegment(uris_[pos]);
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
        public uint? MediaSegmentAtTime(TimeSpan durationSpan)
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
    }
}

