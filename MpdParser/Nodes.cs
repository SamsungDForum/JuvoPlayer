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
using System.Collections.Generic;
using System.Threading;
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
        [Xml.Attribute] public TimeSpan? StartTime { get; internal set; }
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
        [Xml.Attribute] public bool? AvailabilityTimeComplete { get; internal set; }
        [Xml.Attribute] public TimeSpan? TimeShiftBufferDepth { get; internal set; }
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
        [Xml.Attribute] public string Profiles { get; internal set; }
        [Xml.Attribute] public uint? Width { get; internal set; }
        [Xml.Attribute] public uint? Height { get; internal set; }
        [Xml.Attribute] public string Sar { get; internal set; }
        [Xml.Attribute] public string FrameRate { get; internal set; }
        [Xml.Attribute] public string AudioSamplingRate { get; internal set; }
        [Xml.Attribute] public string MimeType { get; internal set; }
        [Xml.Attribute] public string SegmentProfiles { get; internal set; }
        [Xml.Attribute] public string Codecs { get; internal set; }
        [Xml.Attribute] public double? MaximumSAPPeriod { get; internal set; }
        [Xml.Attribute] public uint? StartWithSAP { get; internal set; }
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
        /// <summary>
        /// Duration of entire representation stream
        /// NULL if not present.
        /// </summary>
        TimeSpan? Duration { get; }

        /// <summary>
        /// Initialization segment.
        /// Null if init segment does not exist for representation stream.
        /// </summary>
        Segment InitSegment { get; }

        /// <summary>
        /// Number of playable media segments contained in stream. May be 0
        /// Dynamic streams - number reflects segments available for playback, not all
        /// segments in stream.
        /// </summary>
        uint Count { get; }

        /// <summary>
        /// Media Segment iterator.
        /// Static content - all media segments
        /// Dynamic content - segments available for playback.
        /// </summary>
        /// <returns>playable media segment</returns>
        IEnumerable<Segment> MediaSegments();

        /// <summary>
        /// Returns playable media segment based on provided segment identifier.
        /// </summary>
        /// <param name="segmentId">Internal segment identifier</param>
        /// <returns>Playable media segment. May be null if provided segment Id has no corresponding media</returns>
        Segment MediaSegment(uint? segmentId);

        /// <summary>
        /// Retrieves internal segment identifier for a given point in time of stream
        /// </summary>
        /// <param name="pointInTime">point in time for which segment id is to be obtained</param>
        /// <returns>Segment ID
        /// May be null if point in time is outside of playable time range for a stream.</returns>
        uint? SegmentId(TimeSpan pointInTime);

        /// <summary>
        /// Obtains segment identifier which is to be used as beginning of playback (Start Segment)
        /// </summary>
        /// <returns>Segment Identifier to be used as start point. May be null if start point has not
        /// been found</returns>
        uint? StartSegmentId();

        /// <summary>
        /// Sets manifest parameters to stream
        /// </summary>
        /// <param name="docParams">ManifestParameters to be set </param>
        void SetDocumentParameters(ManifestParameters docParams);

        /// <summary>
        /// Retrieves ManifestParameters set by SetDocumentParameters
        /// </summary>
        /// <returns>ManifestParameters</returns>
        ManifestParameters GetDocumentParameters();

        /// <summary>
        /// Obtains next playable segment identifier based on provided segment Id
        /// </summary>
        /// <param name="segmentId">Segment Id</param>
        /// <returns>Next Segment ID to be played. Null if no playable segment exist</returns>
        uint? NextSegmentId(uint? segmentId);

        /// <summary>
        /// Based on provided SegmentID, method returns previous segmentID
        /// </summary>
        /// <param name="segmentId"></param>
        /// <returns>Previous Segment ID</returns>
        uint? PreviousSegmentId(uint? segmentId);

        /// <summary>
        /// Obtains next playable segment identifier based on provided point in time
        /// </summary>
        /// <param name="pointInTime">A point in playback time</param>
        /// <returns>Next Segment ID to be played. Null if no playable segment exist</returns>
        uint? NextSegmentId(TimeSpan pointInTime);

        /// <summary>
        /// Obtains TimeRange information for a specified segment identifier
        /// TimeRange contains data on segment start time and its duration
        /// </summary>
        /// <param name="segmentId">Segment Identifier for which TimeRange is to be retrieved</param>
        /// <returns>TimeRange data. Null if specified segment ID does not
        /// correspond to a playable segment</returns>
        TimeRange SegmentTimeRange(uint? segmentId);

        /// <summary>
        /// Methods indicates if stream completed all preparation operations and is
        /// ready for usage with valid data.
        /// </summary>
        /// <returns>True - Ready. False - Not ready or preparation failed</returns>
        bool IsReady();

        /// <summary>
        /// Initializes representation stream.
        /// </summary>
        /// <param name="token">Cancellation token</param>
        void Initialize(CancellationToken token);
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
        public Period Period => this.AdaptationSet.Period;
        public DASH Document => this.AdaptationSet.Document;

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
        public DASH Document => this.Period.Document;

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

        [Xml.Element] public ProgramInformation[] ProgramInformation { get; internal set; }
        [Xml.Element] public BaseURL[] BaseURLs { get; internal set; }
        [Xml.Element] public string[] Locations { get; internal set; }
        [Xml.Element] public Period[] Periods { get; internal set; }
        [Xml.Element] public Metrics[] Metrics { get; internal set; }
    }

}

