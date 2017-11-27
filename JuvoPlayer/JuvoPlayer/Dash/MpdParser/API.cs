using System;
using System.Reflection;

namespace MpdParser
{
    public class Representation
    {
        // REPR or ADAPTATION SET:
        public string Profile { get; }
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

        public Representation(Node.Representation repr)
        {
            Profile = repr.Profile ?? repr.AdaptationSet.Profile;
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

            if (NumChannels == null)
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
                if (channels == null)
                {
                    foreach (Node.Descriptor d in repr.AudioChannelConfigurations)
                    {
                        if (!string.IsNullOrEmpty(d.Value))
                        {
                            channels = d.Value;
                            break;
                        }
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
            if (Profile != null)
                result += " / " + Profile.ToString();
            result += " / " + (GetMimeType(MimeType, Codecs) ?? "-");

            string size = Size(Width, Height);
            string frate = FrameRate;

            if (size != null || frate != null)
            {
                result += " [" + (size ?? "");
                if (frate != null)
                    result += "@" + frate;
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

            return result;
        }
    }

    // a.k.a. AdaptationSet
    public class Media
    {
        public uint? Id { get; }
        public uint? Group { get; }
        public string Lang { get; }
        public string Type { get; }
        public string[] Roles { get; }
        public Representation[] Representations { get; }

        public Media(Node.AdaptationSet set)
        {
            Id = set.Id;
            Group = set.Group;
            Lang = set.GetLang() ?? "und";
            Type = set.GetContentType();

            Roles = new string[set.Roles.Length];
            for (int i = 0; i < Roles.Length; ++i)
                Roles[i] = set.Roles[i].Value;

            Representations = new Representation[set.Representations.Length];
            for (int i = 0; i < Representations.Length; ++i)
                Representations[i] = new Representation(set.Representations[i]);
        }

        public override string ToString()
        {
            string result =
                (Id?.ToString() ?? "-") + (Group?.ToString() ?? "") + " / " +
                Lang + " / " + Type;
            if (Roles.Length > 0)
                result += " [" + string.Join(", ", Roles) + "]";
            return result;
        }
    }

    public class Period
    {
        public TimeSpan? Start { get; }
        public TimeSpan? Duration { get; }
        public Media[] Sets { get; }

        public Period(Node.Period period)
        {
            Start = period.Start;
            Duration = period.Duration;
            Sets = new Media[period.AdaptationSets.Length];
            for (int i = 0; i < Sets.Length; ++i)
                Sets[i] = new Media(period.AdaptationSets[i]);
        }

        public override string ToString()
        {
            return
                (Start ?? TimeSpan.Zero).ToString() + " + " +
                (Duration ?? TimeSpan.Zero).ToString();
        }
    }

    public class Document
    {
        public string Title { get; }
        public TimeSpan? MediaPresentationDuration { get; }
        public TimeSpan? MinBufferTime { get; }
        public string[] Profiles { get; }
        public Period[] Periods { get; }

        private Document(Node.DASH dash)
        {
            MediaPresentationDuration = dash.MediaPresentationDuration;
            MinBufferTime = dash.MinBufferTime;
            Profiles = Xml.Conv.A2s(dash.Profiles);

            foreach (Node.ProgramInformation info in dash.ProgramInformations)
            {
                foreach(string title in info.Titles)
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

            Periods = new Period[dash.Periods.Length];
            for (int i = 0; i < Periods.Length; ++i)
                Periods[i] = new Period(dash.Periods[i]);
        }

        public static Document FromText(string manifest)
        {
            Node.DASH dash = new Node.DASH();
            System.IO.StringReader reader = new System.IO.StringReader(manifest);
            Xml.Parser.Parse(reader, dash, "MPD");
            return new Document(dash);
        }

        public override string ToString()
        {
            return
                "presentation:" + (MediaPresentationDuration ?? TimeSpan.Zero).ToString() +
                ", min:" + (MinBufferTime ?? TimeSpan.Zero).ToString();
        }
    }
}
