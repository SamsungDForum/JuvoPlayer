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

        public TimeRange(TimeSpan start, TimeSpan stop)
        {
            this.start = start;
            this.duration = stop;
        }
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

        internal bool Contains(TimeSpan time_point)
        {
            if (Period == null)
                return false;
            if (time_point < Period.start)
                return false;
            time_point -= Period.start;
            return time_point <= Period.duration;
        }
    }

    public class BaseRepresentationStream : IRepresentationStream
    {
        public BaseRepresentationStream(Segment init, Segment media)
        {
            media_ = media;
            InitSegment = init;
            Count = media == null ? 0 : 1;
        }

        private Segment media_;

        public Segment InitSegment { get; }
        public int Count { get; }

        public Segment MediaSegmentAtPos(int pos)
        {
            if (pos < 1)
                return media_;
            return null;
        }

        public Segment MediaSegmentAtTime(TimeSpan duration)
        {
            if (media_ != null && media_.Contains(duration))
                return media_;
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

        public uint? RepeatFor(ulong point)
        {
            if (point < Time)
                return null;
            if (point >= Time + Duration * (ulong)(Repeats + 1))
                return null;
            point -= Time;
            return (uint)(point / Duration);
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

        public static TimelineItem[] FromXml(uint startNumber, TimeSpan startPoint, uint timescale, S[] esses)
        {
            ulong start = (ulong)Math.Ceiling(startPoint.TotalSeconds * timescale);
            TimelineItem[] result = new TimelineItem[esses.Length];
            for (int i = 0; i < esses.Length; ++i)
            {
                S s = esses[i];
                if (s.D == null)
                    return null;

                uint count = 1;
                int rep = s.R ?? 0;
                if (rep < 0)
                    rep = 0;
                count += (uint)rep;

                start = s.T ?? start;

                result[i].Number = startNumber;
                result[i].Time = start;
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

            int count = timeline.Length;
            foreach (TimelineItem item in timeline)
                count += item.Repeats;

            Count = count;

            InitSegment = MakeSegment(init.Get(bandwidth, reprId), null);
        }

        private Segment MakeSegment(string url, TimeRange span)
        {
            Uri file;
            if (baseURL_ == null)
                file = new Uri(url, UriKind.RelativeOrAbsolute);
            else if (!Uri.TryCreate(baseURL_, url, out file))
                return null;
            return new Segment(file, null, span); // new TimeRange(item.Time, item.Time + item.Duration));
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

        public Segment InitSegment { get; }
        public int Count { get; }

        public Segment MediaSegmentAtPos(int pos)
        {
            int true_pos = 0;
            while (true_pos < timeline_.Length &&
                timeline_[true_pos].Number < pos)
            {
                ++true_pos;
            }
            if (true_pos < 0)
                return null;
            --true_pos;

            TimelineItem item = timeline_[true_pos];
            uint repeat = (uint)pos - item.Number;
            if (repeat > item.Repeats)
                return null;
            return MakeSegment(item, repeat);
        }

        public Segment MediaSegmentAtTime(TimeSpan durationSpan)
        {
            ulong duration = (ulong)Math.Ceiling(durationSpan.TotalSeconds * timescale_);
            foreach(TimelineItem item in timeline_)
            {
                uint? repeat = item.RepeatFor(duration);
                if (repeat == null)
                    continue;
                return MakeSegment(item, repeat.Value);
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
}