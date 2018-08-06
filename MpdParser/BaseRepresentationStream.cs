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
using MpdParser.Network;
using MpdParser.Node.Atom;
using System.Net;
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
          TimeSpan avaliabilityTimeOffset, bool? avaliabilityTimeComplete,
          Segment index = null)
        {
            media = media_;
            InitSegment = init;
            IndexSegment = index;

            PresentationTimeOffset = presentationTimeOffset;
            TimeShiftBufferDepth = timeShiftBufferDepth;
            AvaliabilityTimeOffset = avaliabilityTimeOffset;
            AvaliabilityTimeComplete = avaliabilityTimeComplete;

            Duration = media?.Period?.Duration;
        }

        protected static LoggerManager LogManager = LoggerManager.GetInstance();
        protected static ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        private ManifestParameters parameters;

        private bool indexDownloaded;
        private Segment media;

        public TimeSpan? Duration { get; internal set; }

        public Segment InitSegment { get; }

        private Segment IndexSegment;

        public ulong PresentationTimeOffset { get; }
        public TimeSpan? TimeShiftBufferDepth { get; }
        public TimeSpan AvaliabilityTimeOffset { get; }
        public bool? AvaliabilityTimeComplete { get; }

        public uint Count
        {
            get
            {
                // Non indexed segments have just one element - media.
                // Indexed segments have segments defined by index entry count
                return (uint)(IndexSegment == null ? 1 : segments_.Count);
            }
        }
        private List<Segment> segments_ = new List<Segment>();

        public void SetDocumentParameters(ManifestParameters docParams)
        {
            parameters = docParams;
        }

        public ManifestParameters GetDocumentParameters()
        {
            return parameters;
        }

        private void DownloadIndexOnce()
        {
            //Create index storage only if index segment is provided
            if (this.indexDownloaded)
                return;

            indexDownloaded = true;

            ByteRange range = new ByteRange(IndexSegment.ByteRange);

            try
            {
                Logger.Debug($"Downloading Index Segment {IndexSegment.Url} {range}");
                byte[] data = Downloader.DownloadData(IndexSegment.Url, range);
                Logger.Debug($"Downloaded successfully Index Segment {IndexSegment.Url} {range}");
                ProcessIndexData(data, (UInt64)range.High);

                // Update Duration to correspond to what's contained in index data
                if (segments_.Count > 0)
                {
                    var lastEntry = segments_.Count - 1;

                    // Update duration with information from index segment data.
                    // Index segment data is used for seeks. If variaes from Manifest Duration
                    // especially if it is shorter, this will cause missing segments.
                    //
                    Duration = segments_[lastEntry].Period.Start + segments_[lastEntry].Period.Duration;
                }
            }
            catch (WebException e)
            {
                Logger.Error($"Downloading Index Segment FAILED {IndexSegment.Url} ({e.Status}):\n{e}");
                // todo(m.rybinski): what now? Retry? How many times? Show an error to the user?
                // No need to add any special error handling for failed downloads. Indexed content
                // will return null segments if no index data is present.
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

                    segments_.Add(new Segment(media.Url, rng, new TimeRange(starttime, duration)));
                }
            }
        }

        public Segment MediaSegment(uint? segmentId)
        {
            if (media == null || !segmentId.HasValue)
                return null;

            // Non Indexed base representation. segment Id verified to prevent
            // repeated playback of same segment with different IDs
            //
            if (IndexSegment == null && segmentId == 0)
                return media;

            // Indexed Segments. Require segment information to be available.
            // If does not exists (Count==0), do not return segment.
            // Count - Segment ID Argument shall handle index validation
            //
            if (segments_.Count - segmentId > 0)
                return segments_[(int)segmentId];

            return null;
        }

        public uint? SegmentId(TimeSpan pointInTime)
        {
            if (media == null)
                return null;

            // Non indexed case
            //
            if (IndexSegment == null)
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
            if (IndexSegment == null)
                return 0;

            // Index Case. 
            // Prepare stream had to be called first.
            return 0;

        }

        public uint? StartSegmentId()
        {
            if (media == null)
                return null;

            //TODO: Take into account @startNumber if available
            if (parameters.Document.IsDynamic == true)
                return GetStartSegmentDynamic();

            return GetStartSegmentStatic();
        }

        public IEnumerable<Segment> MediaSegments()
        {
            if (segments_.Count == 0 && media != null)
                return new List<Segment>() { media };
            return segments_;
        }

        public uint? NextSegmentId(uint? segmentId)
        {
            // Non Index case has no next segment. Just one - start
            // so return no index. 
            // Sanity check included (all ORs)
            if (IndexSegment == null || media == null || !segmentId.HasValue)
                return null;

            var nextSegmentId = segmentId + 1;

            if (nextSegmentId >= segments_.Count)
                return null;

            return nextSegmentId;
        }

        public uint? PreviousSegmentId(uint? segmentId)
        {
            // Non Index case has no next segment. Just one - start
            // so return no index. 
            // Sanity check included (all ORs)
            if (IndexSegment == null || media == null || !segmentId.HasValue)
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
            if (IndexSegment == null && segmentId == 0)
                return media.Period.Copy();

            if (segmentId >= segments_.Count)
                return null;

            // Returned TimeRange via a copy. Intentional.
            // If Manifest gets updated it is undesired to have wierd values in it.
            //
            return segments_[(int)segmentId].Period.Copy();
        }

        private int GetIndexSegmentIndex(TimeSpan pointInTime)
        {
            // TODO: Verify lower bound limit where iterative search will be faster then 
            // binary search with associated object creation.
            //
            var searcher = new IndexSearchStartTime();
            var searchFor = new Segment(null, null, new TimeRange(pointInTime, TimeSpan.Zero));
            var idx = segments_.BinarySearch(0, segments_.Count, searchFor, searcher);

            if (idx < 0)
                Logger.Info($"Failed to find index segment in @time. FA={segments_[0].Period.Start} Req={pointInTime} LA={segments_[segments_.Count - 1].Period.Start}");

            return idx;
        }

        public bool PrepeareStream()
        {
            // Index case - search index data for segment information. Ignore media information.
            //
            if (IndexSegment == null)
                return true;
            try
            {
                DownloadIndexOnce();
            }
            catch (WebException)
            { /* Ignore HTTP errors. If failed, segments_.Count == 0 */}

            // If there are no segments, signall as not ready
            return (segments_.Count > 0);
        }
    }
}