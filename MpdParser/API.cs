using System;
using System.Collections.Generic;

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
                
                /* Looks like a repeated loop. Any reason?
                 * 
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
                */
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
            if (value == "video") return MediaType.Video;
            if (value == "audio") return MediaType.Audio;
            if (value == "application") return MediaType.Application;
            if (value == "text") return MediaType.Text;
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

    // a.k.a. AdaptationSet
    public class Media
    {
        public uint? Id { get; }
        public uint? Group { get; }
        public string Lang { get; }
        public ContentProtection[] ContentProtections { get; }
        public MimeType Type { get; }
        public Role[] Roles { get; }
        public Representation[] Representations { get; }

        public Media(Node.AdaptationSet set)
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
                Representations[i] = new Representation(set.Representations[i]);

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
            return result + " (length:" + (Longest()?.ToString() ?? "-") + ")";
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
            foreach (Representation r in Representations)
            {
                string cand = GuessFromMimeType(r.MimeType);
                if (cand == null) continue;
                if (guessed == null) guessed = cand;
                else if (guessed != cand) return null; // At least two different types
            }
            return guessed;
        }

        public TimeSpan? Longest()
        {
            TimeSpan? current = null;
            foreach (Representation repr in Representations)
            {
                TimeSpan? length = repr.Segments?.Duration;
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

    public class Period : IComparable
    {
        public TimeSpan? Start { get; internal set; }
        public TimeSpan? Duration { get; internal set; }
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
                (Start?.ToString() ?? "(-)") + " + " +
                (Duration?.ToString() ?? "(-)");
        }

        public TimeSpan? Longest()
        {
            TimeSpan? current = null;
            foreach (Media set in Sets)
            {
                TimeSpan? length = set.Longest();
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

            Periods = new Period[dash.Periods.Length];
            for (int i = 0; i < Periods.Length; ++i)
                Periods[i] = new Period(dash.Periods[i]);
            Array.Sort(Periods);
        }

        public static Document FromText(string manifestText, string manifestUrl)
        {
            Node.DASH dash = new Node.DASH(manifestUrl);
            System.IO.StringReader reader = new System.IO.StringReader(manifestText);
            Xml.Parser.Parse(reader, dash, "MPD");
            dash.PeriodFixup();
            return new Document(dash);
        }

        public override string ToString()
        {
            return
                "presentation:" + (MediaPresentationDuration?.ToString() ?? "(-)") +
                ", min:" + (MinBufferTime?.ToString() ?? "(-)");
        }
    }
}
