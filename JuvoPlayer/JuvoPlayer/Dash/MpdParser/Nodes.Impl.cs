using System;
using MpdParser.XmlUtils;
using System.Collections.Generic;
using MpdParser.Node.Dynamic;
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
            foreach (BaseURL url in urls) {
                if (!string.IsNullOrEmpty(url.BaseUrlValue))
                    return new URI(url.BaseUrlValue);
            }
            return null;
        }
    };

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
                result[i] = "";
            }

            foreach (string key in keys_.Keys)
            {
                string[] key_fmt = key.Split('%');
                string value;
                if (args.ContainsKey(key_fmt[0]))
                {
                    value = args[key_fmt[0]].ToString();

                    if (key_fmt.Length > 1)
                    {
                        throw new UnexpectedTemplateArgumentException(
                            "Format of a template variable ignored: " + key
                            );
                    }
                }
                else
                {
                    value = "$" + key_fmt[0] + "$";
                }

                List<int> refs = keys_[key];
                foreach (int i in refs)
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

        public string Get(uint? bandwidth, string reprId, uint number, uint time)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>() {
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
                case SegmentType.Template: return CreateTemplateRepresentationStream();
            }
            return null;
        }

        private IRepresentationStream CreateBaseRepresentationStream()
        {
            Dynamic.SegmentBase seg = SegmentBase();
            URL[] init = seg.Initializations;
            URL init_url = GetFirst(init);
            if (init_url == null)
                return null;

            URI media_uri = CalcURL();
            URI init_uri = media_uri;

            // If the init_url.SourceUrl is present,
            // it is relative to AdpativeSet, not Representation.
            // Representation's BaseURI is for media only.
            if (init_url.SourceURL != null)
            {
                init_uri = AdaptationSet.CalcURL()?.With(init_url.SourceURL) ?? media_uri;
            }

            return new Dynamic.BaseRepresentationStream(
                new Segment(init_uri.Uri, init_url.Range),
                new Segment(media_uri.Uri, null)
                );
        }

        private TimelineItem[] FromDuration(uint startNumber, Dynamic.SegmentTemplate seg)
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

            return Timeline.FromDuration(startNumber, start,
                duration.Value, segDuration.Value, seg.Timescale ?? 1);
        }

        private TimelineItem[] FromTimeline(uint startNumber, Dynamic.SegmentTemplate seg, SegmentTimeline segmentTimeline)
        {
            return Timeline.FromXml(startNumber, Period.Start ?? TimeSpan.Zero, seg.Timescale ?? 1, segmentTimeline.Ss);
        }

        private IRepresentationStream CreateTemplateRepresentationStream()
        {
            Dynamic.SegmentTemplate seg = SegmentTemplate();
            SegmentTimeline segTimeline = GetFirst(seg.SegmentTimelines);
            uint startNumber = seg.StartNumber ?? 1;

            TimelineItem[] timeline = null;
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

            return new Dynamic.TemplateRepresentationStream(CalcURL().Uri,
                seg.Initialization, seg.Media, Bandwidth, Id,
                seg.Timescale ?? 1, timeline);
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
        public URI CalcURL()
        {
            URI parent = Document.CalcURL();
            URI local = URI.FromBaseURLs(BaseURLs);

            return local == null ? parent : parent.With(local);
        }
    }

    public partial class DASH
    {
        public URI CalcURL()
        {
            return URI.FromBaseURLs(BaseURLs) ?? new URI();
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
