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
using System.Reflection;

namespace MpdParser.Node
{
    public class URI
    {
        public Uri Uri { get { return mInternal; } }
        private Uri mInternal;
        public URI()
        {
            // mInternal = new Uri("", UriKind.Relative);
        }

        public URI(string path)
        {
            mInternal = new Uri(path, UriKind.RelativeOrAbsolute);
        }

        public URI(Uri path)
        {
            mInternal = path;
        }

        public URI With(URI child)
        {
            if (mInternal == null)
                return child;
            if (child?.mInternal != null)
            {
                if (Uri.TryCreate(mInternal, child.mInternal, out Uri result))
                    return new URI(result);
            }
            return this;
        }

        public URI With(string child)
        {
            if (child == null)
                return null;
            return With(new Uri(child, UriKind.RelativeOrAbsolute));
        }

        public URI With(Uri child)
        {
            if (child == null)
                return null;
            if (!Uri.TryCreate(mInternal, child, out Uri result))
                return null;
            return new URI(result);
        }

        public static URI FromBaseURLs(BaseURL[] urls)
        {
            if (urls == null) return null;

            foreach (BaseURL url in urls)
            {
                if (!string.IsNullOrEmpty(url.BaseUrlValue))
                    return new URI(url.BaseUrlValue);
            }
            return null;
        }
    };

    public partial class Formatter
    {
        public Formatter(string key)
        {
            Positions = new List<int>();
            string[] key_fmt = key.Split('%');
            TrueKey = key_fmt[0];
            pad = 1;
            fill = '0';

            if (key_fmt.Length > 1 && !string.IsNullOrEmpty(key_fmt[1]))
            {
                string fmt = key_fmt[1];
                int ndx = 0;
                if (fmt[0] < '1' || fmt[0] > '9')
                {
                    fill = fmt[0];
                    ndx = 1;
                }
                pad = 0;
                while (fmt[ndx] >= '0' && fmt[ndx] <= '9')
                {
                    pad *= 10;
                    pad += fmt[ndx] - '0';
                    ++ndx;
                }
                if (pad < 1)
                    pad = 1;
            }
        }

        public string GetValue(object value)
        {
            if (value == null)
            {
                if (string.IsNullOrEmpty(TrueKey))
                    return "$"; // $$ escapes a dollar sign
                return "$" + TrueKey + "$"; // de
            }

            return value.ToString().PadLeft(pad, fill);
        }
    }

    public partial class Template
    {
        private string Get(Dictionary<string, object> args)
        {
            string[] result = new string[chunks_.Length];
            chunks_.CopyTo(result, 0);
            for (int i = 0; i < chunks_.Length; ++i)
            {
                if ((i % 2) == 0)
                    continue;
                result[i] = "$"; // assume empty
            }

            foreach (string key in keys_.Keys)
            {
                Formatter fmt = keys_[key];

                string value = fmt.GetValue(
                    args.TryGetValue(fmt.TrueKey, out object arg) ? arg : null
                    );

                foreach (int i in fmt.Positions)
                {
                    result[i] = value;
                }
            }
            return string.Join("", result);
        }

        public string Get(uint? bandwidth, string reprId)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            if (bandwidth != null)
                dict.Add("Bandwidth", bandwidth.Value);
            if (reprId != null)
                dict.Add("RepresentationID", reprId);
            return Get(dict);
        }

        public string Get(uint? bandwidth, string reprId, ulong number, ulong time)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>()
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
    }

    public partial class Representation
    {
        private static TimeSpan Scale(ulong offset, uint scale)
        {
            return TimeSpan.FromSeconds(((double)offset) / scale);
        }

        private static S GetFirst<S>(S[] list)
        {
            if ((list?.Length ?? 0) == 0)
                return default(S);
            return list[0];
        }
        private static T GetDynamic<T, S>(S[] repr, S[] set, S[] period)
        {
            S repr_ = GetFirst(repr);
            S set_ = GetFirst(set);
            S period_ = GetFirst(period);
            return (T)Activator.CreateInstance(typeof(T), new object[] { repr_, set_, period_ });
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
            SegmentType best = FindBestStream();
            switch (best)
            {
                case SegmentType.Base: return CreateBaseRepresentationStream();
                case SegmentType.List: return CreateListRepresentationStream();
                case SegmentType.Template: return CreateTemplateRepresentationStream();
            }
            // No "SegmentXyz" elements, but at least one BaseURL present
            // This setup could be in e.g. subtitles
            if (BaseURLs.Length > 0)
                return CreateBaseURLRepresentationStream();
            return null;
        }

        private IRepresentationStream CreateBaseRepresentationStream()
        {
            Dynamic.TimeRange periodRange = null;
            if (Period.Start != null && Period.Duration != null)
                periodRange = new Dynamic.TimeRange(Period.Start.Value, Period.Duration.Value);

            Dynamic.SegmentBase seg = SegmentBase();
            URL init_url = GetFirst(seg.Initializations);

            string index_range = seg.IndexRange;

            // Live content elements from Segment Base...
            var presentationTimeOffset = seg.PresentationTimeOffset ?? 0;
            TimeSpan availabilityTimeOffset = seg.AvailabilityTimeOffset.HasValue ?
                TimeSpan.FromSeconds(seg.AvailabilityTimeOffset.Value) : TimeSpan.MaxValue;


            URI media_uri = CalcURL();
            URI init_uri = null;

            // If the init_url.SourceUrl is present,
            // it is relative to AdaptiveSet, not Representation.
            // Representation's BaseURI is for media only.
            if (init_url?.SourceURL != null)
                init_uri = AdaptationSet.CalcURL()?.With(init_url.SourceURL);
            else if (init_url != null)
                init_uri = media_uri;

            if (init_uri == null)
            {
                if (media_uri == null)
                    return null;

                Dynamic.Segment media = new Dynamic.Segment(media_uri.Uri, null, periodRange);
                Dynamic.Segment index = index_range.Length != 0 ? new Dynamic.Segment(media_uri.Uri, index_range) : null;
                return new Dynamic.BaseRepresentationStream(null, media,
                    presentationTimeOffset, seg.TimeShiftBufferDepth,
                    availabilityTimeOffset, seg.AvailabilityTimeComplete,
                    index );
            }

            if (media_uri == null)
                return null;

            return new Dynamic.BaseRepresentationStream(
                new Dynamic.Segment(init_uri.Uri, init_url?.Range),
                new Dynamic.Segment(media_uri.Uri, null, periodRange),
                presentationTimeOffset, seg.TimeShiftBufferDepth,
                availabilityTimeOffset,seg.AvailabilityTimeComplete,
                index_range.Length !=0? new Dynamic.Segment(init_uri.Uri, index_range):null);
        }

        private IRepresentationStream CreateBaseURLRepresentationStream()
        {
            Dynamic.TimeRange periodRange = null;
            if (Period.Start != null && Period.Duration != null)
                periodRange = new Dynamic.TimeRange(Period.Start.Value, Period.Duration.Value);

            URI media_uri = AdaptationSet.CalcURL();
            foreach (BaseURL url in BaseURLs)
            {
                if (!string.IsNullOrEmpty(url.BaseUrlValue))
                    media_uri = media_uri.With(url.BaseUrlValue);
            }

            Dynamic.SegmentBase segBase = SegmentBase();
            var presentationTimeOffset = segBase.PresentationTimeOffset ?? 0;
            TimeSpan availabilityTimeOffset = segBase.AvailabilityTimeOffset.HasValue ?
                TimeSpan.FromSeconds(segBase.AvailabilityTimeOffset.Value) : TimeSpan.MaxValue;
            // Live content elements from Segment Base...
            // Can we have this changing dynamically and available at given time?
            // God forbids...

            return new Dynamic.BaseRepresentationStream(
                null,
                new Dynamic.Segment(media_uri.Uri, null, periodRange),
               presentationTimeOffset, segBase.TimeShiftBufferDepth,
                    availabilityTimeOffset, segBase.AvailabilityTimeComplete);
        }

        private IRepresentationStream CreateListRepresentationStream()
        {
            Dynamic.SegmentList seg = SegmentList();
            Dynamic.SegmentBase segBase = SegmentBase();

            URL init_url = GetFirst(seg.Initializations) ?? GetFirst(segBase.Initializations);
            URI init_uri = null;
            if (init_url?.SourceURL != null)
                init_uri = AdaptationSet.CalcURL()?.With(init_url.SourceURL);

            // If we still have no init URI, construct one based on BaseURL
            // for that purpose, index range from URL is required. Don't even try without it
            if (init_uri == null && init_url?.Range != null)
            {                
                if (this.BaseURL == null)
                    throw new ArgumentNullException("the BaseURL is null");
                init_uri = new URI(this.BaseURL);
            }

            Dynamic.Segment init = init_uri == null ? null : new Dynamic.Segment(init_uri.Uri, init_url?.Range);

            // Live content elements from Segment Base...
            var presentationTimeOffset = seg.PresentationTimeOffset ?? 0;
            TimeSpan availabilityTimeOffset = seg.AvailabilityTimeOffset.HasValue ?
                TimeSpan.FromSeconds(seg.AvailabilityTimeOffset.Value) : TimeSpan.MaxValue;

            Dynamic.ListItem[] items = Dynamic.ListItem.FromXml(
                seg.StartNumber ?? 1,
                Period.Start ?? new TimeSpan(0),
                seg.Timescale ?? 1,
                seg.Duration ?? 0,
                seg.SegmentURLs,
                this.BaseURL);
            return new Dynamic.ListRepresentationStream(CalcURL().Uri, init, seg.Timescale ?? 1,
                items,
                presentationTimeOffset, seg.TimeShiftBufferDepth,
                availabilityTimeOffset, seg.AvailabilityTimeComplete);
        }

        private Dynamic.TimelineItem[] FromDuration(uint startNumber, Dynamic.SegmentTemplate seg)
        {
            uint? segDuration = seg.Duration;
            if (segDuration == null)
                return null;

            TimeSpan start = Period.Start ?? TimeSpan.Zero;

            TimeSpan? duration = Period.Duration;
            if (duration == null)
            {
                TimeSpan mpdDuration = Document.MediaPresentationDuration ?? TimeSpan.Zero;
                if (mpdDuration <= start)
                    duration = TimeSpan.Zero;
                else
                    duration = mpdDuration - start;
            }

            if (duration.Equals(TimeSpan.Zero))
                return null;

            return Dynamic.Timeline.FromDuration(startNumber, start,
                duration.Value, segDuration.Value, seg.Timescale ?? 1);
        }

        private Dynamic.TimelineItem[] FromTimeline(uint startNumber, Dynamic.SegmentTemplate seg, SegmentTimeline segmentTimeline)
        {
            return Dynamic.Timeline.FromXml(startNumber, Period.Start ?? TimeSpan.Zero, Period.End, seg.Timescale ?? 1, segmentTimeline.Ss);
        }

        private IRepresentationStream CreateTemplateRepresentationStream()
        {
            Dynamic.SegmentTemplate seg = SegmentTemplate();
            SegmentTimeline segTimeline = GetFirst(seg.SegmentTimelines);
            uint startNumber = seg.StartNumber ?? 1;

            Dynamic.TimelineItem[] timeline = null;
            if (segTimeline == null)
            {
                timeline = FromDuration(startNumber, seg);
            }
            else
            {
                timeline = FromTimeline(startNumber, seg, segTimeline);
            }

            if (timeline == null)
                return null;

            // Live content elements from Segment Base...
            var presentationTimeOffset = seg.PresentationTimeOffset ?? 0;
            TimeSpan availabilityTimeOffset = seg.AvailabilityTimeOffset.HasValue ?
                TimeSpan.FromSeconds(seg.AvailabilityTimeOffset.Value) : TimeSpan.Zero;


            return new Dynamic.TemplateRepresentationStream(CalcURL().Uri,
                seg.Initialization, seg.Media, Bandwidth, Id,
                seg.Timescale ?? 1, timeline,
                presentationTimeOffset,seg.TimeShiftBufferDepth,
                availabilityTimeOffset,seg.AvailabilityTimeComplete,(segTimeline != null),
                seg.StartNumber??0,seg.Duration );
        }

        public URI CalcURL()
        {
            URI parent = AdaptationSet.CalcURL();
            URI local = URI.FromBaseURLs(BaseURLs);

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

            // With corrections to MPD XML Parser, we now get parsed elements in correct
            // places. So representation may not have segment templates (Adaptation set may)

            // REPRESENTATION:
            if (SegmentTemplates?.Length > 0)
                return SegmentType.Template;
            if (SegmentLists?.Length > 0)
                return SegmentType.List;
            if (SegmentBases?.Length > 0)
                return SegmentType.Base;

            // ADAPTATION SET:
            if (AdaptationSet?.SegmentTemplates?.Length > 0)
                return SegmentType.Template;
            if (AdaptationSet?.SegmentLists?.Length > 0)
                return SegmentType.List;
            if (AdaptationSet?.SegmentBases?.Length > 0)
                return SegmentType.Base;

            // PERIOD:
            if (Period?.SegmentTemplates?.Length > 0)
                return SegmentType.Template;
            if (Period?.SegmentLists?.Length > 0)
                return SegmentType.List;
            if (Period?.SegmentBases?.Length > 0)
                return SegmentType.Base;

            return SegmentType.None;
        }
    }

    public partial class AdaptationSet
    {
        private T InComponents<T>(Func<ContentComponent, T> pred)
        {
            if (ContentComponents == null)
                return default(T);
            foreach (ContentComponent comp in ContentComponents)
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

        public URI CalcURL()
        {
            URI parent = Period.CalcURL();
            URI local = URI.FromBaseURLs(BaseURLs);

            return local == null ? parent : parent.With(local);
        }
    }

    public partial class Period
    {
        internal TimeSpan? End { get; set; }
        public URI CalcURL()
        {
            URI parent = Document.CalcURL();
            URI local = URI.FromBaseURLs(BaseURLs);

            return local == null ? parent : parent.With(local);
        }
    }

    public partial class DASH
    {
        private URI manifestUrl;

        public DASH(string manifestUrl)
        {
            this.manifestUrl = string.IsNullOrEmpty(manifestUrl) ? null : new URI(manifestUrl);
        }

        public URI CalcURL()
        {
            URI local = URI.FromBaseURLs(BaseURLs);
            return manifestUrl?.With(local) ?? local ?? new URI();
        }

        public void PeriodFixup()
        {
            for (int i = 0; i < Periods.Length; ++i)
            {
                Period curr = Periods[i];
                if (curr.Start == null)
                {
                    if (i == 0)
                        curr.Start = TimeSpan.Zero;
                    else
                        curr.Start = Periods[i - 1].End;
                }

                if (curr.End == null)
                {
                    // for the last period, if @mediaPresentationDuration
                    // is present, it takes precedence
                    if (i == (Periods.Length - 1))
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
                    if (curr.End == null && curr.Start != null && curr.Duration != null)
                        curr.End = curr.Start.Value + curr.Duration.Value;
                }
            }
            for (int i = 0; i < Periods.Length; ++i)
            {
                Period curr = Periods[i];
                if (curr.Start == null) continue;

                if (curr.End != null)
                    curr.Duration = curr.End.Value - curr.Start.Value;

                if (curr.Duration == null)
                    curr.Duration = GuessFromRepresentations(curr);
            }
        }

        private TimeSpan? GuessFromRepresentations(Period curr)
        {
            TimeSpan? result = null;
            foreach (AdaptationSet set in curr.AdaptationSets)
            {
                TimeSpan? longest = GuessFromRepresentations(set);
                if (result == null)
                    result = longest;
                else if (longest != null && result.Value < longest.Value)
                    result = longest.Value;
            }
            return result;
        }

        private TimeSpan? GuessFromRepresentations(AdaptationSet set)
        {
            TimeSpan? result = null;
            foreach (Representation repr in set.Representations)
            {
                TimeSpan? longest = GuessFromRepresentations(repr);
                if (result == null)
                    result = longest;
                else if (longest != null && result.Value < longest.Value)
                    result = longest.Value;
            }
            return result;
        }

        private TimeSpan? GuessFromRepresentations(Representation repr)
        {
            return repr.SegmentsStream()?.Duration;
        }
    }

    public class Internal
    {
        public static void SetValue<T>(PropertyInfo prop, T result, object value)
        {
            prop.SetValue(result, value);
        }
    }
}
