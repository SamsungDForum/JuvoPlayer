using System;
using System.Collections.Generic;
using System.Linq;
using JuvoPlayer.Dash.MpdParser.Node;

namespace JuvoPlayer.Dash.MpdParser.Dynamic
{
    public enum SegmentLocation
    {
        None,
        Period,
        Set,
        Repr
    }

    public class SegmentBaseTmplt<TSegment>
        where TSegment : Node.SegmentBase
    {
        protected TSegment Representation;
        protected TSegment AdaptationSet;
        protected TSegment Period;

        public uint? Timescale => Representation?.Timescale ??
                                  AdaptationSet?.Timescale ??
                                  Period?.Timescale;

        public string IndexRange => Representation?.IndexRange ??
                                    AdaptationSet?.IndexRange ??
                                    Period?.IndexRange;

        public bool IndexRangeExact => Flag(Representation?.IndexRangeExact) ||
                    Flag(AdaptationSet?.IndexRangeExact) ||
                    Flag(Period?.IndexRangeExact);
        public ulong? PresentationTimeOffset => Representation?.PresentationTimeOffset ??
                                                AdaptationSet?.PresentationTimeOffset ??
                                                Period?.PresentationTimeOffset;

        public double? AvailabilityTimeOffset => Representation?.AvailabilityTimeOffset ??
                                                 AdaptationSet?.AvailabilityTimeOffset ??
                                                 Period?.AvailabilityTimeOffset;

        public bool AvailabilityTimeComplete => Flag(Representation?.AvailabilityTimeComplete) ||
                                                Flag(AdaptationSet?.AvailabilityTimeComplete) ||
                                                Flag(Period?.AvailabilityTimeComplete);

        public URL[] Initializations => Nonempty(Representation?.Initializations) ??
                                        Nonempty(AdaptationSet?.Initializations) ??
                                        Nonempty(Period?.Initializations) ??
                                        new URL[] { };

        public URL[] RepresentationIndexes => Nonempty(Representation?.RepresentationIndexes) ??
                                              Nonempty(AdaptationSet?.RepresentationIndexes) ??
                                              Nonempty(Period?.RepresentationIndexes) ??
                                              new URL[] { };

        internal SegmentBaseTmplt(
            TSegment representation,
            TSegment adaptationSet,
            TSegment period)
        {
            Representation = representation;
            AdaptationSet = adaptationSet;
            Period = period;
        }

        protected bool Flag(bool? f) { return f ?? false; }
        protected T[] Nonempty<T>(T[] array)
        {
            return (array?.Length ?? 0) > 0 ? array : null;
        }

        public SegmentLocation HighestAvailable()
        {
            if (Representation != null)
                return SegmentLocation.Repr;
            if (AdaptationSet != null)
                return SegmentLocation.Set;
            if (Period != null)
                return SegmentLocation.Period;
            return SegmentLocation.None;
        }

        public SegmentLocation NextAvailable(SegmentLocation below)
        {
            switch (below)
            {
                case SegmentLocation.None:
                    throw new Exception("Can't get lower, than SegmentLocation.None");
                case SegmentLocation.Repr when AdaptationSet != null:
                    return SegmentLocation.Set;
                case SegmentLocation.Period:
                    break;
                case SegmentLocation.Set:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(below), below, null);
            }
            if (below != SegmentLocation.Period && Period != null)
                return SegmentLocation.Period;
            return SegmentLocation.None;
        }

        public TSegment GetDirect(SegmentLocation which)
        {
            switch (which)
            {
                case SegmentLocation.Repr:
                    return Representation;
                case SegmentLocation.Set:
                    return AdaptationSet;
                case SegmentLocation.Period:
                    return Period;
                case SegmentLocation.None:
                    return null;
                default:
                    return null;
            }
        }
    }

    public class MultipleSegmentBaseTmplt<TSegment> : SegmentBaseTmplt<TSegment>
        where TSegment : MultipleSegmentBase
    {
        public uint? Duration => Representation?.Duration ??
                                 AdaptationSet?.Duration ??
                                 Period?.Duration;

        public uint? StartNumber => Representation?.StartNumber ??
                                    AdaptationSet?.StartNumber ??
                                    Period?.StartNumber;

        public SegmentTimeline[] SegmentTimelines => Nonempty(Representation?.SegmentTimelines) ??
                                                     Nonempty(AdaptationSet?.SegmentTimelines) ??
                                                     Nonempty(Period?.SegmentTimelines) ??
                                                     new SegmentTimeline[] { };

        public URL[] BitstreamSwitchings => Nonempty(Representation?.BitstreamSwitchings) ??
                                            Nonempty(AdaptationSet?.BitstreamSwitchings) ??
                                            Nonempty(Period?.BitstreamSwitchings) ??
                                            new URL[] { };

        internal MultipleSegmentBaseTmplt(
            TSegment representation,
            TSegment adaptationSet,
            TSegment period): base(representation, adaptationSet, period)
        {
        }
    }

    public class SegmentBase : SegmentBaseTmplt<Node.SegmentBase>
    {
        public SegmentBase(
            Node.SegmentBase representation,
            Node.SegmentBase adaptationSet,
            Node.SegmentBase period): base(representation, adaptationSet, period)
        {
        }
    }

    public class SegmentTemplate : MultipleSegmentBaseTmplt<Node.SegmentTemplate>
    {
        public Template Media => Representation?.Media ??
                                 AdaptationSet?.Media ??
                                 Period?.Media;

        public Template Index => Representation?.Index ??
                                 AdaptationSet?.Index ??
                                 Period?.Index;

        public Template Initialization => Representation?.Initialization ??
                                          AdaptationSet?.Initialization ??
                                          Period?.Initialization;

        public string BitstreamSwitching => Representation?.BitstreamSwitching ??
                                            AdaptationSet?.BitstreamSwitching ??
                                            Period?.BitstreamSwitching;

        public SegmentTemplate(
            Node.SegmentTemplate representation,
            Node.SegmentTemplate adaptationSet,
            Node.SegmentTemplate period): base(representation, adaptationSet, period)
        {
        }
    }

    public class SegmentList : MultipleSegmentBaseTmplt<Node.SegmentList>
    {
        public SegmentURL[] SegmentUrls => Nonempty(Representation?.SegmentURLs) ??
                                           Nonempty(AdaptationSet?.SegmentURLs) ??
                                           Nonempty(Period?.SegmentURLs) ??
                                           new SegmentURL[] { };

        public SegmentList(
            Node.SegmentList representation,
            Node.SegmentList adaptationSet,
            Node.SegmentList period): base(representation, adaptationSet, period)
        {
        }
    }

    public class TimeRange
    {
        public readonly TimeSpan Start;
        public readonly TimeSpan Duration;

        public TimeRange(TimeSpan start, TimeSpan duration)
        {
            Start = start;
            Duration = duration;
        }
    }

    internal enum TimeRelation
    {
        Unknown = -2,
        Earlier,
        Spoton,
        Later
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

        internal TimeRelation Contains(TimeSpan timePoint)
        {
            if (Period == null)
                return TimeRelation.Unknown;
            if (timePoint < Period.Start)
                return TimeRelation.Later;
            timePoint -= Period.Start;
            return timePoint <= Period.Duration ? TimeRelation.Spoton : TimeRelation.Earlier;
        }
    }

    public class BaseRepresentationStream : IRepresentationStream
    {
        public BaseRepresentationStream(Segment init, Segment media)
        {
            _media = media;
            InitSegment = init;
            Count = media == null ? 0u : 1u;
            Duration = media?.Period?.Duration;
        }

        private readonly Segment _media;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public uint Count { get; }

        public Segment MediaSegmentAtPos(uint pos)
        {
            return pos == 0 ? _media : null;
        }

        public uint? MediaSegmentAtTime(TimeSpan duration)
        {
            if (_media != null && _media.Contains(duration) > TimeRelation.Earlier)
                return 0;
            return null;
        }

        public IEnumerable<Segment> MediaSegments()
        {
            if (_media != null)
                yield return _media;
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
                return TimeRelation.Later;
            if (point > Time + Duration * (ulong)(Repeats + 1))
                return TimeRelation.Earlier;
            point -= Time;
            repeat = (uint)(point / Duration);
            return TimeRelation.Spoton;
        }
    }

    public class Timeline
    {
        public static TimelineItem[] FromDuration(
            uint startNumber,
            TimeSpan start,
            TimeSpan duration,
            uint segDuration,
            uint timescale)
        {
            var totalDuration = (uint)Math.Ceiling(duration.TotalSeconds * timescale);
            var totalStart = (uint)Math.Ceiling(start.TotalSeconds * timescale);

            var count = totalDuration / segDuration;
            var end = count * segDuration;

            var result = new TimelineItem[totalDuration == end ? 1 : 2];
            result[0].Number = startNumber;
            result[0].Time = totalStart;
            result[0].Duration = segDuration;
            result[0].Repeats = (int)count - 1;

            if (totalDuration == end) return result;
            result[1].Number = startNumber + count;
            result[1].Time = totalStart + end;
            result[1].Duration = totalDuration - end;
            result[1].Repeats = 0;

            return result;
        }

        public static TimelineItem[] FromXml(
            uint startNumber,
            TimeSpan periodStart,
            TimeSpan? periodEnd,
            uint timescale,
            S[] esses)
        {
            var offset = (ulong)Math.Ceiling(periodStart.TotalSeconds * timescale);
            ulong start = 0;
            var result = new TimelineItem[esses.Length];
            for (var i = 0; i < esses.Length; ++i)
            {
                var segment = esses[i];
                if (segment.D == null)
                    return null;

                start = segment.T ?? start;

                uint count = 1;
                var rep = segment.R ?? -1; // non-existing and invalid @r should be treated the same:
                if (rep < 0)
                {
                    if (i < (esses.Length - 1))
                    {
                        // if t[segment+1] is present, then r[segment] is the ceil of (t[segment+1] - t[segment])/d[segment]
                        var nextStart = esses[i + 1].T ?? start + segment.D.Value;
                        var chunks = (ulong)Math.Ceiling(((double)nextStart - start) / segment.D.Value);
                        rep = (int)chunks - 1;
                    }
                    else
                    {
                        // else r[segment] is the ceil of (PEwc[i] - PSwc[i] - t[segment]/ts)*ts/d[segment])
                        var totalEnd = periodEnd == null ?
                            start + segment.D.Value :
                            (ulong)Math.Ceiling(periodEnd.Value.TotalSeconds * timescale);
                        var chunks = (ulong)Math.Ceiling(((double)totalEnd - offset - start) / segment.D.Value);
                        rep = (int)chunks - 1;
                    }
                }
                count += (uint)rep;

                result[i].Number = startNumber;
                result[i].Time = start + offset;
                result[i].Duration = segment.D.Value;
                result[i].Repeats = (int)count - 1;

                start += segment.D.Value * count;
                startNumber += count;
            }
            return result;
        }
    }

    public class TemplateRepresentationStream : IRepresentationStream
    {
        public TemplateRepresentationStream(
            Uri baseUrl,
            Template init,
            Template media,
            uint? bandwidth,
            string reprId,
            uint timescale,
            TimelineItem[] timeline)
        {
            _baseUrl = baseUrl;
            _media = media;
            _bandwidth = bandwidth;
            _reprId = reprId;
            _timescale = timescale;
            _timeline = timeline;

            var count = (uint)timeline.Length;
            ulong totalDuration = 0;
            foreach (var item in timeline)
            {
                count += (uint)item.Repeats;
                var rightMost = item.Time + (1 + (ulong)item.Repeats) * item.Duration;
                if (rightMost > totalDuration)
                    totalDuration = rightMost;
            }

            Count = count;
            Duration = Scaled(totalDuration - (timeline.Length > 0 ? timeline[0].Time : 0));
            InitSegment = init == null ?
                null : MakeSegment(init.Get(bandwidth, reprId), null);
        }

        private Segment MakeSegment(string url, TimeRange span)
        {
            Uri file;
            if (_baseUrl == null)
                file = new Uri(url, UriKind.RelativeOrAbsolute);
            else if (!Uri.TryCreate(_baseUrl, url, out file))
                return null;
            return new Segment(file, null, span);
        }

        private TimeSpan Scaled(ulong point)
        {
            return TimeSpan.FromSeconds((double)point / _timescale);
        }
        private Segment MakeSegment(TimelineItem item, uint repeat)
        {
            ulong start = item.Time + item.Duration * repeat;
            string uri = _media.Get(_bandwidth, _reprId, item.Number + repeat, (uint)start);
            return MakeSegment(uri, new TimeRange(Scaled(start), Scaled(item.Duration)));
        }

        private readonly Uri _baseUrl;
        private readonly Template _media;
        private readonly uint? _bandwidth;
        private readonly string _reprId;
        private readonly uint _timescale;
        private readonly TimelineItem[] _timeline;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public uint Count { get; }

        public Segment MediaSegmentAtPos(uint pos)
        {
            foreach (var timelineItem in _timeline)
            {
                if (pos <= (uint)timelineItem.Repeats)
                    return MakeSegment(timelineItem, pos);
                pos -= (uint)timelineItem.Repeats;
                --pos;
            }
            return null;
        }

        public uint? MediaSegmentAtTime(TimeSpan durationSpan)
        {
            var duration = (ulong)Math.Ceiling(durationSpan.TotalSeconds * _timescale);
            uint pos = 0;
            foreach (var timelineItem in _timeline)
            {
                var rel = timelineItem.RepeatFor(duration, out var repeat);
                if (rel > TimeRelation.Earlier)
                    return pos + repeat;
                pos += (uint)timelineItem.Repeats + 1;
            }
            return null;
        }

        public IEnumerable<Segment> MediaSegments()
        {
            foreach (var item in _timeline)
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
                return TimeRelation.Later;
            if (timepoint - Time > Duration)
                return TimeRelation.Earlier;
            return TimeRelation.Spoton;
        }

        public static ListItem[] FromXml(
            uint startNumber,
            TimeSpan startPoint,
            uint timescale,
            ulong duration,
            SegmentURL[] urls)
        {
            var start = (ulong)Math.Ceiling(startPoint.TotalSeconds * timescale);
            var size = urls.Count(url => !string.IsNullOrEmpty(url.Media));
            var result = new ListItem[size];

            var pos = 0;
            foreach (var url in urls)
            {
                if (string.IsNullOrEmpty(url.Media))
                    continue;

                result[pos].Media = url.Media;
                result[pos].Range = url.MediaRange;
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
        public ListRepresentationStream(
            Uri baseUrl,
            Segment init,
            uint timescale,
            ListItem[] uris)
        {
            _baseUrl = baseUrl;
            _timescale = timescale;
            _uris = uris ?? new ListItem[] { };

            ulong totalDuration = 0;
            foreach (var item in _uris)
            {
                var rightMost = item.Time + item.Duration;
                if (rightMost > totalDuration)
                    totalDuration = rightMost;
            }

            if (uris != null) Count = (uint) uris.Length;
            Duration = Scaled(totalDuration - (_uris.Length > 0 ? _uris[0].Time : 0));
            InitSegment = init;
        }

        private Segment MakeSegment(string media, string range, TimeRange span)
        {
            Uri file;
            if (_baseUrl == null)
                file = new Uri(media, UriKind.RelativeOrAbsolute);
            else if (!Uri.TryCreate(_baseUrl, media, out file))
                return null;
            return new Segment(file, range, span);
        }

        private TimeSpan Scaled(ulong point)
        {
            return TimeSpan.FromSeconds((double)point / _timescale);
        }
        private Segment MakeSegment(ListItem item)
        {
            return MakeSegment(
                item.Media,
                item.Range,
                new TimeRange(Scaled(item.Time),
                Scaled(item.Duration)));
        }

        private readonly Uri _baseUrl;
        private readonly uint _timescale;
        private readonly ListItem[] _uris;

        public TimeSpan? Duration { get; }
        public Segment InitSegment { get; }
        public uint Count { get; }

        public Segment MediaSegmentAtPos(uint pos)
        {
            if (pos < _uris.Length)
                return MakeSegment(_uris[pos]);
            return null;
        }

        public uint? MediaSegmentAtTime(TimeSpan durationSpan)
        {
            var duration = (ulong)Math.Ceiling(durationSpan.TotalSeconds * _timescale);
            for (uint pos = 0; pos < _uris.Length; ++pos)
            {
                if (_uris[pos].Contains(duration) > TimeRelation.Earlier)
                    return pos;
            }
            return null;
        }

        public IEnumerable<Segment> MediaSegments()
        {
            return _uris.Select(MakeSegment);
        }
    }
}