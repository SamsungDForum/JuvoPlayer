using System;
using System.Collections.Generic;
using MpdParser.Node.Dynamic;

namespace MpdParser.Node
{
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

    public partial class Formatter
    {
        public List<int> Positions;
        public string TrueKey;
        private int pad;
        private char fill;
    }

    public partial class Template
    {
        private string[] chunks_;
        private Dictionary<string, Formatter> keys_ = new Dictionary<string, Formatter>();
        public Template(string text)
        {
            chunks_ = text.Split('$');
            for (int i = 0; i < chunks_.Length; ++i)
            {
                if ((i % 2) == 0)
                    continue;
                string chunk = chunks_[i];
                if (!keys_.ContainsKey(chunk))
                    keys_.Add(chunk, new Formatter(chunk));
                keys_[chunk].Positions.Add(i);
            }
        }

        public override string ToString()
        {
            return Get(null, null);
        }
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

    public class ContentProtection
    {
        [Xml.Attribute("cenc:default_KID")] public string CencDefaultKID { get; internal set; }
        [Xml.Attribute] public string SchemeIdUri { get; internal set; }
        [Xml.Attribute] public string Value { get; internal set; }
        [Xml.InnerXml] public string Data { get; internal set; }
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
        [Xml.Element] public ContentProtection[] ContentProtections { get; internal set; }
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

    public partial class Representation : RepresentationBase
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
                foreach (BaseURL url in BaseURLs)
                {
                    if (string.IsNullOrEmpty(url.BaseUrlValue))
                        continue;
                    return url.BaseUrlValue;
                }
                return null;
            }
        }

        public AdaptationSet AdaptationSet { get; internal set; }
        public Period Period { get { return this.AdaptationSet.Period; } }
        public DASH Document { get { return this.AdaptationSet.Document; } }

        public Representation(AdaptationSet set)
        {
            AdaptationSet = set;
        }
    }

    public class SubRepresentation : RepresentationBase
    {
        [Xml.Attribute] public uint? Level { get; internal set; }
        [Xml.Attribute] public uint? Bandwidth { get; internal set; }
        [Xml.Attribute] public uint[] DependencyLevel { get; internal set; }
        [Xml.Attribute] public string[] ContentComponent { get; internal set; }
    }

    public partial class AdaptationSet : RepresentationBase
    {
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

        public string BaseURL
        {
            get
            {
                if (BaseURLs == null)
                    return null;
                foreach (BaseURL url in BaseURLs)
                {
                    if (string.IsNullOrEmpty(url.BaseUrlValue))
                        continue;
                    return url.BaseUrlValue;
                }
                return null;
            }
        }

        public Period Period { get; }
        public DASH Document { get { return this.Period.Document; } }

        public static string UrnRole2011 = "urn:mpeg:dash:role:2011";
        public static string UrnRole = "urn:mpeg:dash:role";

        public AdaptationSet(Period period)
        {
            Period = period;
        }
    }

    public class EventStream
    {
        [Xml.Attribute] public string SchemeIdUri { get; internal set; }
        [Xml.Attribute] public string Value { get; internal set; }
        [Xml.Attribute] public uint? Timescale { get; internal set; }
        [Xml.Element] public Event[] Events { get; internal set; }
    }

    public partial class Period
    {
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

        public DASH Document { get; }

        public Period(DASH doc)
        {
            Document = doc;
        }
    }

    public partial class DASH
    {
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
}
