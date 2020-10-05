/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2020, Samsung Electronics Co., Ltd
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
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using JuvoPlayer.Common;
using JuvoPlayer.Dash.MPD;
using Period = JuvoPlayer.Dash.MPD.Period;

namespace JuvoPlayer.Dash
{
    public class DashManifestParser
    {
        private static readonly Regex _frameRateRegex = new Regex("(\\d+)(?:/(\\d+))?");
        private static readonly Regex _cea608AccessibilityRegex = new Regex("CC([1-4])=.*");
        private static readonly Regex _cea708AccessibilityRegex = new Regex("([1-9]|[1-5][0-9]|6[0-3])=.*");

        public Manifest Parse(Stream input, string baseUrl)
        {
            var document = new XmlDocument();
            document.Load(input);
            var mpd = document["MPD"];
            if (mpd == null)
                throw new ArgumentException("No MPD element found");
            return ParseMpd(mpd, baseUrl);
        }

        private Manifest ParseMpd(XmlElement mpd, string baseUrl)
        {
            var duration = ParseDuration(mpd, "mediaPresentationDuration", null);
            var availabilityStartTime = ParseDateTime(mpd, "availabilityStartTime", null);
            var type = ParseString(mpd, "type", "static");
            var dynamic = type.Equals("dynamic");
            var publishTime = ParseDateTime(mpd, "publishTime", null);
            var timeShiftBufferDepth = ParseDuration(mpd, "timeShiftBufferDepth", null);
            var suggestedPresentationDelay = ParseDuration(mpd, "suggestedPresentationDelay", null);
            var minimumUpdatePeriod = ParseDuration(mpd, "minimumUpdatePeriod", null);
            var minBufferTime = ParseDuration(mpd, "minBufferTime", null);
            var seenFirstBaseUrl = false;
            var seenEarlyAccessPeriod = false;

            ProgramInformation programInformation = null;
            UtcTiming utcTiming = null;
            Uri location = null;
            var periods = new List<Period>();
            var nextPeriodStart = dynamic ? (TimeSpan?)null : TimeSpan.Zero;

            foreach (XmlNode node in mpd.ChildNodes)
            {
                if (!(node is XmlElement))
                    continue;
                var element = node as XmlElement;

                if (element.Name.Equals("BaseURL"))
                {
                    if (!seenFirstBaseUrl)
                    {
                        baseUrl = element.InnerText;
                        seenFirstBaseUrl = true;
                    }
                }
                else if (element.Name.Equals("ProgramInformation"))
                {
                    programInformation = ParseProgramInformation(element);
                }
                else if (element.Name.Equals("Location"))
                {
                    location = new Uri(element.InnerText);
                }
                else if (element.Name.Equals("UTCTiming"))
                {
                    utcTiming = ParseUtcTiming(element);
                }
                else if (element.Name.Equals("Period") && !seenEarlyAccessPeriod)
                {
                    var (period, periodDuration) = ParsePeriod(element, baseUrl, nextPeriodStart);
                    if (!period.Start.HasValue)
                    {
                        if (dynamic)
                            seenEarlyAccessPeriod = true;
                        else
                            throw new ArgumentException("Period doesn't have a start time");
                    }
                    else
                    {
                        nextPeriodStart = !periodDuration.HasValue ? null : period.Start + periodDuration;
                        periods.Add(period);
                    }
                }
            }

            if (duration == null && nextPeriodStart != null) duration = nextPeriodStart;

            if (duration == null && !dynamic)
                throw new ArgumentException("Unable to determine duration");

            return new Manifest(duration, availabilityStartTime, publishTime, minBufferTime,
                timeShiftBufferDepth, suggestedPresentationDelay, minimumUpdatePeriod, programInformation, utcTiming,
                periods, dynamic, location);
        }

        private (Period, TimeSpan?) ParsePeriod(XmlElement element, string baseUrl, TimeSpan? defaultPeriodStart)
        {
            var id = element.GetAttribute("id");
            var start = ParseDuration(element, "start", defaultPeriodStart);
            var duration = ParseDuration(element, "duration", null);
            SegmentBase segmentBase = null;
            Descriptor assetIdentifier = null;
            var adaptationSets = new List<AdaptationSet>();
            var seenFirstBaseUrl = false;

            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement))
                    continue;
                var childElement = node as XmlElement;
                if (childElement.Name.Equals("BaseURL"))
                {
                    if (!seenFirstBaseUrl)
                    {
                        baseUrl = ParseBaseUrl(childElement, baseUrl);
                        seenFirstBaseUrl = true;
                    }
                }
                else if (childElement.Name.Equals("AdaptationSet"))
                {
                    adaptationSets.Add(ParseAdaptationSet(childElement, baseUrl, segmentBase, duration));
                }
                else if (childElement.Name.Equals("SegmentBase"))
                {
                    segmentBase = ParseSegmentBase(childElement, null);
                }
                else if (childElement.Name.Equals("SegmentList"))
                {
                    segmentBase = ParseSegmentList(childElement, null, duration);
                }
                else if (childElement.Name.Equals("SegmentTemplate"))
                {
                    segmentBase = ParseSegmentTemplate(childElement, null, new List<Descriptor>(), duration);
                }
                else if (childElement.Name.Equals("AssetIdentifier"))
                {
                    assetIdentifier = ParseDescriptor(childElement);
                }
            }

            return (new Period(id, start, adaptationSets), duration);
        }

        private AdaptationSet ParseAdaptationSet(XmlElement element, string baseUrl, SegmentBase segmentBase,
            TimeSpan? periodDuration)
        {
            var id = ParseInt(element, "id");
            var contentType = ParseContentType(element);
            var mimeType = element.GetAttribute("mimeType");
            var codecs = element.GetAttribute("codecs");
            var width = ParseInt(element, "width");
            var height = ParseInt(element, "height");
            var frameRate = ParseFrameRate(element);
            int? audioChannels = null;
            var audioSamplingRate = ParseInt(element, "audioSamplingRate");
            var language = element.GetAttribute("lang");
            var label = element.GetAttribute("label");
            string drmSchemeType = null;
            var contentProtections = new List<ContentProtection>();
            var inbandEventStreams = new List<Descriptor>();
            var accessibilityDescriptors = new List<Descriptor>();
            var roleDescriptors = new List<Descriptor>();
            var supplementalProperties = new List<Descriptor>();
            var representationInfos = new List<RepresentationInfo>();

            var seenFirstBaseUrl = false;
            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement))
                    continue;
                var childElement = node as XmlElement;
                if (childElement.Name.Equals("BaseURL"))
                {
                    if (!seenFirstBaseUrl)
                    {
                        baseUrl = ParseBaseUrl(childElement, baseUrl);
                        seenFirstBaseUrl = true;
                    }
                }
                else if (childElement.Name.Equals("ContentProtection"))
                {
                    var (newDrmSchemeType, contentProtection) = ParseContentProtection(childElement);
                    if (newDrmSchemeType != null)
                        drmSchemeType = newDrmSchemeType;
                    if (contentProtection != null)
                        contentProtections.Add(contentProtection);
                }
                else if (childElement.Name.Equals("ContentComponent"))
                {
                    language = string.IsNullOrEmpty(language) ? childElement.GetAttribute("language") : language;
                    contentType = contentType == ContentType.Unknown ? ParseContentType(childElement) : contentType;
                }
                else if (childElement.Name.Equals("Role"))
                {
                    roleDescriptors.Add(ParseDescriptor(childElement));
                }
                else if (childElement.Name.Equals("AudioChannelConfiguration"))
                {
                    audioChannels = ParseAudioChannelConfiguration(childElement);
                }
                else if (childElement.Name.Equals("Accessibility"))
                {
                    accessibilityDescriptors.Add(ParseDescriptor(childElement));
                }
                else if (childElement.Name.Equals("SupplementalProperty"))
                {
                    supplementalProperties.Add(ParseDescriptor(childElement));
                }
                else if (childElement.Name.Equals("Representation"))
                {
                    var representationInfo = ParseRepresentation(childElement, baseUrl, mimeType, codecs, width, height,
                        frameRate, audioChannels, audioSamplingRate, language, roleDescriptors,
                        accessibilityDescriptors, supplementalProperties, segmentBase, periodDuration);
                    contentType = contentType != ContentType.Unknown
                        ? contentType
                        : GetContentType(representationInfo.Format);
                    representationInfos.Add(representationInfo);
                }
                else if (childElement.Name.Equals("SegmentBase"))
                {
                    segmentBase = ParseSegmentBase(childElement, (SingleSegmentBase)segmentBase);
                }
                else if (childElement.Name.Equals("SegmentList"))
                {
                    segmentBase = ParseSegmentList(childElement, (SegmentList)segmentBase, periodDuration);
                }
                else if (childElement.Name.Equals("SegmentTemplate"))
                {
                    segmentBase = ParseSegmentTemplate(childElement, (SegmentTemplate)segmentBase,
                        supplementalProperties,
                        periodDuration);
                }
                else if (childElement.Name.Equals("InbandEventStream"))
                {
                    inbandEventStreams.Add(ParseDescriptor(childElement));
                }
                else if (childElement.Name.Equals("Label"))
                {
                    label = ParseLabel(childElement);
                }
            }

            var representations = new List<Representation>(representationInfos.Capacity);
            foreach (var representationInfo in representationInfos)
            {
                representations.Add(BuildRepresentation(representationInfo, label, drmSchemeType, contentProtections,
                    inbandEventStreams));
            }

            return new AdaptationSet(id, contentType, representations, accessibilityDescriptors,
                supplementalProperties);
        }

        private RepresentationInfo ParseRepresentation(XmlElement element, string baseUrl, string adaptationSetMimeType,
            string adaptationSetCodecs, int? adaptationSetWidth, int? adaptationSetHeight,
            float? adaptationSetFrameRate, int? adaptationSetAudioChannels, int? adaptationSetAudioSamplingRate,
            string adaptationSetLanguage, List<Descriptor> adaptationSetRoleDescriptors,
            List<Descriptor> adaptationSetAccessibilityDescriptors,
            List<Descriptor> adaptationSetSupplementalProperties, SegmentBase segmentBase, TimeSpan? periodDuration)
        {
            var id = element.GetAttribute("id");
            var bandwidth = ParseInt(element, "bandwidth");
            var mimeType = ParseString(element, "mimeType", adaptationSetMimeType);
            var codecs = ParseString(element, "codecs", adaptationSetCodecs);
            var width = ParseInt(element, "width", adaptationSetWidth);
            var height = ParseInt(element, "height", adaptationSetHeight);
            var frameRate = ParseFrameRate(element, adaptationSetFrameRate);
            var audioChannels = adaptationSetAudioChannels;
            var audioSamplingRate = ParseInt(element, "audioSamplingRate", adaptationSetAudioSamplingRate);
            string drmSchemeType = null;
            var contentProtections = new List<ContentProtection>();
            var inbandEventStreams = new List<Descriptor>();
            var supplementalProperties = new List<Descriptor>();

            var seenFirstBaseUrl = false;

            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement))
                    continue;
                var childElement = node as XmlElement;
                if (childElement.Name.Equals("BaseURL"))
                {
                    if (!seenFirstBaseUrl)
                    {
                        baseUrl = ParseBaseUrl(childElement, baseUrl);
                        seenFirstBaseUrl = true;
                    }
                }
                else if (childElement.Name.Equals("AudioChannelConfiguration"))
                {
                    audioChannels = ParseAudioChannelConfiguration(childElement);
                }
                else if (childElement.Name.Equals("SegmentBase"))
                {
                    segmentBase = ParseSegmentBase(childElement, (SingleSegmentBase)segmentBase);
                }
                else if (childElement.Name.Equals("SegmentList"))
                {
                    segmentBase = ParseSegmentList(childElement, (SegmentList)segmentBase, periodDuration);
                }
                else if (childElement.Name.Equals("SegmentTemplate"))
                {
                    segmentBase = ParseSegmentTemplate(childElement, (SegmentTemplate)segmentBase,
                        adaptationSetSupplementalProperties, periodDuration);
                }
                else if (childElement.Name.Equals("ContentProtection"))
                {
                    var (newDrmSchemeType, contentProtection) = ParseContentProtection(childElement);
                    if (newDrmSchemeType != null)
                        drmSchemeType = newDrmSchemeType;
                    if (contentProtection != null)
                        contentProtections.Add(contentProtection);
                }
                else if (childElement.Name.Equals("InbandEventStream"))
                {
                    inbandEventStreams.Add(ParseDescriptor(childElement));
                }
                else if (childElement.Name.Equals("SupplementalProperty"))
                {
                    supplementalProperties.Add(ParseDescriptor(childElement));
                }
            }

            var format = BuildFormat(id, mimeType, width, height, frameRate, audioChannels, audioSamplingRate,
                bandwidth, adaptationSetLanguage, adaptationSetRoleDescriptors, adaptationSetAccessibilityDescriptors,
                codecs, supplementalProperties);
            segmentBase = segmentBase ?? new SingleSegmentBase();
            return new RepresentationInfo(format, baseUrl, segmentBase, drmSchemeType, contentProtections,
                inbandEventStreams, null);
        }

        private Representation BuildRepresentation(RepresentationInfo representationInfo, string label,
            string extraDrmSchemeType, List<ContentProtection> extraContentProtections,
            List<Descriptor> extraInbandEventStreams)
        {
            var format = representationInfo.Format;
            if (label != null)
                format = format.CopyWithLabel(label);
            var drmSchemeType = representationInfo.DrmSchemeType ?? extraDrmSchemeType;
            var contentProtections = representationInfo.ContentProtections;
            foreach (var extraContentProtection in extraContentProtections)
                contentProtections.Add(extraContentProtection);
            // TODO: Handle ContentProtections
            var inbandEventStreams = representationInfo.InbandEventStreams;
            foreach (var inbandEventStream in extraInbandEventStreams)
                inbandEventStreams.Add(inbandEventStream);
            return Representation.NewInstance(representationInfo.RevisionId,
                format,
                representationInfo.BaseUrl,
                representationInfo.SegmentBase,
                inbandEventStreams);
        }

        private SegmentList ParseSegmentList(XmlElement element, SegmentList parent, TimeSpan? periodDuration)
        {
            var timescale = ParseLong(element, "timescale", parent?.TimeScale ?? 1).Value;
            var presentationTimeOffset =
                ParseLong(element, "presentationTimeOffset", parent?.PresentationTimeOffset ?? 0).Value;
            var duration = ParseLong(element, "duration", parent?.Duration);
            var startNumber = ParseLong(element, "startNumber", parent?.StartNumber ?? 1).Value;

            RangedUri initialization = null;
            SegmentTimeline timeline = null;
            IList<RangedUri> segments = null;

            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement))
                    continue;
                var childElement = node as XmlElement;
                if (childElement.Name.Equals("Initialization"))
                {
                    initialization = ParseInitialization(childElement);
                }
                else if (childElement.Name.Equals("SegmentTimeline"))
                {
                    timeline = ParseSegmentTimeline(childElement, timescale, periodDuration);
                }
                else if (childElement.Name.Equals("SegmentURL"))
                {
                    if (segments == null)
                        segments = new List<RangedUri>();
                    segments.Add(ParseSegmentUrl(childElement));
                }
            }

            if (parent != null)
            {
                initialization = initialization ?? parent.Initialization;
                timeline = timeline ?? parent.SegmentTimeline;
                segments = segments ?? parent.MediaSegments;
            }

            return new SegmentList(initialization, timescale, presentationTimeOffset, startNumber, duration, timeline,
                segments);
        }

        private SegmentTemplate ParseSegmentTemplate(XmlElement element, SegmentTemplate parent,
            List<Descriptor> adaptationSetSupplementalProperties, TimeSpan? periodDuration)
        {
            var timescale = ParseLong(element, "timescale", parent?.TimeScale ?? 1).Value;
            var presentationTimeOffset =
                ParseLong(element, "presentationTimeOffset", parent?.PresentationTimeOffset ?? 0).Value;
            var duration = ParseLong(element, "duration", parent?.Duration);
            var startNumber = ParseLong(element, "startNumber", parent?.StartNumber ?? 1).Value;
            var endNumber = ParseLastSegmentNumberSupplementalProperty(adaptationSetSupplementalProperties);

            var mediaTemplate = ParseUrlTemplate(element, "media", parent?.MediaTemplate);
            var initializationTemplate = ParseUrlTemplate(element, "initialization", parent?.InitializationTemplate);

            RangedUri initialization = null;
            SegmentTimeline timeline = null;

            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement))
                    continue;
                var childElement = node as XmlElement;
                if (childElement.Name.Equals("Initialization"))
                    initialization = ParseInitialization(childElement);
                else if (childElement.Name.Equals("SegmentTimeline"))
                    timeline = ParseSegmentTimeline(childElement, timescale, periodDuration);
            }

            if (parent != null)
            {
                initialization = initialization ?? parent.Initialization;
                timeline = timeline ?? parent.SegmentTimeline;
            }

            return new SegmentTemplate(initialization, timescale, presentationTimeOffset, startNumber, duration,
                timeline, initializationTemplate, mediaTemplate, endNumber);
        }

        private SegmentBase ParseSegmentBase(XmlElement element, SingleSegmentBase parent)
        {
            var timescale = ParseLong(element, "timescale", parent?.TimeScale ?? 1).Value;
            var presentationTimeOffset = ParseLong(element, "presentationTimeOffset",
                parent != null ? parent.PresentationTimeOffset : 0).Value;
            var indexStart = parent?.IndexStart ?? 0;
            var indexLength = parent?.IndexLength ?? 0;
            var indexRangeText = element.GetAttribute("indexRange");
            if (indexRangeText != null)
            {
                var indexRange = indexRangeText.Split('-');
                indexStart = Convert.ToInt64(indexRange[0]);
                indexLength = Convert.ToInt64(indexRange[1]) - indexStart + 1;
            }

            var initialization = parent?.Initialization;
            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement))
                    continue;
                var childElement = node as XmlElement;
                if (childElement.Name.Equals("Initialization"))
                    initialization = ParseInitialization(childElement);
            }

            return new SingleSegmentBase(initialization, timescale, presentationTimeOffset, indexStart, indexLength);
        }

        private SegmentTimeline ParseSegmentTimeline(XmlElement element, long timescale, TimeSpan? periodDuration)
        {
            var segmentTimeline = new SegmentTimeline();
            long startTime = 0;
            long elementDuration = 0;
            var elementRepeatCount = 0;
            var havePreviousTimelineElement = false;

            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement))
                    continue;
                var childElement = node as XmlElement;
                if (childElement.Name.Equals("S"))
                {
                    var newStartTime = ParseLong(element, "t");
                    if (havePreviousTimelineElement)
                    {
                        startTime = AddSegmentTimelineElementsToList(segmentTimeline, startTime, elementDuration,
                            elementRepeatCount, newStartTime);
                    }

                    if (newStartTime != null)
                        startTime = newStartTime.Value;

                    elementDuration = ParseLong(childElement, "d", 0).Value;
                    elementRepeatCount = ParseInt(childElement, "r", 0).Value;
                    havePreviousTimelineElement = true;
                }
            }

            if (havePreviousTimelineElement)
            {
                long? endTime = null;
                if (periodDuration.HasValue)
                    endTime = (long)periodDuration.Value.TotalSeconds * timescale;
                AddSegmentTimelineElementsToList(segmentTimeline, startTime, elementDuration, elementRepeatCount,
                    endTime);
            }

            return segmentTimeline;
        }

        private long AddSegmentTimelineElementsToList(SegmentTimeline segmentTimeline, long startTime,
            long elementDuration, int elementRepeatCount, long? endTime)
        {
            var count = elementRepeatCount >= 0
                ? 1 + elementRepeatCount
                : (int)((endTime.Value - startTime + elementDuration - 1) / elementDuration);
            for (var i = 0; i < count; i++)
            {
                segmentTimeline.Add(new SegmentTimeline.Element(startTime, elementDuration));
                startTime += elementDuration;
            }

            return startTime;
        }

        private (string, ContentProtection) ParseContentProtection(XmlElement element)
        {
            // TODO: Implement
            return (null, null);
        }

        private int? ParseAudioChannelConfiguration(XmlElement element)
        {
            var schemeIdUri = ParseString(element, "schemeIdUri");
            var audioChannels = "urn:mpeg:dash:23003:3:audio_channel_configuration:2011".Equals(schemeIdUri)
                ? ParseInt(element, "value")
                : "tag:dolby.com,2014:dash:audio_channel_configuration:2011".Equals(schemeIdUri) ||
                  "urn:dolby:dash:audio_channel_configuration:2011".Equals(schemeIdUri)
                    ? ParseDolbyChannelConfiguration(element)
                    : null;
            return audioChannels;
        }

        private int? ParseDolbyChannelConfiguration(XmlElement element)
        {
            var value = element.GetAttribute("value").ToLowerInvariant();
            if (string.IsNullOrEmpty(value))
                return null;
            switch (value)
            {
                case "4000":
                    return 1;
                case "a000":
                    return 2;
                case "f801":
                    return 6;
                case "fa01":
                    return 8;
                default:
                    return null;
            }
        }

        private UrlTemplate ParseUrlTemplate(XmlElement element, string name, UrlTemplate defaultTemplate)
        {
            var valueString = element.GetAttribute(name);
            if (!string.IsNullOrEmpty(valueString))
                return new UrlTemplate(valueString);
            return defaultTemplate;
        }

        private Format BuildFormat(string id, string containerMimeType, int? width, int? height, float? frameRate,
            int? audioChannels, int? audioSamplingRate, int? bitrate, string language,
            List<Descriptor> roleDescriptors, List<Descriptor> accessibilityDescriptors, string codecs,
            List<Descriptor> supplementalProperties)
        {
            var sampleMimeType = GetSampleMimeType(containerMimeType, codecs);
            var selectionFlags = ParseSelectionFlagsFromRoleDescriptors(roleDescriptors);
            var roleFlags = ParseRoleFlagsFromRoleDescriptors(roleDescriptors);
            roleFlags |= ParseRoleFlagsFromAccessibilityDescriptors(accessibilityDescriptors);
            if (sampleMimeType != null)
            {
                if (MimeType.AudioEac3.Equals(sampleMimeType))
                    sampleMimeType = ParseEac3SupplementalProperties(supplementalProperties);
                if (MimeType.IsVideo(sampleMimeType))
                {
                    return Format.CreateVideoContainerFormat(id, null, containerMimeType, sampleMimeType, codecs,
                        bitrate, width, height, frameRate, selectionFlags, roleFlags);
                }

                if (MimeType.IsAudio(sampleMimeType))
                {
                    return Format.CreateAudioContainerFormat(id, null, containerMimeType, sampleMimeType, codecs,
                        bitrate, audioChannels, audioSamplingRate, selectionFlags, roleFlags, language);
                }

                if (MimeTypeIsRawText(sampleMimeType))
                {
                    int? accessibilityChannel = null;
                    if (MimeType.ApplicationCea608.Equals(sampleMimeType))
                        accessibilityChannel = ParseCea608AccessibilityChannel(accessibilityDescriptors);
                    else if (MimeType.ApplicationCea708.Equals(sampleMimeType))
                        accessibilityChannel = ParseCea708AccessibilityChannel(accessibilityDescriptors);
                    return Format.CreateTextContainerFormat(id, null, containerMimeType, sampleMimeType, codecs,
                        bitrate, selectionFlags, roleFlags, language, accessibilityChannel);
                }
            }

            return Format.CreateContainerFormat(id, null, containerMimeType, sampleMimeType, codecs, bitrate,
                selectionFlags, roleFlags, language);
        }

        private RoleFlags ParseRoleFlagsFromRoleDescriptors(List<Descriptor> roleDescriptors)
        {
            RoleFlags result = 0;
            foreach (var descriptor in roleDescriptors)
            {
                if ("urn:mpeg:dash:role:2011".Equals(descriptor.SchemeIdUri,
                    StringComparison.InvariantCultureIgnoreCase))
                    result |= ParseDashRoleSchemeValue(descriptor.Value);
            }

            return result;
        }

        private RoleFlags ParseRoleFlagsFromAccessibilityDescriptors(List<Descriptor> accessibilityDescriptors)
        {
            RoleFlags result = 0;
            foreach (var descriptor in accessibilityDescriptors)
            {
                if ("urn:mpeg:dash:role:2011".Equals(descriptor.SchemeIdUri,
                    StringComparison.InvariantCultureIgnoreCase))
                    result |= ParseDashRoleSchemeValue(descriptor.Value);
                else if ("urn:tva:metadata:cs:AudioPurposeCS:2007".Equals(descriptor.SchemeIdUri,
                    StringComparison.InvariantCultureIgnoreCase))
                    result |= ParseTvaAudioPurposeCsValue(descriptor.Value);
            }

            return result;
        }

        private RoleFlags ParseDashRoleSchemeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;
            switch (value)
            {
                case "main":
                    return RoleFlags.Main;
                case "alternate":
                    return RoleFlags.Alternate;
                case "supplementary":
                    return RoleFlags.Supplementary;
                case "commentary":
                    return RoleFlags.Commentary;
                case "dub":
                    return RoleFlags.Dub;
                case "emergency":
                    return RoleFlags.Emergency;
                case "caption":
                    return RoleFlags.Caption;
                case "subtitle":
                    return RoleFlags.Subtitle;
                case "sign":
                    return RoleFlags.Sign;
                case "description":
                    return RoleFlags.DescribesVideo;
                case "enhanced-audio-intelligibility":
                    return RoleFlags.EnhancedDialogIntelligibility;
                default:
                    return 0;
            }
        }

        private RoleFlags ParseTvaAudioPurposeCsValue(string value)
        {
            if (value == null)
                return 0;
            switch (value)
            {
                case "1":
                    return RoleFlags.DescribesVideo;
                case "2":
                    return RoleFlags.EnhancedDialogIntelligibility;
                case "3":
                    return RoleFlags.Supplementary;
                case "4":
                    return RoleFlags.Commentary;
                case "6":
                    return RoleFlags.Main;
                default:
                    return 0;
            }
        }

        private int? ParseCea608AccessibilityChannel(List<Descriptor> accessibilityDescriptors)
        {
            foreach (var descriptor in accessibilityDescriptors)
            {
                if ("urn:scte:dash:cc:cea-608:2015".Equals(descriptor.SchemeIdUri) && descriptor.Value != null)
                {
                    var accessibilityValueMatcher = _cea608AccessibilityRegex.Match(descriptor.Value);
                    if (accessibilityValueMatcher.Success)
                        return Convert.ToInt32(accessibilityValueMatcher.Groups[1]);
                }
            }

            return null;
        }

        private int? ParseCea708AccessibilityChannel(List<Descriptor> accessibilityDescriptors)
        {
            foreach (var descriptor in accessibilityDescriptors)
            {
                if ("urn:scte:dash:cc:cea-708:2015".Equals(descriptor.SchemeIdUri) && descriptor.Value != null)
                {
                    var accessibilityValueMatcher = _cea708AccessibilityRegex.Match(descriptor.Value);
                    if (accessibilityValueMatcher.Success)
                        return Convert.ToInt32(accessibilityValueMatcher.Groups[1]);
                }
            }

            return null;
        }

        private string ParseEac3SupplementalProperties(List<Descriptor> supplementalProperties)
        {
            foreach (var descriptor in supplementalProperties)
            {
                var schemeIdUri = descriptor.SchemeIdUri;
                if (("tag:dolby.com,2018:dash:EC3_ExtensionType:2018".Equals(schemeIdUri) &&
                    "JOC".Equals(descriptor.Value))
                    || ("tag:dolby.com,2014:dash:DolbyDigitalPlusExtensionType:2014".Equals(schemeIdUri) &&
                    "ec+3".Equals(descriptor.Value)))
                    return MimeType.AudioEac3Joc;
            }

            return MimeType.AudioEac3;
        }

        private string GetSampleMimeType(string containerMimeType, string codecs)
        {
            if (MimeType.IsAudio(containerMimeType)) return MimeType.GetAudioMediaMimeType(codecs);

            if (MimeType.IsVideo(containerMimeType)) return MimeType.GetVideoMediaMimeType(codecs);

            if (MimeTypeIsRawText(containerMimeType)) return containerMimeType;

            if (MimeType.ApplicationMp4.Equals(containerMimeType))
            {
                if (codecs != null)
                {
                    if (codecs.StartsWith("stpp"))
                        return MimeType.ApplicationTtml;
                    if (codecs.StartsWith("wvtt"))
                        return MimeType.ApplicationMp4Vtt;
                }
            }
            else if (MimeType.ApplicationRawCc.Equals(containerMimeType))
            {
                if (codecs != null)
                {
                    if (codecs.Contains("cea708"))
                        return MimeType.ApplicationCea708;
                    if (codecs.Contains("eia608") || codecs.Contains("cea608"))
                        return MimeType.ApplicationCea608;
                }
            }

            return null;
        }

        private SelectionFlags ParseSelectionFlagsFromRoleDescriptors(List<Descriptor> roleDescriptors)
        {
            foreach (var descriptor in roleDescriptors)
            {
                if ("urn:mpeg:dash:role:2011".Equals(descriptor.SchemeIdUri,
                        StringComparison.InvariantCultureIgnoreCase)
                    && "main".Equals(descriptor.Value))
                    return SelectionFlags.Default;
            }

            return SelectionFlags.Unknown;
        }

        private static bool MimeTypeIsRawText(string mimeType)
        {
            return MimeType.IsText(mimeType)
                   || MimeType.ApplicationTtml.Equals(mimeType)
                   || MimeType.ApplicationMp4Vtt.Equals(mimeType)
                   || MimeType.ApplicationCea708.Equals(mimeType)
                   || MimeType.ApplicationCea608.Equals(mimeType);
        }

        private RangedUri ParseInitialization(XmlElement element)
        {
            return ParseRangedUrl(element, "sourceURL", "range");
        }

        private RangedUri ParseSegmentUrl(XmlElement element)
        {
            return ParseRangedUrl(element, "media", "mediaRange");
        }

        private RangedUri ParseRangedUrl(XmlElement element, string urlAttribute, string rangeAttribute)
        {
            var urlText = element.GetAttribute(urlAttribute);
            long rangeStart = 0;
            long? rangeLength = null;
            var rangeText = element.GetAttribute(rangeAttribute);
            if (!string.IsNullOrEmpty(rangeText))
            {
                var range = rangeText.Split('-');
                rangeStart = Convert.ToInt64(range[0]);
                if (range.Length == 2)
                    rangeLength = Convert.ToInt64(range[1]) - rangeStart + 1;
            }

            return new RangedUri(urlText, rangeStart, rangeLength);
        }

        private string ParseBaseUrl(XmlElement element, string baseUrl)
        {
            return new Uri(new Uri(baseUrl), element.InnerText).ToString();
        }

        private string ParseLabel(XmlElement element)
        {
            return ParseString(element, "Label", "");
        }

        private ContentType ParseContentType(XmlElement element)
        {
            var contentType = element.GetAttribute("contentType");
            return string.IsNullOrEmpty(contentType) ? ContentType.Unknown
                : MimeType.BaseTypeAudio.Equals(contentType) ? ContentType.Audio
                : MimeType.BaseTypeVideo.Equals(contentType) ? ContentType.Video
                : MimeType.BaseTypeText.Equals(contentType) ? ContentType.Text
                : ContentType.Unknown;
        }

        private ProgramInformation ParseProgramInformation(XmlElement element)
        {
            string title = null;
            string source = null;
            string copyright = null;
            var moreInformationUrl = ParseString(element, "moreInformationURL");
            var lang = ParseString(element, "lang");
            foreach (XmlNode node in element.ChildNodes)
            {
                if (!(node is XmlElement))
                    continue;
                var childElement = node as XmlElement;
                if (childElement.Name.Equals("Title"))
                    title = childElement.InnerText;
                else if (childElement.Name.Equals("Source"))
                    source = childElement.InnerText;
                else if (childElement.Name.Equals("Copyright")) copyright = childElement.InnerText;
            }

            return new ProgramInformation(title, source, copyright, lang, moreInformationUrl);
        }

        private Descriptor ParseDescriptor(XmlElement element)
        {
            var schemeIdUri = ParseString(element, "schemeIdUri", "");
            var value = ParseString(element, "value");
            var id = ParseString(element, "id");
            return new Descriptor(schemeIdUri, value, id);
        }

        private ContentType GetContentType(Format format)
        {
            var sampleMimeType = format.SampleMimeType;
            if (string.IsNullOrEmpty(sampleMimeType))
                return ContentType.Unknown;
            if (MimeType.IsAudio(sampleMimeType))
                return ContentType.Audio;
            if (MimeType.IsVideo(sampleMimeType))
                return ContentType.Video;
            if (MimeTypeIsRawText(sampleMimeType))
                return ContentType.Text;
            return ContentType.Unknown;
        }

        private long? ParseLastSegmentNumberSupplementalProperty(List<Descriptor> supplementalProperties)
        {
            foreach (var descriptor in supplementalProperties.Where(descriptor =>
                "http://dashif.org/guidelines/last-segment-number".Equals(descriptor.SchemeIdUri,
                    StringComparison.InvariantCultureIgnoreCase)))
                return Convert.ToInt64(descriptor.Value);

            return null;
        }

        private UtcTiming ParseUtcTiming(XmlElement element)
        {
            var schemeIdUri = element.GetAttribute("schemeIdUri");
            var value = element.GetAttribute("value");
            return new UtcTiming(schemeIdUri, value);
        }

        private float? ParseFrameRate(XmlElement element, float? defaultValue = null)
        {
            var frameRate = defaultValue;
            var frameRateAttribute = element.GetAttribute("frameRate");
            if (!string.IsNullOrEmpty(frameRateAttribute))
            {
                var match = _frameRateRegex.Match(frameRateAttribute);
                if (match.Success)
                {
                    var numerator = Convert.ToInt32(match.Groups[1].Value);
                    var denominatorString = match.Groups[2].Value;
                    if (!string.IsNullOrEmpty(denominatorString))
                        frameRate = (float)numerator / Convert.ToInt32(denominatorString);
                    else
                        frameRate = numerator;
                }
            }

            return frameRate;
        }

        private static TimeSpan? ParseDuration(XmlElement element, string attributeName, TimeSpan? defaultValue)
        {
            if (!element.HasAttribute(attributeName))
                return defaultValue;

            var attribute = element.GetAttribute(attributeName);
            return XmlConvert.ToTimeSpan(attribute);
        }

        private static DateTime? ParseDateTime(XmlElement element, string attributeName, DateTime? defaultValue)
        {
            if (!element.HasAttribute(attributeName))
                return defaultValue;

            var attribute = element.GetAttribute(attributeName);
            return XmlConvert.ToDateTime(attribute, XmlDateTimeSerializationMode.RoundtripKind);
        }

        private static string ParseString(XmlElement element, string attributeName, string defaultValue = null)
        {
            return !element.HasAttribute(attributeName) ? defaultValue : element.GetAttribute(attributeName);
        }

        private static int? ParseInt(XmlElement element, string attributeName, int? defaultValue = null)
        {
            return !element.HasAttribute(attributeName)
                ? defaultValue
                : Convert.ToInt32(element.GetAttribute(attributeName));
        }

        private static long? ParseLong(XmlElement element, string attributeName, long? defaultValue = null)
        {
            return !element.HasAttribute(attributeName)
                ? defaultValue
                : Convert.ToInt64(element.GetAttribute(attributeName));
        }

        private class RepresentationInfo
        {
            public RepresentationInfo(Format format, string baseUrl, SegmentBase segmentBase, string drmSchemeType,
                IList<ContentProtection> contentProtections, IList<Descriptor> inbandEventStreams, long? revisionId)
            {
                Format = format;
                BaseUrl = baseUrl;
                SegmentBase = segmentBase;
                DrmSchemeType = drmSchemeType;
                ContentProtections = contentProtections;
                InbandEventStreams = inbandEventStreams;
                RevisionId = revisionId;
            }

            public Format Format { get; }
            public string BaseUrl { get; }
            public SegmentBase SegmentBase { get; }
            public string DrmSchemeType { get; }
            public IList<ContentProtection> ContentProtections { get; }
            public IList<Descriptor> InbandEventStreams { get; }
            public long? RevisionId { get; }
        }
    }
}
