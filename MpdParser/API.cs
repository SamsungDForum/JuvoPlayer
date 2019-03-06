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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MpdParser.Node;

namespace MpdParser
{
    public class Representation
    {
        internal struct AlignmentEntry
        {
            public uint? VideoSegmentID;
            public TimeSpan VideoSegmentStart;
            public uint? AudioSegmentID;
            public TimeSpan AudioSegmentStart;
            public TimeSpan AudioVideoStartDiff;

            public AlignmentEntry(uint? vSegId, TimeSpan vStart, uint? aSegId, TimeSpan aStart)
            {
                VideoSegmentID = vSegId;
                VideoSegmentStart = vStart;
                AudioSegmentID = aSegId;
                AudioSegmentStart = aStart;
                AudioVideoStartDiff = (VideoSegmentStart - AudioSegmentStart).Duration();
            }
        };

        // REPR or ADAPTATION SET:
        public string Id { get; }
        public string Profiles { get; }
        public uint? Width { get; }
        public uint? Height { get; }
        public string FrameRate { get; }
        public string MimeType { get; }
        public string Codecs { get; }
        public string SegmentProfiles { get; }

        // REPR only
        public uint? Bandwidth { get; }
        public uint? NumChannels { get; }
        public ulong? SampleRate { get; }

        public Node.IRepresentationStream Segments { get; }

        public uint? AlignedStartSegmentID { get; set; }

        public TimeSpan? AlignedTrimOffset { get; set; }

        public void SetDocumentParameters(ManifestParameters docParams)
        {
            Segments.SetDocumentParameters(docParams);
        }

        /// <summary>
        /// Function aligns Audio & Video Start Segments for dynamic & static content content.
        /// Static content no alignment is done, just selection of start segments and trim offsets
        /// </summary>
        /// <remarks>
        /// Video Segment is picked as start point. Segment Time Range (Start-Duration) is split
        /// into 4 separate time points between Segment.Start and Segment.Duration.
        /// For each of those points, audio segment is selected and its start time recorded.
        /// Such list is recorded for
        ///
        /// {Prev. Video Segment}.{Start Video Segment}.{Next Video Segment}
        ///
        /// if prev video segment is unavailable, then video segment source list is as following:
        ///
        /// {Start Video Segment}.{Next Video Segment}.{Next Video Segment}
        ///
        /// Once a list of {VideoSegmentId}.{Video Start Time}.{AudioSegmentID}.{Audio Start Time}
        /// is created, it is scanned for smallest differences between Video Start Time & Audio Start Time.
        /// Audio segment ID and Video Segment ID selected for aligned start parameters will have
        /// that difference smallest.
        ///
        /// After selecting start IDs for audio & video, associated start times of each segment are chosen as
        /// aligned trim Offsets.
        ///
        /// Reason for scanning multiple time points of video segment: A & V segments do not have to be aligned,
        /// as such there may be overlaps.
        ///
        /// Representation order (audio/video) is irrelevant, however, it is recommended to align audio TO video
        /// i.e.
        ///
        /// audioRepresentation.AlightStartSegmentWith( videoRepresentation )
        ///
        /// </remarks>
        /// <param name="alignTo">representation to align with (video)</param>
        public void AlignStartSegmentsWith(Representation alignTo)
        {
            // Sanity checks
            if (alignTo == null)
                throw new ArgumentNullException("Representations is null");

            var repAManifest = Segments.GetDocumentParameters();
            var repBManifest = alignTo.Segments.GetDocumentParameters();

            if (repAManifest == null || repBManifest == null)
                throw new ArgumentNullException($"Manifest Paraers not set. RepA {repAManifest == null} RepB {repBManifest == null}");

            if (repAManifest.Document.IsDynamic != repBManifest.Document.IsDynamic)
                throw new ArgumentException($"Representation class mismatch. RepA Dynamic {repAManifest.Document.IsDynamic} RepB Dynamic {repBManifest.Document.IsDynamic}");

            if (repAManifest.Document.IsDynamic)
                AlignDynamicStartParameters(alignTo);
            else
                AlignStaticStartParameters(alignTo);
        }

        private void AlignStaticStartParameters(Representation video)
        {
            var audioStartSegment = Segments.StartSegmentId();
            if (audioStartSegment == null)
                throw new ArgumentNullException("audio StartSegmentId is null");
            var audioTimeRange = Segments.SegmentTimeRange(audioStartSegment);
            if (audioTimeRange == null)
                throw new ArgumentNullException("audio SegmentTimeRange is null");

            var videoStartSegment = video.Segments.StartSegmentId();
            if (videoStartSegment == null)
                throw new ArgumentNullException("video StartSegmentId is null");
            var videoTimeRange = video.Segments.SegmentTimeRange(videoStartSegment);
            if (videoTimeRange == null)
                throw new ArgumentNullException("vdeo SegmentTimeRange is null");
            var trimOffset = audioTimeRange.Start < videoTimeRange.Start ? audioTimeRange.Start : videoTimeRange.Start;

            //Set aligned start data
            AlignedStartSegmentID = audioStartSegment;
            AlignedTrimOffset = trimOffset;
            video.AlignedStartSegmentID = videoStartSegment;
            video.AlignedTrimOffset = trimOffset;
        }

        private void AlignDynamicStartParameters(Representation video)
        {
            var alignments = new List<AlignmentEntry>();

            var videoStartSegment = video.Segments.StartSegmentId();
            var videoSegmentId = video.Segments.PreviousSegmentId(videoStartSegment);

            if (!videoSegmentId.HasValue)
                videoSegmentId = videoStartSegment;

            // Scan 3 video segments starting from videoSegmentId.
            for (int s = 0; s < 3; s++)
            {
                var videoTimeRange = video.Segments.SegmentTimeRange(videoSegmentId);

                if (videoTimeRange == null)
                    continue;

                // Split duration into 3 elements to cover 4 time points in segment duration
                // Start, Start+1/3*Duration, Start+2/3*Duration, Start+3/3*Duration
                //
                var tickOffset = videoTimeRange.Duration.Ticks / 3;
                var tickStart = videoTimeRange.Start.Ticks;

                for (int t = 0; t < 4; t++)
                {
                    // Get audio SegmentId & Start time for a given time point
                    var timePoint = TimeSpan.FromTicks(tickStart + (t * tickOffset));
                    var audioSegmentId = Segments.SegmentId(timePoint);
                    var audioTimeRange = Segments.SegmentTimeRange(audioSegmentId);

                    if (audioTimeRange == null)
                        continue;

                    alignments.Add(new AlignmentEntry(videoSegmentId, videoTimeRange.Start, audioSegmentId, audioTimeRange.Start));
                }
                // Get Next video segment
                videoSegmentId = video.Segments.NextSegmentId(videoSegmentId);
            }

            var audioStartSegment = videoStartSegment = null;
            TimeSpan? trimmOffset = null;

            // handle the unexpected
            if (alignments.Count > 0)
            {
                var bestAlignment = (from AlignmentEntry in alignments
                                     orderby AlignmentEntry.AudioVideoStartDiff
                                     select AlignmentEntry).First();

                videoStartSegment = bestAlignment.VideoSegmentID;
                audioStartSegment = bestAlignment.AudioSegmentID;
                var videoStart = bestAlignment.VideoSegmentStart;
                var audioStart = bestAlignment.AudioSegmentStart;

                trimmOffset = videoStart < audioStart ? videoStart : audioStart;
            }

            //Set aligned start data
            AlignedStartSegmentID = audioStartSegment;
            AlignedTrimOffset = trimmOffset;
            video.AlignedStartSegmentID = videoStartSegment;
            video.AlignedTrimOffset = trimmOffset;
        }

        public Representation(Node.Representation repr)
        {
            Id = repr.Id;
            Profiles = repr.Profiles ?? repr.AdaptationSet.Profiles;
            Width = repr.Width ?? repr.AdaptationSet.Width;
            Height = repr.Height ?? repr.AdaptationSet.Height;
            FrameRate = repr.FrameRate ?? repr.AdaptationSet.FrameRate;
            MimeType = repr.MimeType ?? repr.AdaptationSet.MimeType;
            Codecs = repr.Codecs ?? repr.AdaptationSet.Codecs;
            SegmentProfiles = repr.SegmentProfiles ?? repr.AdaptationSet.SegmentProfiles;

            Bandwidth = repr.Bandwidth;
            NumChannels = repr.NumChannels;
            SampleRate = repr.SampleRate;

            Segments = repr.SegmentsStream();

            // Check if repr. is valid - Entries without representation
            // specification are valid too.
            if (NumChannels == null && repr.AudioChannelConfigurations != null)
            {
                string channels = null;
                foreach (Node.Descriptor d in repr.AudioChannelConfigurations)
                {
                    if (!string.IsNullOrEmpty(d.Value))
                    {
                        channels = d.Value;
                        break;
                    }
                }

                if (channels != null)
                    NumChannels = System.Xml.XmlConvert.ToUInt32(channels);
            }

            if (SampleRate == null)
            {
                string sampling = repr.AudioSamplingRate ?? repr.AdaptationSet.AudioSamplingRate;
                if (sampling != null)
                    SampleRate = System.Xml.XmlConvert.ToUInt32(sampling);
            }
        }

        static string GetMimeType(string type, string codecs)
        {
            if (string.IsNullOrEmpty(type))
                return null;

            if (string.IsNullOrEmpty(codecs))
                return type;

            return type + "; codecs=" + codecs;
        }

        static string Size(uint? width, uint? height)
        {
            if (width == null && height == null)
                return null;
            return (width?.ToString() ?? "-") + "x" + (height?.ToString() ?? "-");
        }

        static string Channels(uint? num)
        {
            if (num == null)
                return null;
            switch (num.Value)
            {
                case 1: return "mono";
                case 2: return "stereo";
            }
            return num.Value.ToString() + "ch";
        }

        static string Rate(ulong? rate)
        {
            if (rate == null)
                return null;
            string suffix = "Hz";
            ulong val = rate.Value * 10;

            string[] fixes = { "kHz", "MHz", "GHz" };

            foreach (string fix in fixes)
            {
                if (val < 10000)
                    break;
                val /= 1000;
                suffix = fix;
            }

            if ((val % 10) == 0)
                return (val / 10).ToString() + suffix;
            return (val / 10).ToString() + "." + (val % 10).ToString() + suffix;
        }

        public override string ToString()
        {
            string result = (Bandwidth?.ToString() ?? "-");
            if (Profiles != null)
                result += " / " + Profiles.ToString();
            result += " / " + (GetMimeType(MimeType, Codecs) ?? "-");

            string size = Size(Width, Height);

            if (size != null || FrameRate != null)
            {
                result += " [" + (size ?? "");
                if (FrameRate != null)
                    result += "@" + FrameRate;
                result += "]";
            }

            string rate = Rate(SampleRate);
            string ch = Channels(NumChannels);

            if (rate != null || ch != null)
            {
                result += " [" + (rate ?? "");
                if (rate != null && ch != null)
                    result += " ";
                result += (ch ?? "") + "]";
            }

            return result + " (duration:" + ((Segments?.Duration)?.ToString() ?? "-") + ")";
        }
    }

    public enum MediaRole
    {
        Main,
        Alternate,
        Captions,
        Subtitle,
        Supplementary,
        Commentary,
        Dub,
        Other
    };

    public class Role
    {
        public MediaRole Kind { get; }
        public string Scheme { get; }
        public string Value { get; }
        public Role(string scheme, string value)
        {
            Kind = MediaRole.Other;
            Scheme = scheme;
            Value = value;
            if (scheme.Equals(Node.AdaptationSet.UrnRole) ||
                scheme.Equals(Node.AdaptationSet.UrnRole2011))
            {
                Kind = ParseDashUrn(value);
            }
        }
        public static MediaRole ParseDashUrn(string value)
        {
            if (value == "main") return MediaRole.Main;
            if (value == "alternate") return MediaRole.Alternate;
            if (value == "captions") return MediaRole.Captions;
            if (value == "subtitle") return MediaRole.Subtitle;
            if (value == "supplementary") return MediaRole.Supplementary;
            if (value == "commentary") return MediaRole.Commentary;
            if (value == "dub") return MediaRole.Dub;
            return MediaRole.Other;
        }
        public override string ToString()
        {
            if (Kind == MediaRole.Other)
                return Scheme + "/" + Value;
            return Value;
        }
    }


    public enum MediaType
    {
        Unknown,
        Video,
        Audio,
        Application,
        Text,
        Other
    };

    public class MimeType
    {
        public MediaType Value { get; }
        public string Key { get; }
        public MimeType(string value)
        {
            Key = value;
            Value = Parse(value);
        }
        public static MediaType Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return MediaType.Unknown;
            if (value.IndexOf("video", StringComparison.OrdinalIgnoreCase) >= 0) return MediaType.Video;
            if (value.IndexOf("audio", StringComparison.OrdinalIgnoreCase) >= 0) return MediaType.Audio;
            if (value.IndexOf("application", StringComparison.OrdinalIgnoreCase) >= 0) return MediaType.Application;
            if (value.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0) return MediaType.Text;
            return MediaType.Other;
        }
        public override string ToString()
        {
            return Key;
        }
    }

    public class ContentProtection
    {
        public string CencDefaultKID { get; }
        public string SchemeIdUri { get; }
        public string Value { get; }
        public string Data { get; }

        public ContentProtection(Node.ContentProtection node)
        {
            CencDefaultKID = node.CencDefaultKID;
            Data = node.Data;
            SchemeIdUri = node.SchemeIdUri;
            Value = node.Value;
        }
    }

    public class AdaptationSet
    {
        public uint? Id { get; }
        public uint? Group { get; }
        public string Lang { get; }
        public ContentProtection[] ContentProtections { get; }
        public MimeType Type { get; }
        public Role[] Roles { get; }
        public Representation[] Representations { get; }

        public void SetDocumentParameters(ManifestParameters docParams)
        {
            foreach (var rep in Representations)
            {
                rep.SetDocumentParameters(docParams);
            }
        }

        public AdaptationSet(Node.AdaptationSet set)
        {
            Id = set.Id;
            Group = set.Group;
            Lang = set.GetLang() ?? "und";

            ContentProtections = new ContentProtection[set.ContentProtections.Length];
            for (int i = 0; i < ContentProtections.Length; ++i)
                ContentProtections[i] = new ContentProtection(set.ContentProtections[i]);

            Roles = new Role[set.Roles.Length];
            for (int i = 0; i < Roles.Length; ++i)
            {
                Node.Descriptor r = set.Roles[i];
                Roles[i] = new Role(r.SchemeIdUri, r.Value);
            }

            Representations = new Representation[set.Representations.Length];
            for (int i = 0; i < Representations.Length; ++i)
            {
                Representations[i] = new Representation(set.Representations[i]);
            }

            Type = new MimeType(set.GetContentType() ??
                GuessFromMimeType(set.MimeType) ??
                GuessFromRepresentations());
        }

        public bool HasRole(MediaRole kind)
        {
            if (kind == MediaRole.Other)
                return false;

            foreach (Role r in Roles)
            {
                if (r.Kind == kind)
                    return true;
            }
            return false;
        }

        public bool HasRole(string uri, string kind)
        {
            if (uri.Equals(Node.AdaptationSet.UrnRole) ||
                uri.Equals(Node.AdaptationSet.UrnRole2011))
                return HasRole(Role.ParseDashUrn(kind));

            foreach (Role r in Roles)
            {
                if (r.Kind != MediaRole.Other)
                    continue;

                if (r.Scheme.Equals(uri) && r.Value.Equals(kind))
                    return true;
            }
            return false;
        }

        public override string ToString()
        {
            string result = Lang + " / " + Type;
            if (Roles.Length > 0)
                result += " [" + string.Join(", ", (IEnumerable<Role>)Roles) + "]";

            // When querying representation for longest segment, use LongestReadySegment
            // as LongestSegment will wait for all segment initialization to complete.
            // Full info is not required for ToString() purposes.
            return result + " (length:" + (LongestReadySegment()?.ToString() ?? "-") + ")";
        }

        private string GuessFromMimeType(string mimeType)
        {
            if (mimeType == null)
                return null;
            string[] mime = mimeType.Split(new char[] { '/' }, 2);
            if (mime.Length > 1)
                return mime[0];
            return null;
        }

        private string GuessFromRepresentations()
        {
            string guessed = null;
            foreach (var r in Representations)
            {
                var candidate = GuessFromMimeType(r.MimeType);
                if (candidate == null) continue;
                if (guessed == null) guessed = candidate;
                else if (guessed != candidate) return null; // At least two different types
            }
            return guessed;
        }

        public TimeSpan? LongestSegment()
        {
            TimeSpan? current = null;
            foreach (var repr in Representations)
            {
                TimeSpan? length;

                length = repr.Segments?.Duration;

                if (length == null) continue;
                if (current == null)
                {
                    current = length.Value;
                    continue;
                }
                if (length.Value > current.Value)
                    current = length.Value;
            }
            return current;
        }

        public TimeSpan? LongestReadySegment()
        {
            TimeSpan? current = null;
            foreach (var repr in Representations)
            {
                TimeSpan? length;

                length = repr.Segments?.IsReady() == true ?
                    repr.Segments?.Duration : null;

                if (length == null) continue;
                if (current == null)
                {
                    current = length.Value;
                    continue;
                }
                if (length.Value > current.Value)
                    current = length.Value;
            }
            return current;
        }
    }


    public class ManifestParameters
    {
        public DocumentParameters Document { get; }

        public PeriodParameters Period { get; }

        public TimeSpan PlayClock { get; set; }

        public ManifestParameters(Document aDocument, Period aPeriod)
        {
            Document = new DocumentParameters(aDocument);
            Period = new PeriodParameters(aPeriod);
        }
    }

    /// <summary>
    /// DocumentParameters is a pass through information from Document.
    /// Intentionally, a separate class is used so all parameters from currently used
    /// Document / Period as COPIED rather then referenced to original doc as this
    /// may get updated.
    /// </summary>
    public class PeriodParameters
    {
        public TimeSpan? Start { get; }
        public TimeSpan? Duration { get; }
        public Period.Types Type { get; }

        public PeriodParameters(Period aPeriod)
        {
            // Remember COPY data. Do not reference it.

            // Structs (TimeSpan) are copied by default
            Start = aPeriod.Start;
            Duration = aPeriod.Duration;
            Type = aPeriod.Type;

        }

    }

    public class Period : IComparable
    {
        public enum Types
        {
            Unknown,
            Regular,
            EarlyAvailable

        }

        public TimeSpan? Start { get; internal set; }
        public TimeSpan? Duration { get; internal set; }
        public Types Type { get; internal set; }

        public AdaptationSet[] Sets { get; }

        public DateTime? PeriodEnd;

        /// <summary>
        /// Period Builder. Converts Node.Period[] into MpdParser.Period[]. If docType is passed
        /// with a value other then DocumentType.Unknown, adjustment of periods Start Time and Type
        /// is performed.
        /// For dynamic content, periods will be trimmed if their availability time is out (beyond wallClock)
        /// </summary>
        /// <param name="periods"> Table of raw dash periods</param>
        /// <param name="aDoc">DASH Document</param>
        /// <returns>Array of Periods (MpdParser.Period[]) derived from periods argument</returns>
        public static Period[] BuildPeriods(Node.Period[] periods, Document aDoc)
        {
            var res = periods.Select(o => new Period(o)).ToArray();

            if (aDoc.Type != DocumentType.Unknown)
                res = AdjustStartAndType(res, aDoc);

            return res;
        }

        /// <summary>
        /// Adjusts Period Start times and Types according to ISO IEC 23009-1 Section 5.3.2.1
        /// </summary>
        /// <param name="periods">List of MpdParser.Period for which adjustments are to be made</param>
        /// <param name="aDoc">DASH Document </param>
        /// <returns>Period[] Adjusted list of periods</returns>
        private static Period[] AdjustStartAndType(Period[] periods, Document aDoc)
        {
            if (periods.Length == 0)
                return null;

            // Ifs are intentionally unoptimized now
            // reflecting spec wording and its interpretation
            if (periods.Length > 0)
            {
                // If the attribute @start is present in the Period, then the Period is a regular Period
                // and the PeriodStart is equal to the value of this attribute.
                //
                // This spec leaves me puzzled. If period is not missing, type is regular.
                // If period is missing, but it is static and first in file, create StartTime=0.
                // should this imply type = regular? Local DASH experts:
                // k.kluczek@samsung.com j.wasikowski@samsung.com j.gabryel@samsung.com
                // state that all periods are "regular" by default, unless
                // period[n].start=null && period[n-1].duration == null
                //
                periods[0].Type = Types.Regular;

                // If (i) @start attribute is absent,
                // and(ii) the Period element is the first in the MPD,
                // and (iii) the MPD@type is 'static', then the PeriodStart time shall be set to
                // zero.
                if (periods[0].Start == null && aDoc.Type == DocumentType.Static)
                    periods[0].Start = new TimeSpan(0);

            }
            for (int i = 1; i < periods.Length - 1; i++)
            {
                // A.3.2 ISO ISEC 23009-1:2014
                // the Period end time referred as PeriodEnd is determined as follows:
                // For any Period in the MPD except for the last one, the PeriodEnd is
                // obtained as the value of the PeriodStart of the next Period.
                // For the last Period in the MPD:...
                // (this will be described "after" this loop)
                if (periods[i - 1].Duration == null && periods[i].Start != null)
                {
                    periods[i - 1].Duration = periods[i].Start - periods[i - 1].Start;
                }
                // If the @start attribute is absent, but the previous Period element contains
                // a @duration attribute then this new Period is also a regular Period.
                // The start time of the new Period PeriodStart is the sum of the start time of
                // the previous Period PeriodStart and the value of the attribute @duration of
                // the previous Period.
                if (periods[i].Start == null && periods[i - 1].Duration != null)
                {
                    periods[i].Type = Types.Regular;
                    periods[i].Start = periods[i - 1].Start + periods[i - 1].Duration;
                }

                // If(i) @start attribute is absent, and(ii) the previous Period element does not
                // contains a @duration attribute or the Period element is the first in the MPD,
                // and (iii) the MPD@type is 'dynamic', then this Period is an
                // Early Available Period
                if (periods[i].Start == null && periods[i - 1].Duration == null &&
                    aDoc.Type == DocumentType.Dynamic)
                    periods[i].Type = Types.EarlyAvailable;
            }

            //Last Period Fixup based on A.3.2 ISO ISEC 23009-1:2014
            var lastPeriod = periods.Length - 1;
            if (aDoc.MinimumUpdatePeriod.HasValue == false)
            {
                periods[lastPeriod].PeriodEnd = aDoc.AvailabilityStartTime + aDoc.MediaPresentationDuration;
            }
            else
            {
                var v1 = DateTime.UtcNow + (aDoc.MinimumUpdatePeriod ?? TimeSpan.Zero);
                var v2 = aDoc.AvailabilityStartTime ?? DateTime.MinValue;
                v2 += aDoc.MediaPresentationDuration ?? TimeSpan.Zero;

                periods[lastPeriod].PeriodEnd = new DateTime(Math.Min(v1.Ticks, v2.Ticks));
            }

            return periods;
        }

        public Period(Node.Period period)
        {
            Start = period.Start;
            Duration = period.Duration;
            Type = Types.Unknown;
            Sets = new AdaptationSet[period.AdaptationSets.Length];
            for (int i = 0; i < Sets.Length; ++i)
                Sets[i] = new AdaptationSet(period.AdaptationSets[i]);
        }

        public override string ToString()
        {
            return $"{Type}: ({Start})-({Duration})";
        }

        public TimeSpan? LongestAdaptationSet()
        {
            TimeSpan? current = null;
            foreach (var set in Sets)
            {
                TimeSpan? length = set.LongestSegment();
                if (length == null) continue;
                if (current == null)
                {
                    current = length.Value;
                    continue;
                }
                if (length.Value > current.Value)
                    current = length.Value;
            }
            return current;
        }

        public TimeSpan? LongestReadyAdaptationSet()
        {
            TimeSpan? current = null;
            foreach (var set in Sets)
            {
                TimeSpan? length = set.LongestReadySegment();
                if (length == null) continue;
                if (current == null)
                {
                    current = length.Value;
                    continue;
                }
                if (length.Value > current.Value)
                    current = length.Value;
            }
            return current;
        }

        public int CompareTo(object obj)
        {
            Period p = (Period)obj;
            if (this.Start == null)
            {
                if (p.Start == null)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            else if (p.Start == null)
            {
                return 1;
            }
            else
            {
                return TimeSpan.Compare(
                    (TimeSpan)this.Start,
                    (TimeSpan)p.Start);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="manifestParams"></param>
        public void SetManifestParameters(ManifestParameters manifestParams)
        {
            Sets.ToList().ForEach(media => media.SetDocumentParameters(manifestParams));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public List<AdaptationSet> GetAdaptationSets(MediaType type)
        {
            return Sets.Where(o => o.Type.Value == type).ToList();
        }

        /// <summary>
        /// Initializes media components in Period
        /// </summary>
        public void Initialize(CancellationToken token)
        {
            foreach (var set in Sets)
            {
                foreach (var rep in set.Representations)
                {
                    if (!rep.Segments.IsReady())
                        rep.Segments.Initialize(token);
                }
            }
        }
    }

    /// <summary>
    /// DocumentParameters is a pass through information from Document.
    /// Intentionally, a separate class is used so all parameters from currently used
    /// Document / Period as COPIED rather then referenced to original doc as this
    /// may get updated.
    /// </summary>
    public class DocumentParameters
    {
        public bool IsDynamic { get; }

        public TimeSpan? MediaPresentationDuration { get; }
        public TimeSpan? MinBufferTime { get; }
        public DateTime? AvailabilityStartTime { get; }
        public DateTime? PublishTime { get; }
        public DateTime? AvailabilityEndTime { get; }
        public TimeSpan? TimeShiftBufferDepth { get; }
        public DateTime DownloadRequestTime { get; }
        public DateTime DownloadCompleteTime { get; }
        public DateTime ManifestParseCompleteTime { get; }
        public TimeSpan? SuggestedPresentationDelay { get; }
        public TimeSpan TimeOffset { get; }

        public DocumentParameters(Document aDocument)
        {
            // Remember COPY all data. Do not "assign" by reference.
            IsDynamic = aDocument.IsDynamic;

            //TimeSpan is a structure. "copies" by default...
            MediaPresentationDuration = aDocument.MediaPresentationDuration;
            MinBufferTime = aDocument.MinBufferTime;
            TimeShiftBufferDepth = aDocument.TimeShiftBufferDepth;

            //DateTime is also a struct
            AvailabilityStartTime = aDocument.AvailabilityStartTime;
            AvailabilityEndTime = aDocument.AvailabilityEndTime;
            PublishTime = aDocument.PublishTime;
            DownloadRequestTime = aDocument.DownloadRequestTime;
            DownloadCompleteTime = aDocument.DownloadCompleteTime;
            ManifestParseCompleteTime = aDocument.ParseCompleteTime;

            SuggestedPresentationDelay = aDocument.SuggestedPresentationDelay;

            TimeOffset = IsDynamic == true ? ManifestParseCompleteTime - DownloadCompleteTime : TimeSpan.Zero;
        }
    }


    public enum DocumentType
    {
        Unknown,
        Static,
        Dynamic
    };

    public class Document
    {
        public string Title { get; }
        public TimeSpan? MediaPresentationDuration { get; }
        public TimeSpan? MinBufferTime { get; }
        public TimeSpan? TimeShiftBufferDepth { get; }
        public TimeSpan? SuggestedPresentationDelay { get; }
        public string[] Profiles { get; }
        public Period[] Periods { get; }

        public DocumentType Type { get; set; }

        public TimeSpan? MinimumUpdatePeriod { get; internal set; }
        public DateTime? AvailabilityStartTime { get; internal set; }
        public DateTime? PublishTime { get; internal set; }

        public DateTime? AvailabilityEndTime { get; internal set; }
        public DateTime DownloadRequestTime { get; set; }
        public DateTime DownloadCompleteTime { get; set; }
        public DateTime ParseCompleteTime { get; set; }

        public bool IsDynamic => (Type == DocumentType.Dynamic);

        private Document(Node.DASH dash)
        {
            MediaPresentationDuration = dash.MediaPresentationDuration;
            MinBufferTime = dash.MinBufferTime;
            MinimumUpdatePeriod = dash.MinimumUpdatePeriod;
            AvailabilityStartTime = dash.AvailabilityStartTime;
            PublishTime = dash.PublishTime;
            AvailabilityEndTime = dash.AvailabilityEndTime;
            MediaPresentationDuration = dash.MediaPresentationDuration;
            TimeShiftBufferDepth = dash.TimeShiftBufferDepth;
            SuggestedPresentationDelay = dash.SuggestedPresentationDelay;


            Profiles = Xml.Conv.A2s(dash.Profiles);

            Type = ParseDashType(dash);

            if (Type == DocumentType.Dynamic)
            {
                if (AvailabilityStartTime == null || PublishTime == null)
                {
                    throw new ArgumentException("Malformed MPD. MPD.Type=Dynamic ISO IEC 23009-1 requires" +
                        $"AvailabilityStartTime is '{AvailabilityStartTime}'" +
                        $"PublishTime is '{PublishTime}' To be non null");
                }
            }

            foreach (Node.ProgramInformation info in dash.ProgramInformation)
            {
                foreach (string title in info.Titles)
                {
                    if (!string.IsNullOrEmpty(title))
                    {
                        Title = title;
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(Title))
                    break;
            }

            Periods = Period.BuildPeriods(dash.Periods, this);

            Array.Sort(Periods);
        }

        public TimeSpan WallClock(TimeSpan time)
        {
            if (!IsDynamic)
                return time;

            return DateTime.UtcNow.Subtract(AvailabilityStartTime ?? DateTime.MinValue);

        }

        /// <summary>
        /// Finds a period that matches current playback timestamp. May return "early access"
        /// Period. Based on ISO IEC 23009-1 2015 section 5.3.2.1 & DASH IF IOP v 3.3
        /// </summary>
        /// <param name="timeIndex"></param>
        /// <returns></returns>
        public Period FindPeriod(TimeSpan timeIndex)
        {
            var timeIndexTotalSeconds = Math.Truncate(timeIndex.TotalSeconds);
            // Periods are already "sorted" from lowest to highest, thus find a period
            // where start time (time elapsed when this period should start)
            // to do so, search periods backwards, starting from those that should play last.
            for (var p = Periods.Length - 1; p >= 0; p--)
            {
                var period = Periods[p];

                var availstart = AvailabilityStartTime ?? DateTime.UtcNow;
                var start = IsDynamic ? DateTime.UtcNow.Subtract(availstart) : TimeSpan.Zero;

                start = start.Add(period.Start ?? TimeSpan.Zero);
                var end = start.Add(period.Duration ?? TimeSpan.Zero);

                if (timeIndexTotalSeconds >= Math.Truncate(start.TotalSeconds)
                    && timeIndexTotalSeconds <= Math.Truncate(end.TotalSeconds))
                {
                    return period;
                }
            }

            return null;
        }

        private static DocumentType ParseDashType(Node.DASH dash)
        {
            switch (dash.Type.ToLowerInvariant())
            {
                case "dynamic":
                    return DocumentType.Dynamic;
                case "static":
                default:        // ISO IEC 23009-1 Section 5.3.1.2 Table 3.
                                // Type is optional with default value of "static"
                    return DocumentType.Static;
            }
        }

        public static async Task<Document> FromText(string manifestText, string manifestUrl)
        {
            var dash = await FromTextInternal(manifestText, manifestUrl);
            dash.PeriodFixup();

            return new Document(dash);
        }

        public static async Task<DASH> FromTextInternal(string manifestText, string manifestUrl)
        {
            Node.DASH dash = new Node.DASH(manifestUrl);
            System.IO.StringReader reader = new System.IO.StringReader(manifestText);
            await Xml.Parser.ParseAsync(reader, dash, "MPD");
            return dash;
        }

        public override string ToString()
        {
            return $"Type: {Type} StartTime: ({AvailabilityStartTime}) EndTime: ({AvailabilityEndTime}) " +
                $"Duration: ({MediaPresentationDuration}) PublishTime: ({PublishTime}) " +
                $"MinBufferTime: ({MinBufferTime}) MinUpdatePeriod ({MinimumUpdatePeriod})";
        }
    }
}

