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
        private ManifestParameters Parameters;
        
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


            //Index Download could be changed to "lazy loading"
            //done before first actual use (calls to IRepresentationStream defined API)
            DownloadIndex(true);
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        private ManualResetEvent DownloadWait = null;

        //Instance of a downloader is kept here for sole purpose
        //of doing async download cancelation if object will be destroyed before 
        //download completes.
        private NetClient Downloader = null;

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

        ~BaseRepresentationStream()
        {
            //Cancel any pending request (i.e. Donwloader non null)
            if (Downloader != null)
            {
                Downloader.CancelAsync();
            }
        }

        public void SetDocumentParameters(ManifestParameters docParams)
        {
            Parameters = docParams;
        }

        public ManifestParameters GetDocumentParameters()
        {
            return Parameters;
        }
        private void DownloadIndex(bool async)
        {
            //Create index storage only if index segment is provided
            if (IndexSegment == null)
                return;

            ByteRange rng = new ByteRange(IndexSegment.ByteRange);

            try
            {
                if (async)
                {
                    Logger.Info(string.Format("Index Segment present. Attempting ASYNC download"));
                    DownloadWait = new ManualResetEvent(false);

                    //NetClient could be moved to a singleton servicing
                    //all internal instances as long as it can internally multitask
                    //which is not fully clear from docs (states it is threaded but no
                    //info as to how many threads are supported, etc.)
                    Downloader = new NetClient();
                    Downloader.SetRange(rng.Low, rng.High);
                    Downloader.DownloadDataCompleted += new DownloadDataCompletedEventHandler(DownloadCompleted);
                    Logger.Info(string.Format("Downloading Index Segment {0} {1}-{2}", IndexSegment.Url, rng.Low, rng.High));
                    Downloader.DownloadDataAsync(IndexSegment.Url, (UInt64)rng.High);

                }
                else
                {
                    Logger.Info(string.Format("Index Segment present. Attempting SYNC download"));

                    using (NetClient DataSucker = new NetClient())
                    {
                        DataSucker.SetRange(rng.Low, rng.High);
                        byte[] data = DataSucker.DownloadData(IndexSegment.Url);
                        ProcessIndexData(data, (UInt64)rng.High);
                    }

                }
            }
            catch (Exception ex)
            {
                Logger.Warn(string.Format("Index dwonload failed {0} {1}", ex.GetType(), IndexSegment.Url));
                if (ex is WebException)
                {
                    Logger.Warn(string.Format("Error Code {0} {1} {2}", ((WebException)ex).Message,
                        ((WebException)ex).Response,
                        IndexSegment.Url));
                }

                if (async)
                {
                    // In case of async load failure, release semaphores
                    DownloadWait.Set();
                    DownloadWait.Dispose();
                    DownloadWait = null;

                    Downloader.Dispose();
                    Downloader = null;
                }
            }
        }

        private void DownloadCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            try
            {
                // If the request was not canceled and did not throw
                // an exception, display the resource.
                if (!e.Cancelled && e.Error == null)
                {
                    Logger.Info(string.Format("Index Segment Downloaded {0}", IndexSegment.Url));

                    UInt64 datastart = (UInt64)e.UserState;
                    byte[] rawData = e.Result;

                    ProcessIndexData(rawData, datastart);
                }
                else
                {
                    //How to handle failure of download?
                    //Wipe segment from existance? Pretend there is no index data and play along?
                    Logger.Info(string.Format("Downloading Index Segment FAILED {0}", IndexSegment.Url));
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(string.Format("Error {0}", ex.Message));
            }
            finally
            {
                // Let the main application thread resume.
                Logger.Info(string.Format("Unblocking access to index data {0}", IndexSegment.Url));

                DownloadWait.Set();
                DownloadWait.Dispose();
                DownloadWait = null;

                Downloader.Dispose();
                Downloader = null;
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

            DownloadWait?.WaitOne();

            if (segments_.Count == 0)
            {
                Logger.Info(string.Format("No index data for {0}", media_.Url.ToString()));
                return media_;
            }

            if (pos >= segments_.Count)
                return null;

            return segments_[(int)pos];
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
            //TODO: Take into account @startNumber if available
            if (Parameters.Document.IsDynamic == true)
                return GetStartSegmentDynamic(durationSpan);

            return GetStartSegmentStatic(durationSpan);
        }

        private uint? MediaSegmentAtTimeStatic(TimeSpan duration)
        {
            for (int i = 0; i < segments_.Count; ++i)
            {
                if (segments_[i].Period.Start + segments_[i].Period.Duration >= duration)
                    return (uint)i;
            }

            return null;
        }

        private uint? MediaSegmentAtTimeDynamic(TimeSpan duration)
        {
            throw new NotImplementedException("MediaSegmentAtTime for dynamic content needs implementation");
        }

        public uint? MediaSegmentAtTime(TimeSpan duration)
        {
            Logger.Info(string.Format("MediaSegmentAtTime {0}", duration));
            if (media_ == null)
                return null;

            if (media_.Contains(duration) <= TimeRelation.EARLIER)
                return null;

            DownloadWait?.WaitOne();

            if (Parameters.Document.IsDynamic == true)
                return MediaSegmentAtTimeDynamic(duration);

            return MediaSegmentAtTimeStatic(duration);
        }

        public IEnumerable<Segment> MediaSegments()
        {
            return segments_;
        }
    }

    public class TemplateRepresentationStream : IRepresentationStream
    {
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
        private SortedList<TimeSpan, ulong> timelineTimeIndex_;

        /// <summary>
        /// Indexer for segment search by internal segment ID. 
        /// Maps internal segment ID to physical location in timelineAll_
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

            Logger.Info("TemplateRepresentationStream() Start");
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
            timelineEmpty_ = new TimelineItemRep[0];

            // Indexes will never be longer then unwinded data count, so start them with initial
            // size of entryCount so there will be no resizing later
            timelineTimeIndex_ = new SortedList<TimeSpan, ulong>((Int32)entryCount);
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

            res = $"All={timelineAll_.Length} Available={Count}\r";

            res += "ALL:\r";
            Array.ForEach(timelineAll_, item => { res += $"No={item.Number} Time={item.TimeScaled}/{item.Time} Duration={item.DurationScaled} R={item.Repeats}"; } );

            res += "Time/SegmentID Index:\r";
            foreach (var item in timelineTimeIndex_)
            {
                int idxValue;
                TimeSpan timeScaled;
                ulong time;
                TimeSpan durationScaled;

                res += $"Time={item.Key} SegID={item.Value} ";

                if(timelineNumberIndex_.TryGetValue(item.Value, out idxValue))
                {
                    timeScaled = timelineAll_[idxValue].TimeScaled;
                    time = timelineAll_[idxValue].Time;
                    durationScaled = timelineAll_[item.Value].DurationScaled;
                }
                else
                {
                    idxValue = -1;
                    timeScaled = TimeSpan.Zero;
                    time = 0;
                    durationScaled = TimeSpan.Zero;
                }

                res += $"Index={idxValue} Time={timeScaled}/{time} Dur.={durationScaled}\r";
            }

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
            timelineTimeIndex_.Clear();
            timelineNumberIndex_.Clear();

            foreach (var item in timeline_)
            {
                timelineTimeIndex_.Add(item.TimeScaled, item.Number);
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
                timelineAvailable_ = new ArraySegment<TimelineItemRep>(timelineEmpty_);
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

            if (timelineTimeIndex_.Count == 0)
                return null;

            if (timelineTimeIndex_.TryGetValue(durationSpan, out ulong item) == false)
            {
                Logger.Debug($"Failed to find segment @time. FA={timeline_[0].Time}/{timeline_[0].TimeScaled} Req={durationSpan} LA={timeline_[timeline_.Count-1].Time}/{timeline_[timeline_.Count - 1].TimeScaled}");
                return null;
            }

            return (uint?)item;
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

