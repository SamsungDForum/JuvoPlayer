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

            Duration = media?.Period?.Duration;

            DownloadIndexOnce();

            // If media.Period.Duration has no value (not specified by Manifest), 
            // try to guess duration from index information 
            if (Duration.HasValue == false)
            {
                if (segments_.Count > 0)
                {
                    var lastEntry = segments_.Count - 1;

                    Duration = segments_[lastEntry].Period.Start + segments_[lastEntry].Period.Duration;
                }
            }
        }

        protected static LoggerManager LogManager = LoggerManager.GetInstance();
        protected static ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        private ManifestParameters Parameters;

        private bool indexDownloaded;
        private Segment media_;

        public TimeSpan? Duration { get; }

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
                return (uint)segments_.Count;
            }
        }
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

        public Segment MediaSegment(uint? segmentId)
        {
            if (media_ == null || !segmentId.HasValue)
                return null;

            if (segments_.Count == 0)
            {
                Logger.Info(string.Format("No index data for {0}", media_.Url.ToString()));
                return media_;
            }

            if (segmentId >= segments_.Count)
                return null;

            return segments_[(int)segmentId];
        }

        public uint? SegmentId(TimeSpan pointInTime)
        {
            if (media_ == null)
                return null;

            if (media_.Contains(pointInTime) <= TimeRelation.EARLIER)
                return null;

            // TODO: Values are sorted. Convert to binary search.
            for (int i = 0; i < segments_.Count; ++i)
            {
                if (segments_[i].Period.Start + segments_[i].Period.Duration >= pointInTime)
                    return (uint)i;
            }
            return null;
        }

        private uint? GetStartSegmentDynamic(TimeSpan pointInTime)
        {
            var availStart = (Parameters.Document.AvailabilityStartTime ?? DateTime.MinValue);
            var liveTimeIndex = (availStart + pointInTime + Parameters.PlayClock) - availStart;

            return SegmentId(liveTimeIndex);
        }

        private uint GetStartSegmentStatic(TimeSpan pointInTime)
        {
            return 0;
        }

        public uint? StartSegmentId(TimeSpan pointInTime, TimeSpan bufferDepth)
        {
            if (media_ == null)
                return null;

            //TODO: Take into account @startNumber if available
            if (Parameters.Document.IsDynamic == true)
                return GetStartSegmentDynamic(pointInTime);

            return GetStartSegmentStatic(pointInTime);
        }

        public IEnumerable<Segment> MediaSegments()
        {
            if (segments_.Count == 0 && media_ != null)
                return new List<Segment>() { media_ };
            return segments_;
        }

        public uint? NextSegmentId(uint? segmentId)
        {
            var nextSegmentId = segmentId.HasValue ? segmentId + 1 : Count;

            if (nextSegmentId < 0 || nextSegmentId >= Count)
                return null;

            return nextSegmentId;
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
            if (!segmentId.HasValue || segmentId < 0 || segmentId >= Count)
                return null;

            // Returned TimeRange via a copy. Intentional.
            // If Manifest gets updated it is undesired to have wierd values in it.
            //
            return segments_[(int)segmentId].Period.Copy();
        }
    }
}