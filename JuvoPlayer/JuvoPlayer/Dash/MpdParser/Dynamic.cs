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
            Length = media?.Period?.duration;
        }

        private Segment media_;

        public TimeSpan? Length { get; }
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
            if (point > Time + Duration * (ulong)(Repeats + 1))
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
            ulong totalDuration = 0;
            foreach (TimelineItem item in timeline)
            {
                count += item.Repeats;
                ulong rightMost = item.Time + (1 + (ulong)item.Repeats) * item.Duration;
                if (rightMost > totalDuration)
                    totalDuration = rightMost;
            }

            Count = count;
            Length = Scaled(totalDuration - (timeline.Length > 0 ? timeline[0].Time : 0));
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

        public TimeSpan? Length { get; }
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

    public struct ListItem
    {
        public string Media;
        public string Range;
        public ulong Time;
        public ulong Duration;

        public bool Contains(ulong timepoint)
        {
            return timepoint >= Time && ((timepoint - Time) <= Duration);
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

            Count = uris.Length;
            Length = Scaled(totalDuration - (uris_.Length > 0 ? uris_[0].Time : 0));
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

        public TimeSpan? Length { get; }
        public Segment InitSegment { get; }
        public int Count { get; }

        public Segment MediaSegmentAtPos(int pos)
        {
            if (pos < uris_.Length)
                return MakeSegment(uris_[pos]);
            return null;
        }

        public Segment MediaSegmentAtTime(TimeSpan durationSpan)
        {
            ulong duration = (ulong)Math.Ceiling(durationSpan.TotalSeconds * timescale_);
            foreach (ListItem item in uris_)
            {
                if (!item.Contains(duration))
                    continue;
                return MakeSegment(item);
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