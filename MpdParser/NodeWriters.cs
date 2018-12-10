/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;

/// <summary>
///
/// MpdParser.Node.Writer contains "Write Accessors" to MpdParser.Node namespace classes.
/// Classes included in MpdParser.Node namespace have properties defined with internal set.
/// By inheriting from those classes, dervied class have access to internal set properties
/// (as by inheritance they become part of derived class). New setters do need to be defined though
/// for access from outside of the derived class. Due to MpdParser.Node classes implementation,
/// they cannot be overriden.
///
/// </summary>

namespace MpdParser.Node.Writers
{
    public class SubsetWriter : Subset
    {
        [Xml.Attribute] public new string Id { get => base.Id; set => base.Id = value; }
        [Xml.Attribute] public new int[] Contains { get => base.Contains; set => base.Contains = value; }
    }
    public class URLWriter : URL
    {
        [Xml.Attribute] public new string SourceURL { get => base.SourceURL; set => base.SourceURL = value; }
        [Xml.Attribute] public new string Range { get => base.Range; set => base.Range = value; }
    }
    public class BaseURLWriter : BaseURL
    {
        [Xml.InnerText] public new string BaseUrlValue { get => base.BaseUrlValue; set => base.BaseUrlValue = value; }
        [Xml.Attribute] public new string ServiceLocation { get => base.ServiceLocation; set => base.ServiceLocation = value; }
        [Xml.Attribute] public new string ByteRange { get => base.ByteRange; set => base.ByteRange = value; }
        [Xml.Attribute] public new double? AvailabilityTimeOffset { get => base.AvailabilityTimeOffset; set => base.AvailabilityTimeOffset = value; }
        [Xml.Attribute] public new bool AvailabilityTimeComplete { get => base.AvailabilityTimeComplete; set => base.AvailabilityTimeComplete = value; }
    }
    public class ProgramInformationWriter : ProgramInformation
    {
        [Xml.Attribute] public new string Lang { get => base.Lang; set => base.Lang = value; }
        [Xml.Attribute] public new string MoreInformationURL { get => base.MoreInformationURL; set => base.MoreInformationURL = value; }

        [Xml.Element] public new string[] Titles { get => base.Titles; set => base.Titles = value; }
        [Xml.Element] public new string[] Sources { get => base.Sources; set => base.Sources = value; }
        [Xml.Element] public new string[] Copyrights { get => base.Copyrights; set => base.Copyrights = value; }
    }
    public class RangeWriter : Range
    {
        [Xml.Attribute] public new TimeSpan? StartTime { get => base.StartTime; set => base.StartTime = value; }
        [Xml.Attribute] public new TimeSpan? Duration { get => base.Duration; set => base.Duration = value; }
    }
    public class SegmentURLWriter : SegmentURL
    {
        [Xml.Attribute] public new string Media { get => base.Media; set => base.Media = value; }
        [Xml.Attribute] public new string MediaRange { get => base.MediaRange; set => base.MediaRange = value; }
        [Xml.Attribute] public new string Index { get => base.Index; set => base.Index = value; }
        [Xml.Attribute] public new string IndexRange { get => base.IndexRange; set => base.IndexRange = value; }
    }
    public class SWriter : S
    {
        [Xml.Attribute] public new ulong? T { get => base.T; set => base.T = value; }
        [Xml.Attribute] public new ulong? D { get => base.D; set => base.D = value; }
        [Xml.Attribute] public new int? R { get => base.R; set => base.R = value; }
    }
    public class SegmentTimelineWriter : SegmentTimeline
    {
        [Xml.Element] public new S[] Ss { get => base.Ss; set => base.Ss = value; }
    }
    public class SegmentBaseWriter : SegmentBase
    {
        [Xml.Attribute] public new uint? Timescale { get => base.Timescale; set => base.Timescale = value; }
        [Xml.Attribute] public new string IndexRange { get => base.IndexRange; set => base.IndexRange = value; }
        [Xml.Attribute] public new bool IndexRangeExact { get => base.IndexRangeExact; set => base.IndexRangeExact = value; }
        [Xml.Attribute] public new ulong? PresentationTimeOffset { get => base.PresentationTimeOffset; set => base.PresentationTimeOffset = value; }
        [Xml.Attribute] public new double? AvailabilityTimeOffset { get => base.AvailabilityTimeOffset; set => base.AvailabilityTimeOffset = value; }
        [Xml.Attribute] public new bool? AvailabilityTimeComplete { get => base.AvailabilityTimeComplete; set => base.AvailabilityTimeComplete = value; }
        [Xml.Attribute] public new TimeSpan? TimeShiftBufferDepth { get => base.TimeShiftBufferDepth; set => base.TimeShiftBufferDepth = value; }
        [Xml.Element] public new URL[] Initializations { get => base.Initializations; set => base.Initializations = value; }
        [Xml.Element] public new URL[] RepresentationIndexes { get => base.RepresentationIndexes; set => base.RepresentationIndexes = value; }
    }
    public class MultipleSegmentBaseWriter : MultipleSegmentBase
    {
        [Xml.Attribute] public new uint? Duration { get => base.Duration; set => base.Duration = value; }
        [Xml.Attribute] public new uint? StartNumber { get => base.StartNumber; set => base.StartNumber = value; }
        [Xml.Element] public new SegmentTimeline[] SegmentTimelines { get => base.SegmentTimelines; set => base.SegmentTimelines = value; }
        [Xml.Element] public new  URL[] BitstreamSwitchings { get => base.BitstreamSwitchings; set => base.BitstreamSwitchings = value; }
    }
    public class SegmentTemplateWriter : SegmentTemplate
    {
        [Xml.Attribute] public new Template Media { get => base.Media; set => base.Media = value; }
        [Xml.Attribute] public new Template Index { get => base.Index; set => base.Index = value; }
        [Xml.Attribute] public new Template Initialization { get => base.Initialization; set => base.Initialization = value; }
        [Xml.Attribute] public new string BitstreamSwitching { get => base.BitstreamSwitching; set => base.BitstreamSwitching = value; }
    }
    public class SegmentListWriter : SegmentList
    {
        [Xml.Element] public new SegmentURL[] SegmentURLs { get => base.SegmentURLs; set => base.SegmentURLs = value; }
    }
    public class EventWriter : Event
    {
        [Xml.InnerText] public new string EventValue { get => base.EventValue; set => base.EventValue = value; }
        [Xml.Attribute] public new ulong? PresentationTime { get => base.PresentationTime; set => base.PresentationTime = value; }
        [Xml.Attribute] public new ulong? Duration { get => base.Duration; set => base.Duration = value; }
        [Xml.Attribute] public new uint? Id { get => base.Id; set => base.Id = value; }
    }
    public class DescriptorWriter : Descriptor
    {
        [Xml.Attribute] public new string SchemeIdUri { get => base.SchemeIdUri; set => base.SchemeIdUri = value; }
        [Xml.Attribute] public new string Value { get => base.Value; set => base.Value = value; }
        [Xml.Attribute] public new string Id { get => base.Id; set => base.Id = value; }
    }
    public class MetricsWriter : Metrics
    {
        [Xml.Attribute("metrics")] public new string MetricsAttr { get => base.MetricsAttr; set => base.MetricsAttr = value; }
        [Xml.Element] public new Descriptor[] Reportings { get => base.Reportings; set => base.Reportings = value; }
        [Xml.Element] public new Range[] Ranges { get => base.Ranges; set => base.Ranges = value; }
    }
    public class ContentComponentWriter : ContentComponent
    {
        [Xml.Attribute] public new uint? Id { get => base.Id; set => base.Id = value; }
        [Xml.Attribute] public new string Lang { get => base.Lang; set => base.Lang = value; }
        [Xml.Attribute] public new string ContentType { get => base.ContentType; set => base.ContentType = value; }
        [Xml.Attribute] public new string Par { get => base.Par; set => base.Par = value; }
        [Xml.Element] public new Descriptor[] Accessibilities { get => base.Accessibilities; set => base.Accessibilities = value; }
        [Xml.Element] public new Descriptor[] Roles { get => base.Roles; set => base.Roles = value; }
        [Xml.Element] public new Descriptor[] Ratings { get => base.Ratings; set => base.Ratings = value; }
        [Xml.Element] public new Descriptor[] Viewpoints { get => base.Viewpoints; set => base.Viewpoints = value; }
    }
    public class ContentProtectionWriter : ContentProtection
    {
        [Xml.Attribute("cenc:default_KID")] public new string CencDefaultKID { get => base.CencDefaultKID; set => base.CencDefaultKID = value; }
        [Xml.Attribute] public new string SchemeIdUri { get => base.SchemeIdUri; set => base.SchemeIdUri = value; }
        [Xml.Attribute] public new string Value { get => base.Value; set => base.Value = value; }
        [Xml.InnerXml] public new string Data { get => base.Data; set => base.Data = value; }
    }
    public class RepresentationBaseWriter : RepresentationBase
    {
        [Xml.Attribute] public new string Profiles { get => base.Profiles; set => base.Profiles = value; }
        [Xml.Attribute] public new uint? Width { get => base.Width; set => base.Width = value; }
        [Xml.Attribute] public new uint? Height { get => base.Height; set => base.Height = value; }
        [Xml.Attribute] public new string Sar { get => base.Sar; set => base.Sar = value; }
        [Xml.Attribute] public new string FrameRate { get => base.FrameRate; set => base.FrameRate = value; }
        [Xml.Attribute] public new string AudioSamplingRate { get => base.AudioSamplingRate; set => base.AudioSamplingRate = value; }
        [Xml.Attribute] public new string MimeType { get => base.MimeType; set => base.MimeType = value; }
        [Xml.Attribute] public new string SegmentProfiles { get => base.SegmentProfiles; set => base.SegmentProfiles = value; }
        [Xml.Attribute] public new string Codecs { get => base.Codecs; set => base.Codecs = value; }
        [Xml.Attribute] public new double? MaximumSAPPeriod { get => base.MaximumSAPPeriod; set => base.MaximumSAPPeriod = value; }
        [Xml.Attribute] public new uint? StartWithSAP { get => base.StartWithSAP; set => base.StartWithSAP = value; }
        [Xml.Attribute] public new double? MaxPlayoutRate { get => base.MaxPlayoutRate; set => base.MaxPlayoutRate = value; }
        [Xml.Attribute] public new bool CodingDependency { get => base.CodingDependency; set => base.CodingDependency = value; }
        [Xml.Attribute] public new string ScanType { get => base.ScanType; set => base.ScanType = value; }
        [Xml.Element] public new Descriptor[] FramePackings { get => base.FramePackings; set => base.FramePackings = value; }
        [Xml.Element] public new Descriptor[] AudioChannelConfigurations { get => base.AudioChannelConfigurations; set => base.AudioChannelConfigurations = value; }
        [Xml.Element] public new ContentProtection[] ContentProtections { get => base.ContentProtections; set => base.ContentProtections = value; }
        [Xml.Element] public new Descriptor[] EssentialProperties { get => base.EssentialProperties; set => base.EssentialProperties = value; }
        [Xml.Element] public new Descriptor[] SupplementalProperties { get => base.SupplementalProperties; set => base.SupplementalProperties = value; }
        [Xml.Element] public new Descriptor[] InbandEventStreams { get => base.InbandEventStreams; set => base.InbandEventStreams = value; }
    }
    public partial class RepresentationWriter : Representation
    {
        public RepresentationWriter(AdaptationSet set)
            : base(set) { }

        [Xml.Attribute] public new string Id { get => base.Id; set => base.Id = value; }
        [Xml.Attribute] public new uint? Bandwidth { get => base.Bandwidth; set => base.Bandwidth = value; }
        [Xml.Attribute] public new uint? QualityRanking { get => base.QualityRanking; set => base.QualityRanking = value; }
        [Xml.Attribute] public new string[] DependencyId { get => base.DependencyId; set => base.DependencyId = value; }
        [Xml.Attribute] public new uint? NumChannels { get => base.NumChannels; set => base.NumChannels = value; }
        [Xml.Attribute] public new ulong? SampleRate { get => base.SampleRate; set => base.SampleRate = value; }

        [Xml.Element] public new BaseURL[] BaseURLs { get => base.BaseURLs; set => base.BaseURLs = value; }
        [Xml.Element] public new SegmentBase[] SegmentBases { get => base.SegmentBases; set => base.SegmentBases = value; }
        [Xml.Element] public new SegmentList[] SegmentLists { get => base.SegmentLists; set => base.SegmentLists = value; }
        [Xml.Element] public new SegmentTemplate[] SegmentTemplates { get => base.SegmentTemplates; set => base.SegmentTemplates = value; }
        [Xml.Element] public new SubRepresentation[] SubRepresentations { get => base.SubRepresentations; set => base.SubRepresentations = value; }

        public new  AdaptationSet AdaptationSet { get => base.AdaptationSet; set => base.AdaptationSet = value; }

    }
    public class SubRepresentationWriter : SubRepresentation
    {
        [Xml.Attribute] public new uint? Level { get => base.Level; set => base.Level = value; }
        [Xml.Attribute] public new uint? Bandwidth { get => base.Bandwidth; set => base.Bandwidth = value; }
        [Xml.Attribute] public new uint[] DependencyLevel { get => base.DependencyLevel; set => base.DependencyLevel = value; }
        [Xml.Attribute] public new string[] ContentComponent { get => base.ContentComponent; set => base.ContentComponent = value; }
    }
    public partial class AdaptationSetWriter : AdaptationSet
    {
        public AdaptationSetWriter(Period period)
            : base(period) { }

        [Xml.Attribute] public new uint? Id { get => base.Id; set => base.Id = value; }
        [Xml.Attribute] public new uint? Group { get => base.Group; set => base.Group = value; }
        [Xml.Attribute] public new string Lang { get => base.Lang; set => base.Lang = value; }
        [Xml.Attribute] public new string ContentType { get => base.ContentType; set => base.ContentType = value; }
        [Xml.Attribute] public new string Par { get => base.Par; set => base.Par = value; }
        [Xml.Attribute] public new uint? MinBandwidth { get => base.MinBandwidth; set => base.MinBandwidth = value; }
        [Xml.Attribute] public new uint? MaxBandwidth { get => base.MaxBandwidth; set => base.MaxBandwidth = value; }
        [Xml.Attribute] public new uint? MinWidth { get => base.MinWidth; set => base.MinWidth = value; }
        [Xml.Attribute] public new uint? MaxWidth { get => base.MaxWidth; set => base.MaxWidth = value; }
        [Xml.Attribute] public new uint? MinHeight { get => base.MinHeight; set => base.MinHeight = value; }
        [Xml.Attribute] public new uint? MaxHeight { get => base.MaxHeight; set => base.MaxHeight = value; }
        [Xml.Attribute] public new string MinFrameRate { get => base.MinFrameRate; set => base.MinFrameRate = value; }
        [Xml.Attribute] public new string MaxFrameRate { get => base.MaxFrameRate; set => base.MaxFrameRate = value; }
        [Xml.Attribute] public new string SegmentAlignment { get => base.SegmentAlignment; set => base.SegmentAlignment = value; }
        [Xml.Attribute] public new string SubsegmentAlignment { get => base.SubsegmentAlignment; set => base.SubsegmentAlignment = value; }
        [Xml.Attribute] public new int? SubsegmentStartsWithSAP { get => base.SubsegmentStartsWithSAP; set => base.SubsegmentStartsWithSAP = value; }
        [Xml.Attribute] public new bool BitstreamSwitching { get => base.BitstreamSwitching; set => base.BitstreamSwitching = value; }

        [Xml.Element] public new Descriptor[] Accessibilities { get => base.Accessibilities; set => base.Accessibilities = value; }
        [Xml.Element] public new Descriptor[] Roles { get => base.Roles; set => base.Roles = value; }
        [Xml.Element] public new Descriptor[] Ratings { get => base.Ratings; set => base.Ratings = value; }
        [Xml.Element] public new Descriptor[] Viewpoints { get => base.Viewpoints; set => base.Viewpoints = value; }
        [Xml.Element] public new ContentComponent[] ContentComponents { get => base.ContentComponents; set => base.ContentComponents = value; }
        [Xml.Element] public new BaseURL[] BaseURLs { get => base.BaseURLs; set => base.BaseURLs = value; }
        [Xml.Element] public new SegmentBase[] SegmentBases { get => base.SegmentBases; set => base.SegmentBases = value; }
        [Xml.Element] public new SegmentList[] SegmentLists { get => base.SegmentLists; set => base.SegmentLists = value; }
        [Xml.Element] public new SegmentTemplate[] SegmentTemplates { get => base.SegmentTemplates; set => base.SegmentTemplates = value; }
        [Xml.Element] public new Representation[] Representations { get => base.Representations; set => base.Representations = value; }
    }

    public class EventStreamWriter : EventStream
    {
        [Xml.Attribute] public new string SchemeIdUri { get => base.SchemeIdUri; set => base.SchemeIdUri = value; }
        [Xml.Attribute] public new string Value { get => base.Value; set => base.Value = value; }
        [Xml.Attribute] public new uint? Timescale { get => base.Timescale; set => base.Timescale = value; }
        [Xml.Element] public new Event[] Events { get => base.Events; set => base.Events = value; }
    }
    public partial class PeriodWriter : Period
    {
        public PeriodWriter(Node.DASH doc)
            : base(doc) { }
        [Xml.Attribute] public new string Id { get => base.Id; set => base.Id = value; }
        [Xml.Attribute] public new TimeSpan? Start { get => base.Start; set => base.Start = value; }
        [Xml.Attribute] public new TimeSpan? Duration { get => base.Duration; set => base.Duration = value; }
        [Xml.Attribute] public new bool BitstreamSwitching { get => base.BitstreamSwitching; set => base.BitstreamSwitching = value; }

        [Xml.Element] public new BaseURL[] BaseURLs { get => base.BaseURLs; set => base.BaseURLs = value; }
        [Xml.Element] public new SegmentBase[] SegmentBases { get => base.SegmentBases; set => base.SegmentBases = value; }
        [Xml.Element] public new SegmentList[] SegmentLists { get => base.SegmentLists; set => base.SegmentLists = value; }
        [Xml.Element] public new SegmentTemplate[] SegmentTemplates { get => base.SegmentTemplates; set => base.SegmentTemplates = value; }
        [Xml.Element] public new Descriptor[] AssetIdentifiers { get => base.AssetIdentifiers; set => base.AssetIdentifiers = value; }
        [Xml.Element] public new EventStream[] EventStreams { get => base.EventStreams; set => base.EventStreams = value; }
        [Xml.Element] public new AdaptationSet[] AdaptationSets { get => base.AdaptationSets; set => base.AdaptationSets = value; }
        [Xml.Element] public new Subset[] Subsets { get => base.Subsets; set => base.Subsets = value; }
}

    public partial class DASHWriter : DASH
    {
        public DASHWriter(string manifestURL)
            : base(manifestURL) { }
        [Xml.Attribute] public new string Xmlns { get => base.Xmlns;  set => base.Xmlns = value; }
        [Xml.Attribute] public new string Id { get => base.Id; set => base.Id = value;  }
        [Xml.Attribute] public new string Type { get => base.Type;  set => base.Type = value;  }
        [Xml.Attribute] public new string Profiles { get => base.Profiles;  set => base.Profiles = value;  }
        [Xml.Attribute] public new DateTime? AvailabilityStartTime { get => base.AvailabilityStartTime; set => base.AvailabilityStartTime = value; }
        [Xml.Attribute] public new DateTime? AvailabilityEndTime { get => base.AvailabilityEndTime; set => base.AvailabilityEndTime = value; }
        [Xml.Attribute] public new DateTime? PublishTime { get => base.PublishTime; set => base.PublishTime = value; }
        [Xml.Attribute] public new TimeSpan? MediaPresentationDuration { get => base.MediaPresentationDuration; set => base.MediaPresentationDuration = value; }
        [Xml.Attribute] public new TimeSpan? MinimumUpdatePeriod { get => base.MinimumUpdatePeriod; set => base.MinimumUpdatePeriod = value; }
        [Xml.Attribute] public new TimeSpan? MinBufferTime { get => base.MinBufferTime; set => base.MinBufferTime = value; }
        [Xml.Attribute] public new TimeSpan? TimeShiftBufferDepth { get => base.TimeShiftBufferDepth; set => base.TimeShiftBufferDepth = value; }
        [Xml.Attribute] public new TimeSpan? SuggestedPresentationDelay { get => base.SuggestedPresentationDelay; set => base.SuggestedPresentationDelay = value; }
        [Xml.Attribute] public new TimeSpan? MaxSegmentDuration { get => base.MaxSegmentDuration; set => base.MaxSegmentDuration = value; }
        [Xml.Attribute] public new TimeSpan? MaxSubsegmentDuration { get => base.MaxSubsegmentDuration; set => base.MaxSubsegmentDuration = value; }
        [Xml.Element] public new ProgramInformation[] ProgramInformations { get => base.ProgramInformation;  set => base.ProgramInformation = value;  }
        [Xml.Element] public new BaseURL[] BaseURLs { get => base.BaseURLs; set => base.BaseURLs = value; }
        [Xml.Element] public new string[] Locations { get => base.Locations; set => base.Locations = value; }
        [Xml.Element] public new Period[] Periods { get => base.Periods; set => base.Periods = value; }
        [Xml.Element] public new Metrics[] Metrics { get => base.Metrics; set => base.Metrics = value; }
    }

}
