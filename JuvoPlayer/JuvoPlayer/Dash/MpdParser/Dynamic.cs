using System;
using System.Collections.Generic;

namespace MpdParser.Node.Dynamic
{
    public enum SegmentLocation
    {
        None,
        Period,
        Set,
        Repr
    }

    public class SegmentBaseTmplt<S> where S : Node.SegmentBase
    {
        protected S repr_;
        protected S set_;
        protected S period_;

        public uint? Timescale
        {
            get
            {
                return
                    repr_?.Timescale ??
                    set_?.Timescale ??
                    period_?.Timescale;
            }
        }
        public string IndexRange
        {
            get
            {
                return
                    repr_?.IndexRange ??
                    set_?.IndexRange ??
                    period_?.IndexRange;
            }
        }
        public bool IndexRangeExact
        {
            get
            {
                return
                    Flag(repr_?.IndexRangeExact) ||
                    Flag(set_?.IndexRangeExact) ||
                    Flag(period_?.IndexRangeExact);
            }
        }
        public ulong? PresentationTimeOffset
        {
            get
            {
                return
                    repr_?.PresentationTimeOffset ??
                    set_?.PresentationTimeOffset ??
                    period_?.PresentationTimeOffset;
            }
        }
        public double? AvailabilityTimeOffset
        {
            get
            {
                return
                    repr_?.AvailabilityTimeOffset ??
                    set_?.AvailabilityTimeOffset ??
                    period_?.AvailabilityTimeOffset;
            }
        }

        public bool AvailabilityTimeComplete
        {
            get
            {
                return
                    Flag(repr_?.AvailabilityTimeComplete) ||
                    Flag(set_?.AvailabilityTimeComplete) ||
                    Flag(period_?.AvailabilityTimeComplete);
            }
        }

        public URL[] Initializations
        {
            get
            {
                return
                    Nonempty(repr_?.Initializations) ??
                    Nonempty(set_?.Initializations) ??
                    Nonempty(period_?.Initializations) ??
                    new URL[] { };
            }
        }

        public URL[] RepresentationIndexes
        {
            get
            {
                return
                    Nonempty(repr_?.RepresentationIndexes) ??
                    Nonempty(set_?.RepresentationIndexes) ??
                    Nonempty(period_?.RepresentationIndexes) ??
                    new URL[] { };
            }
        }

        internal SegmentBaseTmplt(S repr, S set, S period)
        {
            repr_ = repr;
            set_ = set;
            period_ = period;
        }

        protected bool Flag(bool? f) { return f ?? false; }
        protected T[] Nonempty<T>(T[] array)
        {
            return (array?.Length ?? 0) > 0 ? array : null;
        }

        public SegmentLocation HighestAvailable()
        {
            if (repr_ != null)
                return SegmentLocation.Repr;
            if (set_ != null)
                return SegmentLocation.Set;
            if (period_ != null)
                return SegmentLocation.Period;
            return SegmentLocation.None;
        }

        public SegmentLocation NextAvailable(SegmentLocation below)
        {
            if (below == SegmentLocation.None)
                throw new Exception("Can't get lower, than SegmentLocation.None");

            if (below == SegmentLocation.Repr && set_ != null)
                return SegmentLocation.Set;
            if (below != SegmentLocation.Period && period_ != null)
                return SegmentLocation.Period;
            return SegmentLocation.None;
        }

        public S GetDirect(SegmentLocation which)
        {
            switch (which)
            {
                case SegmentLocation.Repr: return repr_;
                case SegmentLocation.Set: return set_;
                case SegmentLocation.Period: return period_;
            }
            return null;
        }
    }

    public class MultipleSegmentBaseTmplt<S> : SegmentBaseTmplt<S> where S : Node.MultipleSegmentBase
    {
        public uint? Duration
        {
            get
            {
                return
                    repr_?.Duration ??
                    set_?.Duration ??
                    period_?.Duration;
            }
        }

        public uint? StartNumber
        {
            get
            {
                return
                    repr_?.StartNumber ??
                    set_?.StartNumber ??
                    period_?.StartNumber;
            }
        }

        public SegmentTimeline[] SegmentTimelines
        {
            get
            {
                return
                    Nonempty(repr_?.SegmentTimelines) ??
                    Nonempty(set_?.SegmentTimelines) ??
                    Nonempty(period_?.SegmentTimelines) ??
                    new SegmentTimeline[] { };
            }
        }

        public URL[] BitstreamSwitchings
        {
            get
            {
                return
                    Nonempty(repr_?.BitstreamSwitchings) ??
                    Nonempty(set_?.BitstreamSwitchings) ??
                    Nonempty(period_?.BitstreamSwitchings) ??
                    new URL[] { };
            }
        }

        internal MultipleSegmentBaseTmplt(S repr, S set, S period) : base(repr, set, period)
        {
        }
    }

    public class SegmentBase : SegmentBaseTmplt<Node.SegmentBase>
    {
        public SegmentBase(Node.SegmentBase repr, Node.SegmentBase set, Node.SegmentBase period)
            : base(repr, set, period)
        {
        }
    }

    public class SegmentTemplate : MultipleSegmentBaseTmplt<Node.SegmentTemplate>
    {
        public Template Media
        {
            get
            {
                return
                    repr_?.Media ??
                    set_?.Media ??
                    period_?.Media;
            }
        }

        public Template Index
        {
            get
            {
                return
                    repr_?.Index ??
                    set_?.Index ??
                    period_?.Index;
            }
        }

        public Template Initialization
        {
            get
            {
                return
                    repr_?.Initialization ??
                    set_?.Initialization ??
                    period_?.Initialization;
            }
        }

        public string BitstreamSwitching
        {
            get
            {
                return
                    repr_?.BitstreamSwitching ??
                    set_?.BitstreamSwitching ??
                    period_?.BitstreamSwitching;
            }
        }

        public SegmentTemplate(Node.SegmentTemplate repr, Node.SegmentTemplate set, Node.SegmentTemplate period)
            : base(repr, set, period)
        {
        }
    }

    public class SegmentList : MultipleSegmentBaseTmplt<Node.SegmentList>
    {
        public SegmentURL[] SegmentURLs
        {
            get
            {
                return
                    Nonempty(repr_?.SegmentURLs) ??
                    Nonempty(set_?.SegmentURLs) ??
                    Nonempty(period_?.SegmentURLs) ??
                    new SegmentURL[] { };
            }
        }

        public SegmentList(Node.SegmentList repr, Node.SegmentList set, Node.SegmentList period)
            : base(repr, set, period)
        {
        }
    }

    public class TimeRange
    {
        public readonly TimeSpan start;
        public readonly TimeSpan duration;

        public TimeRange(TimeSpan start, TimeSpan duration)
        {
            this.start = start;
            this.duration = duration;
        }
    }

    internal enum TimeRelation
    {
        UNKNOWN = -2,
        EARLIER,
        SPOTON,
        LATER
    }
    public class Segment
    {
        public readonly Uri Url;
        public readonly string ByteRange;
        public readonly TimeRange Period;

        public Segment(Uri url, string range, TimeRange period = null)
        {
            Url = url;
            ByteRange = range;
            Period = period;
        }

        internal TimeRelation Contains(TimeSpan time_point)
        {
            if (Period == null)
                return TimeRelation.UNKNOWN;
            if (time_point < Period.start)
                return TimeRelation.LATER;
            time_point -= Period.start;
            return time_point <= Period.duration ? TimeRelation.SPOTON : TimeRelation.EARLIER;
        }
    }

    public class BaseRepresentationStream : IRepresentationStream
    {
        public BaseRepresentationStream(Segment init, Segment media)
        {
            media_ = media;
            InitSegment = init;
            Count = media == null ? 0u : 1u;
            Duration = media?.Period?.duration;
        }

        private Segment media_;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public uint Count { get; }

        public Segment MediaSegmentAtPos(uint pos)
        {
            if (pos == 0)
                return media_;
            return null;
        }

        public uint? MediaSegmentAtTime(TimeSpan duration)
        {
            if (media_ != null && media_.Contains(duration) > TimeRelation.EARLIER)
                return 0;
            return null;
        }

        public IEnumerable<Segment> MediaSegments()
        {
            if (media_ != null)
                yield return media_;
        }
    };

    public struct TimelineItem {
        public uint Number;
        public ulong Time;
        public ulong Duration;
        public int Repeats;

        internal TimeRelation RepeatFor(ulong point, out uint repeat)
        {
            repeat = 0;
            if (point < Time)
                return TimeRelation.LATER;
            if (point > Time + Duration * (ulong)(Repeats + 1))
                return TimeRelation.EARLIER;
            point -= Time;
            repeat = (uint)(point / Duration);
            return TimeRelation.SPOTON;
        }
    }

    public class Timeline
    {
        public static TimelineItem[] FromDuration(uint startNumber, TimeSpan start, TimeSpan duration, uint segDuration, uint timescale)
        {
            uint totalDuration = (uint)Math.Ceiling(duration.TotalSeconds * timescale);
            uint totalStart = (uint)Math.Ceiling(start.TotalSeconds * timescale);
            TimeSpan scaledDuration = TimeSpan.FromSeconds((double)segDuration / timescale);

            uint count = totalDuration / segDuration;
            uint end = count * segDuration;

            TimelineItem[] result = new TimelineItem[totalDuration == end ? 1 : 2];
            result[0].Number = startNumber;
            result[0].Time = totalStart;
            result[0].Duration = segDuration;
            result[0].Repeats = (int)count - 1;

            if (totalDuration != end)
            {
                result[1].Number = startNumber + count;
                result[1].Time = totalStart + end;
                result[1].Duration = totalDuration - end;
                result[1].Repeats = 0;
            }

            return result;
        }

        public static TimelineItem[] FromXml(uint startNumber, TimeSpan periodStart, TimeSpan? periodEnd, uint timescale, S[] esses)
        {
            ulong offset = (ulong)Math.Ceiling(periodStart.TotalSeconds * timescale);
            ulong start = 0;
            TimelineItem[] result = new TimelineItem[esses.Length];
            for (int i = 0; i < esses.Length; ++i)
            {
                S s = esses[i];
                if (s.D == null)
                    return null;

                start = s.T ?? start;

                uint count = 1;
                int rep = s.R ?? -1; // non-existing and invalid @r should be treated the same:
                if (rep < 0)
                {
                    if (i < (esses.Length - 1))
                    {
                        // if t[s+1] is present, then r[s] is the ceil of (t[s+1] - t[s])/d[s]
                        ulong nextStart = esses[i + 1].T ?? (start + s.D.Value);
                        ulong chunks = (ulong)Math.Ceiling(((double)nextStart - start) / s.D.Value);
                        rep = (int)chunks - 1;
                    }
                    else
                    {
                        // else r[s] is the ceil of (PEwc[i] - PSwc[i] - t[s]/ts)*ts/d[s])
                        ulong totalEnd = periodEnd == null ? start + s.D.Value :
                            (ulong)Math.Ceiling(periodEnd.Value.TotalSeconds * timescale);
                        ulong chunks = (ulong)Math.Ceiling(((double)totalEnd - offset - start) / s.D.Value);
                        rep = (int)chunks - 1;
                    }
                }
                count += (uint)rep;

                result[i].Number = startNumber;
                result[i].Time = start + offset;
                result[i].Duration = s.D.Value;
                result[i].Repeats = (int)count - 1;

                start += s.D.Value * count;
                startNumber += count;
            }
            return result;
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
            string uri = media_.Get(bandwidth_, reprId_, item.Number + repeat, (uint)start);
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
    }

    public struct ListItem
    {
        public string Media;
        public string Range;
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

        public static ListItem[] FromXml(uint startNumber, TimeSpan startPoint, uint timescale, ulong duration, SegmentURL[] urls)
        {
            ulong start = (ulong)Math.Ceiling(startPoint.TotalSeconds * timescale);
            int size = 0;
            for (int i = 0; i < urls.Length; ++i)
            {
                if (string.IsNullOrEmpty(urls[i].Media))
                    continue;
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