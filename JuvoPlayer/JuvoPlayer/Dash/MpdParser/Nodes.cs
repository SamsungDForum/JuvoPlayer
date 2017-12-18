using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JuvoPlayer.Dash.MpdParser.Dynamic;

// ReSharper disable InconsistentNaming

namespace JuvoPlayer.Dash.MpdParser.Node
{
    public class MpdUri
    {
        public Uri Uri { get; }

        public MpdUri()
        {
            // mInternal = new Uri("", UriKind.Relative);
        }

        public MpdUri(string path)
        {
            Uri = new Uri(path, UriKind.RelativeOrAbsolute);
        }

        public MpdUri(Uri path)
        {
            Uri = path;
        }

        public MpdUri With(MpdUri child)
        {
            if (Uri == null)
                return child;
            if (child?.Uri == null) return this;
            return Uri.TryCreate(Uri, child.Uri, out var result) ?
                new MpdUri(result) :
                this;
        }

        public MpdUri With(string child)
        {
            return child == null ?
                null :
                With(new Uri(child, UriKind.RelativeOrAbsolute));
        }

        public MpdUri With(Uri child)
        {
            if (child == null)
                return null;
            return Uri.TryCreate(Uri, child, out var result) ?
                new MpdUri(result):
                null;
        }

        public static MpdUri FromBaseUrls(BaseURL[] urls)
        {
            return (from url in urls
                    where !string.IsNullOrEmpty(url.BaseUrlValue)
                    select new MpdUri(url.BaseUrlValue)).FirstOrDefault();
        }
    };

    public class Formatter
    {
        public List<int> Positions;
        public string TrueKey;
        private readonly int _pad;
        private readonly char _fill;

        public Formatter(string key)
        {
            Positions = new List<int>();
            var keyFmt = key.Split('%');
            TrueKey = keyFmt[0];
            _pad = 1;
            _fill = '0';

            if (keyFmt.Length <= 1 || string.IsNullOrEmpty(keyFmt[1])) return;
            var fmt = keyFmt[1];
            var ndx = 0;
            if (fmt[0] < '1' || fmt[0] > '9')
            {
                _fill = fmt[0];
                ndx = 1;
            }
            _pad = 0;
            while (fmt[ndx] >= '0' && fmt[ndx] <= '9')
            {
                _pad *= 10;
                _pad += fmt[ndx] - '0';
                ++ndx;
            }
            if (_pad < 1)
                _pad = 1;
        }

        public string GetValue(object value)
        {
            if (value == null)
            {
                if (string.IsNullOrEmpty(TrueKey))
                    return "$"; // $$ escapes a dollar sign
                return "$" + TrueKey + "$"; // de
            }

            return value.ToString().PadLeft(_pad, _fill);
        }
    }

    public class Template
    {
        private string Get(IReadOnlyDictionary<string, object> args)
        {
            var result = new string[_chunks.Length];
            _chunks.CopyTo(result, 0);
            for (var i = 0; i < _chunks.Length; ++i)
            {
                if (i % 2 == 0)
                    continue;
                result[i] = "$"; // assume empty
            }

            foreach (var key in _keys.Keys)
            {
                var fmt = _keys[key];

                var value = fmt.GetValue(
                    args.TryGetValue(fmt.TrueKey, out var arg) ? arg : null);

                foreach (var i in fmt.Positions)
                {
                    result[i] = value;
                }
            }
            return string.Join("", result);
        }

        public string Get(uint? bandwidth, string reprId)
        {
            var dict = new Dictionary<string, object>();
            if (bandwidth != null)
                dict.Add("Bandwidth", bandwidth.Value);
            if (reprId != null)
                dict.Add("RepresentationID", reprId);
            return Get(dict);
        }

        public string Get(uint? bandwidth, string reprId, uint number, uint time)
        {
            var dict = new Dictionary<string, object>
            {
                ["Number"] = number,
                ["Time"] = time
            };
            if (bandwidth != null)
                dict.Add("Bandwidth", bandwidth.Value);
            if (reprId != null)
                dict.Add("RepresentationID", reprId);
            return Get(dict);
        }

        private readonly string[] _chunks;
        private readonly Dictionary<string, Formatter> _keys =
            new Dictionary<string, Formatter>();
        public Template(string text)
        {
            _chunks = text.Split('$');
            for (var i = 0; i < _chunks.Length; ++i)
            {
                if (i % 2 == 0)
                    continue;
                var chunk = _chunks[i];
                if (!_keys.ContainsKey(chunk))
                    _keys.Add(chunk, new Formatter(chunk));
                _keys[chunk].Positions.Add(i);
            }
        }

        public override string ToString()
        {
            return Get(null, null);
        }
    }

    public class Representation : RepresentationBase
    {
        [Xml.Attribute] public string Id { get; internal set; }
        [Xml.Attribute] public uint? Bandwidth { get; internal set; }
        [Xml.Attribute] public uint? QualityRanking { get; internal set; }
        [Xml.Attribute] public string[] DependencyId { get; internal set; }
        [Xml.Attribute] public uint? NumChannels { get; internal set; }
        [Xml.Attribute] public ulong? SampleRate { get; internal set; }
        [Xml.Element] public BaseURL[] BaseURLs { get; internal set; }
        [Xml.Element] public SegmentBase[] SegmentBases { get; internal set; }
        [Xml.Element] public SegmentList[] SegmentLists { get; internal set; }
        [Xml.Element] public SegmentTemplate[] SegmentTemplates { get; internal set; }
        [Xml.Element] public SubRepresentation[] SubRepresentations { get; internal set; }

        public string BaseURL
        {
            get
            {
                if (BaseURLs == null)
                    return null;
                return (from url in BaseURLs
                    where !string.IsNullOrEmpty(url.BaseUrlValue)
                    select url.BaseUrlValue).FirstOrDefault();
            }
        }

        public AdaptationSet AdaptationSet { get; internal set; }
        public Period Period => AdaptationSet.Period;
        public Dash Document => AdaptationSet.Document;

        public Representation(AdaptationSet set)
        {
            AdaptationSet = set;
        }

        private static TS GetFirst<TS>(TS[] list)
        {
            if ((list?.Length ?? 0) == 0)
                return default(TS);
            return list != null ? list[0] : default(TS);
        }
        private static T GetDynamic<T, TS>(TS[] repr, TS[] set, TS[] period)
        {
            return (T)Activator.CreateInstance(
                typeof(T),
                GetFirst(repr),
                GetFirst(set),
                GetFirst(period));
        }

        public Dynamic.SegmentBase SegmentBase()
        {
            return GetDynamic<Dynamic.SegmentBase, SegmentBase>(
                SegmentBases,
                AdaptationSet.SegmentBases,
                Period.SegmentBases);
        }

        public Dynamic.SegmentTemplate SegmentTemplate()
        {
            return GetDynamic<Dynamic.SegmentTemplate, SegmentTemplate>(
                SegmentTemplates,
                AdaptationSet.SegmentTemplates,
                Period.SegmentTemplates);
        }

        public Dynamic.SegmentList SegmentList()
        {
            return GetDynamic<Dynamic.SegmentList, SegmentList>(
                SegmentLists,
                AdaptationSet.SegmentLists,
                Period.SegmentLists);
        }

        public IRepresentationStream SegmentsStream()
        {
            var best = FindBestStream();
            switch (best)
            {
                case SegmentType.Base: return CreateBaseRepresentationStream();
                case SegmentType.List: return CreateListRepresentationStream();
                case SegmentType.Template: return CreateTemplateRepresentationStream();
                case SegmentType.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            // No "SegmentXyz" elements, but at least one BaseURL present
            // This setup could be in e.g. subtitles
            return BaseURLs.Length > 0 ? CreateBaseUrlRepresentationStream() : null;
        }

        private IRepresentationStream CreateBaseRepresentationStream()
        {
            TimeRange periodRange = null;
            if (Period.Start != null && Period.Duration != null)
                periodRange = new TimeRange(Period.Start.Value, Period.Duration.Value);

            var seg = SegmentBase();
            var initUrl = GetFirst(seg.Initializations);

            MpdUri mediaMpdUri = CalcUrl();
            MpdUri initMpdUri = null;

            // If the init_url.SourceUrl is present,
            // it is relative to AdpativeSet, not Representation.
            // Representation's BaseURI is for media only.
            if (initUrl?.SourceURL != null)
                initMpdUri = AdaptationSet.CalcUrl()?.With(initUrl.SourceURL);
            else if (initUrl != null)
                initMpdUri = mediaMpdUri;

            if (initMpdUri == null)
            {
                if (mediaMpdUri == null) return null;

                return new BaseRepresentationStream(
                    null,
                    new Segment(mediaMpdUri.Uri, null, periodRange));
            }

            if (mediaMpdUri == null) return null;

            return new BaseRepresentationStream(
                new Segment(initMpdUri.Uri, initUrl.Range),
                new Segment(mediaMpdUri.Uri, null, periodRange));
        }

        private IRepresentationStream CreateBaseUrlRepresentationStream()
        {
            TimeRange periodRange = null;
            if (Period.Start != null && Period.Duration != null)
                periodRange = new TimeRange(Period.Start.Value, Period.Duration.Value);

            var mediaMpdUri = AdaptationSet.CalcUrl();
            mediaMpdUri = BaseURLs.Where(
                url => !string.IsNullOrEmpty(url.BaseUrlValue)).Aggregate(
                mediaMpdUri,
                (current, url) => current.With(url.BaseUrlValue));

            return new BaseRepresentationStream(
                null,
                new Segment(mediaMpdUri.Uri, null, periodRange)
            );
        }

        private IRepresentationStream CreateListRepresentationStream()
        {
            var seg = SegmentList();
            var initUrl = GetFirst(seg.Initializations) ?? GetFirst(SegmentBase().Initializations);
            MpdUri initMpdUri = null;
            if (initUrl?.SourceURL != null)
                initMpdUri = AdaptationSet.CalcUrl()?.With(initUrl.SourceURL);

            var init = initMpdUri == null ?
                null : new Segment(initMpdUri.Uri, initUrl.Range);

            var items = ListItem.FromXml(
                seg.StartNumber ?? 1,
                Period.Start ?? TimeSpan.Zero,
                seg.Timescale ?? 1,
                seg.Duration ?? 0,
                seg.SegmentUrls);
            return new ListRepresentationStream(
                CalcUrl().Uri,
                init,
                seg.Timescale ?? 1,
                items);
        }

        private TimelineItem[] FromDuration(uint startNumber, Dynamic.SegmentTemplate seg)
        {
            var segDuration = seg.Duration;
            if (segDuration == null)
                return null;

            var start = Period.Start ?? TimeSpan.Zero;

            var duration = Period.Duration;
            if (duration == null)
            {
                var mpdDuration = Document.MediaPresentationDuration ?? TimeSpan.Zero;
                if (mpdDuration <= start)
                    duration = TimeSpan.Zero;
                else
                    duration = mpdDuration - start;
            }

            if (duration.Equals(TimeSpan.Zero))
                return null;

            return Timeline.FromDuration(startNumber, start,
                duration.Value, segDuration.Value, seg.Timescale ?? 1);
        }

        private TimelineItem[] FromTimeline(
            uint startNumber,
            Dynamic.SegmentTemplate seg,
            SegmentTimeline segmentTimeline)
        {
            return Timeline.FromXml(
                startNumber,
                Period.Start ?? TimeSpan.Zero,
                Period.End,
                seg.Timescale ?? 1,
                segmentTimeline.Ss);
        }

        private IRepresentationStream CreateTemplateRepresentationStream()
        {
            var seg = SegmentTemplate();
            var segTimeline = GetFirst(seg.SegmentTimelines);
            var startNumber = seg.StartNumber ?? 1;

            var timeline = segTimeline == null ?
                FromDuration(startNumber, seg) :
                FromTimeline(startNumber, seg, segTimeline);

            if (timeline == null)
                return null;

            return new TemplateRepresentationStream(
                CalcUrl().Uri,
                seg.Initialization,
                seg.Media,
                Bandwidth,
                Id,
                seg.Timescale ?? 1,
                timeline);
        }

        public MpdUri CalcUrl()
        {
            var parent = AdaptationSet.CalcUrl();
            var local = MpdUri.FromBaseUrls(BaseURLs);

            return local == null ? parent : parent.With(local);
        }

        private SegmentType FindBestStream()
        {
            // Priority for the search is:
            // First, how specific segment description is;
            // we look at Representation first, then at AdaptationSet,
            // then at Period, stopping at first with any segment
            // description.
            // Second, we look at the segments in order of libdash,
            // that is SegmentTemplate first, then SegmentList and
            // finally SegmentBase (see AdaptationSetStream::DetermineRepresentationStreamType)

            // REPRESENTATION:
            if (SegmentTemplates.Length > 0)
                return SegmentType.Template;
            if (SegmentLists.Length > 0)
                return SegmentType.List;
            if (SegmentBases.Length > 0)
                return SegmentType.Base;

            // ADAPTATION SET:
            if (AdaptationSet.SegmentTemplates.Length > 0)
                return SegmentType.Template;
            if (AdaptationSet.SegmentLists.Length > 0)
                return SegmentType.List;
            if (AdaptationSet.SegmentBases.Length > 0)
                return SegmentType.Base;

            // PERIOD:
            if (Period.SegmentTemplates.Length > 0)
                return SegmentType.Template;
            if (Period.SegmentLists.Length > 0)
                return SegmentType.List;
            if (Period.SegmentBases.Length > 0)
                return SegmentType.Base;

            return SegmentType.None;
        }
    }

    public class AdaptationSet : RepresentationBase
    {
        private T InComponents<T>(Func<ContentComponent, T> pred)
        {
            if (ContentComponents == null)
                return default(T);
            foreach (var comp in ContentComponents)
            {
                T val = pred(comp);
                if (val != null)
                    return val;
            }
            return default(T);
        }

        public uint? GetId()
        {
            return Id ?? InComponents(comp => comp.Id);
        }

        public string GetLang()
        {
            return Lang ?? InComponents(comp => comp.Lang);
        }

        public string GetContentType()
        {
            return ContentType ?? InComponents(comp => comp.ContentType);
        }

        public MpdUri CalcUrl()
        {
            var parent = Period.CalcUrl();
            var local = MpdUri.FromBaseUrls(BaseURLs);

            return local == null ? parent : parent.With(local);
        }

        [Xml.Attribute] public uint? Id { get; internal set; }
        [Xml.Attribute] public uint? Group { get; internal set; }
        [Xml.Attribute] public string Lang { get; internal set; }
        [Xml.Attribute] public string ContentType { get; internal set; }
        [Xml.Attribute] public string Par { get; internal set; }
        [Xml.Attribute] public uint? MinBandwidth { get; internal set; }
        [Xml.Attribute] public uint? MaxBandwidth { get; internal set; }
        [Xml.Attribute] public uint? MinWidth { get; internal set; }
        [Xml.Attribute] public uint? MaxWidth { get; internal set; }
        [Xml.Attribute] public uint? MinHeight { get; internal set; }
        [Xml.Attribute] public uint? MaxHeight { get; internal set; }
        [Xml.Attribute] public string MinFrameRate { get; internal set; }
        [Xml.Attribute] public string MaxFrameRate { get; internal set; }
        [Xml.Attribute] public string SegmentAlignment { get; internal set; }
        [Xml.Attribute] public string SubsegmentAlignment { get; internal set; }
        [Xml.Attribute] public int? SubsegmentStartsWithSAP { get; internal set; }
        [Xml.Attribute] public bool BitstreamSwitching { get; internal set; }
        [Xml.Element] public Descriptor[] Accessibilities { get; internal set; }
        [Xml.Element] public Descriptor[] Roles { get; internal set; }
        [Xml.Element] public Descriptor[] Ratings { get; internal set; }
        [Xml.Element] public Descriptor[] Viewpoints { get; internal set; }
        [Xml.Element] public ContentComponent[] ContentComponents { get; internal set; }
        [Xml.Element] public BaseURL[] BaseURLs { get; internal set; }
        [Xml.Element] public SegmentBase[] SegmentBases { get; internal set; }
        [Xml.Element] public SegmentList[] SegmentLists { get; internal set; }
        [Xml.Element] public SegmentTemplate[] SegmentTemplates { get; internal set; }
        [Xml.Element] public Representation[] Representations { get; internal set; }

        public string BaseURL => BaseURLs == null ?
            null :
            (from url in BaseURLs
                where !string.IsNullOrEmpty(url.BaseUrlValue)
                select url.BaseUrlValue).FirstOrDefault();

        public Period Period { get; }
        public Dash Document => Period.Document;

        public static string UrnRole2011 = "urn:mpeg:dash:role:2011";
        public static string UrnRole = "urn:mpeg:dash:role";

        public AdaptationSet( Period period)
        {
            Period = period;
        }
    }

    public class Period
    {
        internal TimeSpan? End { get; set; }
        public MpdUri CalcUrl()
        {
            var parent = Document.CalcUrl();
            var local = MpdUri.FromBaseUrls(BaseURLs);

            return local == null ? parent : parent.With(local);
        }

        [Xml.Attribute] public string Id { get; internal set; }
        [Xml.Attribute] public TimeSpan? Start { get; internal set; }
        [Xml.Attribute] public TimeSpan? Duration { get; internal set; }
        [Xml.Attribute] public bool BitstreamSwitching { get; internal set; }
        [Xml.Element] public BaseURL[] BaseURLs { get; internal set; }
        [Xml.Element] public SegmentBase[] SegmentBases { get; internal set; }
        [Xml.Element] public SegmentList[] SegmentLists { get; internal set; }
        [Xml.Element] public SegmentTemplate[] SegmentTemplates { get; internal set; }
        [Xml.Element] public Descriptor[] AssetIdentifiers { get; internal set; }
        [Xml.Element] public EventStream[] EventStreams { get; internal set; }
        [Xml.Element] public AdaptationSet[] AdaptationSets { get; internal set; }
        [Xml.Element] public Subset[] Subsets { get; internal set; }

        public Dash Document { get; }

        public Period(Dash doc)
        {
            Document = doc;
        }
    }

    public class Dash
    {
        private readonly MpdUri _manifestUrl;

        public Dash(string manifestUrl)
        {
            _manifestUrl = string.IsNullOrEmpty(manifestUrl) ?
                null : new MpdUri(manifestUrl);
        }

        public MpdUri CalcUrl()
        {
            var local = MpdUri.FromBaseUrls(BaseURLs);
            return _manifestUrl?.With(local) ?? local ?? new MpdUri();
        }

        public void PeriodFixup()
        {
            for (var i = 0; i < Periods.Length; ++i)
            {
                var curr = Periods[i];
                if (curr.Start == null)
                {
                    curr.Start = i == 0 ? TimeSpan.Zero : Periods[i - 1].End;
                }

                if (curr.End != null) continue;
                // for the last period, if @mediaPresentationDuration{
                // is present, it takes precedence
                if (i == Periods.Length - 1)
                {
                    if (MediaPresentationDuration != null)
                        curr.End = MediaPresentationDuration;
                }
                else
                {
                    // for any other period, the end time is
                    // the start time of the next period
                    curr.End = Periods[i + 1].Start;
                }

                // fallback to @start + @duration, if present
                if (curr.End == null &&
                    curr.Start != null &&
                    curr.Duration != null)
                {
                    curr.End = curr.Start.Value + curr.Duration.Value;
                    
                }
            }
            foreach (var currentPeriod in Periods)
            {
                if (currentPeriod.Start == null) continue;

                if (currentPeriod.End != null)
                    currentPeriod.Duration =
                        currentPeriod.End.Value - currentPeriod.Start.Value;

                if (currentPeriod.Duration == null)
                    currentPeriod.Duration =
                        GuessFromRepresentations(currentPeriod);
            }
        }

        private static TimeSpan? GuessFromRepresentations(Period curr)
        {
            TimeSpan? result = null;
            foreach (var set in curr.AdaptationSets)
            {
                var longest = GuessFromRepresentations(set);
                if (result == null)
                    result = longest;
                else if (longest != null && result.Value < longest.Value)
                    result = longest.Value;
            }
            return result;
        }

        private static TimeSpan? GuessFromRepresentations(AdaptationSet set)
        {
            TimeSpan? result = null;
            foreach (var repr in set.Representations)
            {
                var longest = GuessFromRepresentations(repr);
                if (result == null)
                    result = longest;
                else if (longest != null && result.Value < longest.Value)
                    result = longest.Value;
            }
            return result;
        }

        private static TimeSpan? GuessFromRepresentations(Representation repr)
        {
            return repr.SegmentsStream()?.Duration;
        }

        [Xml.Attribute] public string Xmlns { get; internal set; }
        [Xml.Attribute] public string Id { get; internal set; }
        [Xml.Attribute] public string Type { get; internal set; }
        [Xml.Attribute] public string Profiles { get; internal set; }
        [Xml.Attribute] public DateTime? AvailabilityStartTime { get; internal set; }
        [Xml.Attribute] public DateTime? AvailabilityEndTime { get; internal set; }
        [Xml.Attribute] public DateTime? PublishTime { get; internal set; }
        [Xml.Attribute] public TimeSpan? MediaPresentationDuration { get; internal set; }
        [Xml.Attribute] public TimeSpan? MinimumUpdatePeriod { get; internal set; }
        [Xml.Attribute] public TimeSpan? MinBufferTime { get; internal set; }
        [Xml.Attribute] public TimeSpan? TimeShiftBufferDepth { get; internal set; }
        [Xml.Attribute] public TimeSpan? SuggestedPresentationDelay { get; internal set; }
        [Xml.Attribute] public TimeSpan? MaxSegmentDuration { get; internal set; }
        [Xml.Attribute] public TimeSpan? MaxSubsegmentDuration { get; internal set; }
        [Xml.Element] public ProgramInformation[] ProgramInformations { get; internal set; }
        [Xml.Element] public BaseURL[] BaseURLs { get; internal set; }
        [Xml.Element] public string[] Locations { get; internal set; }
        [Xml.Element] public Period[] Periods { get; internal set; }
        [Xml.Element] public Metrics[] Metrics { get; internal set; }
    }

    public class Internal
    {
        public static void SetValue<T>(PropertyInfo prop, T result, object value)
        {
            prop.SetValue(result, value);
        }
    }
    public class Subset
    {
        [Xml.Attribute] public string Id { get; internal set; }
        [Xml.Attribute] public int[] Contains { get; internal set; }
    }

    public class URL
    {
        [Xml.Attribute] public string SourceURL { get; internal set; }
        [Xml.Attribute] public string Range { get; internal set; }
    }

    public class BaseURL
    {
        [Xml.InnerText] public string BaseUrlValue { get; internal set; }
        [Xml.Attribute] public string ServiceLocation { get; internal set; }
        [Xml.Attribute] public string ByteRange { get; internal set; }
        [Xml.Attribute] public double? AvailabilityTimeOffset { get; internal set; }
        [Xml.Attribute] public bool AvailabilityTimeComplete { get; internal set; }
    }

    public class ProgramInformation
    {
        [Xml.Attribute] public string Lang { get; internal set; }
        [Xml.Attribute] public string MoreInformationURL { get; internal set; }

        [Xml.Element] public string[] Titles { get; internal set; }
        [Xml.Element] public string[] Sources { get; internal set; }
        [Xml.Element] public string[] Copyrights { get; internal set; }
    }

    public class Range
    {
        [Xml.Attribute] public TimeSpan? Starttime { get; internal set; }
        [Xml.Attribute] public TimeSpan? Duration { get; internal set; }
    }

    public class SegmentURL
    {
        [Xml.Attribute] public string Media { get; internal set; }
        [Xml.Attribute] public string MediaRange { get; internal set; }
        [Xml.Attribute] public string Index { get; internal set; }
        [Xml.Attribute] public string IndexRange { get; internal set; }
    }

    public class S
    {
        [Xml.Attribute] public ulong? T { get; internal set; }
        [Xml.Attribute] public ulong? D { get; internal set; }
        [Xml.Attribute] public int? R { get; internal set; }
    }

    public class SegmentTimeline
    {
        [Xml.Element] public S[] Ss { get; internal set; }
    }

    public class SegmentBase
    {
        [Xml.Attribute] public uint? Timescale { get; internal set; }
        [Xml.Attribute] public string IndexRange { get; internal set; }
        [Xml.Attribute] public bool IndexRangeExact { get; internal set; }
        [Xml.Attribute] public ulong? PresentationTimeOffset { get; internal set; }
        [Xml.Attribute] public double? AvailabilityTimeOffset { get; internal set; }
        [Xml.Attribute] public bool AvailabilityTimeComplete { get; internal set; }
        [Xml.Element] public URL[] Initializations { get; internal set; }
        [Xml.Element] public URL[] RepresentationIndexes { get; internal set; }
    }

    public class MultipleSegmentBase : SegmentBase
    {
        [Xml.Attribute] public uint? Duration { get; internal set; }
        [Xml.Attribute] public uint? StartNumber { get; internal set; }
        [Xml.Element] public SegmentTimeline[] SegmentTimelines { get; internal set; }
        [Xml.Element] public URL[] BitstreamSwitchings { get; internal set; }
    }

    public class UnexpectedTemplateArgumentException : Exception
    {
        public UnexpectedTemplateArgumentException(string msg) : base(msg) { }
    }

    public class SegmentTemplate : MultipleSegmentBase
    {
        [Xml.Attribute] public Template Media { get; internal set; }
        [Xml.Attribute] public Template Index { get; internal set; }
        [Xml.Attribute] public Template Initialization { get; internal set; }
        [Xml.Attribute] public string BitstreamSwitching { get; internal set; }
    }

    public class SegmentList : MultipleSegmentBase
    {
        [Xml.Element] public SegmentURL[] SegmentURLs { get; internal set; }
    }

    public class Event
    {
        [Xml.InnerText] public string EventValue { get; internal set; }
        [Xml.Attribute] public ulong? PresentationTime { get; internal set; }
        [Xml.Attribute] public ulong? Duration { get; internal set; }
        [Xml.Attribute] public uint? Id { get; internal set; }
    }

    public class Descriptor
    {
        [Xml.Attribute] public string SchemeIdUri { get; internal set; }
        [Xml.Attribute] public string Value { get; internal set; }
        [Xml.Attribute] public string Id { get; internal set; }
    }

    public class Metrics
    {
        [Xml.Attribute("metrics")] public string MetricsAttr { get; internal set; }
        [Xml.Element] public Descriptor[] Reportings { get; internal set; }
        [Xml.Element] public Range[] Ranges { get; internal set; }
    }

    public class ContentComponent
    {
        [Xml.Attribute] public uint? Id { get; internal set; }
        [Xml.Attribute] public string Lang { get; internal set; }
        [Xml.Attribute] public string ContentType { get; internal set; }
        [Xml.Attribute] public string Par { get; internal set; }
        [Xml.Element] public Descriptor[] Accessibilities { get; internal set; }
        [Xml.Element] public Descriptor[] Roles { get; internal set; }
        [Xml.Element] public Descriptor[] Ratings { get; internal set; }
        [Xml.Element] public Descriptor[] Viewpoints { get; internal set; }
    }

    public class RepresentationBase
    {
        [Xml.Attribute] public string Profile { get; internal set; }
        [Xml.Attribute] public uint? Width { get; internal set; }
        [Xml.Attribute] public uint? Height { get; internal set; }
        [Xml.Attribute] public string Sar { get; internal set; }
        [Xml.Attribute] public string FrameRate { get; internal set; }
        [Xml.Attribute] public string AudioSamplingRate { get; internal set; }
        [Xml.Attribute] public string MimeType { get; internal set; }
        [Xml.Attribute] public string SegmentProfiles { get; internal set; }
        [Xml.Attribute] public string Codecs { get; internal set; }
        [Xml.Attribute] public double? MaximumSAPPeriod { get; internal set; }
        [Xml.Attribute] public int? StartWithSAP { get; internal set; }
        [Xml.Attribute] public double? MaxPlayoutRate { get; internal set; }
        [Xml.Attribute] public bool CodingDependency { get; internal set; }
        [Xml.Attribute] public string ScanType { get; internal set; }
        [Xml.Element] public Descriptor[] FramePackings { get; internal set; }
        [Xml.Element] public Descriptor[] AudioChannelConfigurations { get; internal set; }
        [Xml.Element] public Descriptor[] ContentProtections { get; internal set; }
        [Xml.Element] public Descriptor[] EssentialProperties { get; internal set; }
        [Xml.Element] public Descriptor[] SupplementalProperties { get; internal set; }
        [Xml.Element] public Descriptor[] InbandEventStreams { get; internal set; }
    }

    public interface IRepresentationStream
    {
        TimeSpan? Duration { get; }
        Segment InitSegment { get; }
        uint Count { get; }
        IEnumerable<Segment> MediaSegments();
        Segment MediaSegmentAtPos(uint pos);
        uint? MediaSegmentAtTime(TimeSpan duration);
    }

    public enum SegmentType
    {
        None,
        Base,
        List,
        Template
    };

    public class SubRepresentation : RepresentationBase
    {
        [Xml.Attribute] public uint? Level { get; internal set; }
        [Xml.Attribute] public uint? Bandwidth { get; internal set; }
        [Xml.Attribute] public uint[] DependencyLevel { get; internal set; }
        [Xml.Attribute] public string[] ContentComponent { get; internal set; }
    }

    public class EventStream
    {
        [Xml.Attribute] public string SchemeIdUri { get; internal set; }
        [Xml.Attribute] public string Value { get; internal set; }
        [Xml.Attribute] public uint? Timescale { get; internal set; }
        [Xml.Element] public Event[] Events { get; internal set; }
    }
}
