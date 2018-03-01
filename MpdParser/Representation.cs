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

using JuvoPlayer.Common.Logging;

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
        
        public BaseRepresentationStream(Segment init, Segment media, Segment index = null)
        {
            media_ = media;
            InitSegment = init;
            IndexSegment = index;
            /*
            if (media_ == null)
            {
                Count = 0u;
            }
            else
            {
                Count = 1u;
            }
            */
            // Why this line is throwing TypeInitializationException..
            // beats me...
            Count = media == null ? 0u : 1u;
            Duration = media?.Period?.Duration;
            

            //Index Download could be changed to "lazy loading"
            //done before first actual use (calls to IRepresentationStream defined API)
            DownloadIndex(true);
        }

        protected static LoggerManager LogManager = LoggerManager.GetInstance();
        protected static ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        private ManualResetEvent DownloadWait = null;

        //Instance of a downloader is kept here for sole purpose
        //of doing async download cancelation if object will be destroyed before 
        //download completes.
        private NetClient Downloader = null;

        private Segment media_;

        private List<SIDXAtom> sidxs = null;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public Segment IndexSegment { get; }
        public uint Count { get; }

        ~BaseRepresentationStream()
        {
            //Cancel any pending request (i.e. Donwloader non null)
            if( Downloader != null )
            {
                Downloader.CancelAsync();
            }
        }
        private void DownloadIndex(bool async)
        {
            //Create index storage only if index segment is provided
            if(IndexSegment == null) return;
              
            ByteRange rng = new ByteRange(IndexSegment.ByteRange);

            try
            {
                if (async)
                {

                    Logger.Info( string.Format("Index Segment present. Attempting ASYNC download"));
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
            catch(Exception ex)
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
            if (sidxs == null)
            {
                sidxs = new List<SIDXAtom>();
            }

            SIDXAtom atm = new SIDXAtom();
            atm.ParseAtom(data, dataStart + 1);
            sidxs.Add(atm);

            //TODO:
            //SIDXAtom.SIDX_index_entry should contain a list of other sidx atoms containing
            //with index information. They could be loaded by updating range info in current
            //streamSegment and recursively calling DownloadIndexSegment - but about that we can worry later...
            //TO REMEMBER:
            //List of final sidxs should be "sorted" from low to high. In case of one it is not an issue,
            //it may be in case of N index boxes in hierarchy order (daisy chain should be ok too I think...)
            //so just do sanity check if we have such chunks
            if (atm.SIDXIndexCount > 0)
            {
                throw new NotImplementedException("Daisy chained / Hierarchical chunks not implemented...");
            }
        }
        public Segment MediaSegmentAtPos(uint pos)
        {
            Logger.Info(string.Format("MediaSegmentAtPos {0}", pos));

            Segment res = null;

            if(media_ == null)
            {
                return null;
            }

            DownloadWait?.WaitOne();
            
            if (sidxs == null)
            {
                Logger.Info(string.Format("No index data for {0}", media_.Url.ToString()));
                return media_;
            }

            foreach (SIDXAtom sidx in sidxs)
            {
                if(pos >= sidx.MovieIndexCount)
                {
                    pos -= sidx.MovieIndexCount;
                    continue;
                }

                UInt64 lb;
                UInt64 hb;
                TimeSpan starttime;
                TimeSpan duration;

                (lb,hb, starttime, duration) = sidx.GetRangeData(pos);
                if (lb != hb)
                {
                    string rng = lb.ToString() + "-" + hb.ToString();

                    res = new Segment(media_.Url, rng,
                                        new TimeRange(starttime,duration));

                    Logger.Info(string.Format("Range {0}-{1} set {2} POS={3} StartTime={4} Duration={5} {6}",
                                    lb, hb, rng, pos, 
                                    starttime,
                                    duration,
                                    media_.Url.ToString()));
                    break;
                }
            }

            return res;
        }

        public uint? MediaSegmentAtTime(TimeSpan duration)
        {
            Logger.Info(string.Format("MediaSegmentAtTime {0}", duration));
            if (media_ == null) return null;
            if (media_.Contains(duration) <= TimeRelation.EARLIER) return null;
            
            DownloadWait?.WaitOne();
      
            return GetRangeIndex(duration);
        }

        public IEnumerable<Segment> MediaSegments()
        {
            if (media_ != null)
                yield return media_;
        }

        protected uint? GetRangeIndex(TimeSpan curr)
        {
            Logger.Info(string.Format("GetRangeIndex {0}", curr));
            uint skipcount = 0;
            uint? idx =null;

            if (sidxs != null)
            {
                foreach (SIDXAtom sidx in sidxs)
                {
                    if (curr < sidx.MaxIndexTime)
                    {
                        idx = sidx.GetRangeDurationIndex(curr);

                        // GetRangeDurationIndex may return NULL if ID is not found.
                        // adding a value to null variable should result in null
                        // (expected bahviour) - indication that index entry is not found
                        idx += (uint)skipcount;
                        break;
                    }
                    else
                    {
                        skipcount += sidx.MovieIndexCount;
                    }
                }
            }
            if( idx == null)
            {
                idx = 0;
            }
            return idx;
        }
        protected (UInt64, UInt64, TimeSpan) GetRangeDuration(TimeSpan curr)
        {
            UInt64 lb = 0;
            UInt64 hb = 0;
            TimeSpan durr = default(TimeSpan);

            foreach (SIDXAtom sidx in sidxs)
            {
                (lb, hb, durr) = sidx.GetRangeDuration(curr);
                if (lb != hb) break;
            }

            if (lb == hb)
            {
                Logger.Warn(string.Format("Time Index {0} not found in indexing data", curr));
                foreach (SIDXAtom sidx in sidxs)
                {
                    sidx.DumpMovieIndex(curr);
                }

            }
            return (lb, hb, durr);
        }

    }

    public class TemplateRepresentationStream : IRepresentationStream
    {
        public TemplateRepresentationStream(Uri baseURL, Template init, Template media, uint? bandwidth, string reprId, uint timescale, TimelineItem[] timeline)
        {
            baseURL_ = baseURL;
            media_ = media;
            bandwidth_ = bandwidth;
            reprId_ = reprId;
            timescale_ = timescale;
            timeline_ = timeline;

            uint count = (uint)timeline.Length;
            ulong totalDuration = 0;
            foreach (TimelineItem item in timeline)
            {
                count += (uint)item.Repeats;
                ulong rightMost = item.Time + (1 + (ulong)item.Repeats) * item.Duration;
                if (rightMost > totalDuration)
                    totalDuration = rightMost;
            }

            Count = count;
            Duration = Scaled(totalDuration - (timeline.Length > 0 ? timeline[0].Time : 0));
            InitSegment = init == null ? null : MakeSegment(init.Get(bandwidth, reprId), null);
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
            ulong start = item.Time + item.Duration * repeat;
            string uri = media_.Get(bandwidth_, reprId_, item.Number + repeat, start);
            return MakeSegment(uri, new TimeRange(Scaled(start), Scaled(item.Duration)));
        }

        private Uri baseURL_;
        private Template media_;
        private uint? bandwidth_;
        private string reprId_;
        private uint timescale_;
        private TimelineItem[] timeline_;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public Segment IndexSegment { get; }
        public uint Count { get; }

        public Segment MediaSegmentAtPos(uint pos)
        {
            foreach (TimelineItem item in timeline_)
            {
                if (pos <= (uint)item.Repeats)
                    return MakeSegment(item, pos);
                pos -= (uint)item.Repeats;
                --pos;
            }
            return null;
        }

        public uint? MediaSegmentAtTime(TimeSpan durationSpan)
        {
            ulong duration = (ulong)Math.Ceiling(durationSpan.TotalSeconds * timescale_);
            uint pos = 0;
            foreach (TimelineItem item in timeline_)
            {
                TimeRelation rel = item.RepeatFor(duration, out uint repeat);
                if (rel > TimeRelation.EARLIER)
                    return pos + repeat;
                pos += (uint)item.Repeats + 1;
            }

            return null;
        }

        public IEnumerable<Segment> MediaSegments()
        {
            foreach (TimelineItem item in timeline_)
            {
                for (uint repeat = 0; repeat <= item.Repeats; ++repeat)
                    yield return MakeSegment(item, repeat);
            }
        }

        public void SetIndexData(byte[] rawData)
        {
            throw new NotImplementedException();
        }
    }

    public class ListRepresentationStream : IRepresentationStream
    {
        public ListRepresentationStream(Uri baseURL, Segment init, uint timescale, ListItem[] uris)
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

        private Uri baseURL_;
        private uint timescale_;
        private ListItem[] uris_;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public Segment IndexSegment { get; }
        public uint Count { get; }

        public Segment MediaSegmentAtPos(uint pos)
        {
            if (pos < uris_.Length)
                return MakeSegment(uris_[pos]);
            return null;
        }

        public uint? MediaSegmentAtTime(TimeSpan durationSpan)
        {
            ulong duration = (ulong)Math.Ceiling(durationSpan.TotalSeconds * timescale_);
            for (uint pos = 0; pos < uris_.Length; ++pos)
            {
                if (uris_[pos].Contains(duration) > TimeRelation.EARLIER)
                    return pos;
            }
            return null;
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