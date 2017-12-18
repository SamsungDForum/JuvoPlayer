using System;
using System.Collections.Generic;
using System.Linq;

namespace JuvoPlayer.Dash.MpdParser
{
    /// <summary>
    /// Main class representing MPD file.
    /// </summary>
    public class Document
    {
        public string Title { get; }
        public TimeSpan? MediaPresentationDuration { get; }
        public TimeSpan? MinBufferTime { get; }
        public string[] Profiles { get; }
        public Period[] Periods { get; }

        public Document(string manifestText, string manifestUrl)
        {
            var dash = new Node.Dash(manifestUrl);
            var reader = new System.IO.StringReader(manifestText);
            Xml.Parser.Parse(reader, dash, "MPD");

            dash.PeriodFixup();
            MediaPresentationDuration = dash.MediaPresentationDuration;
            MinBufferTime = dash.MinBufferTime;
            Profiles = Xml.TypeConverter.String2Array(dash.Profiles);

            foreach (var info in dash.ProgramInformations)
            {
                foreach(var title in info.Titles)
                {
                    if (string.IsNullOrEmpty(title)) continue;
                    Title = title;
                    break;
                }
                if (!string.IsNullOrEmpty(Title))
                    break;
            }

            Periods = new Period[dash.Periods.Length];
            for (var i = 0; i < Periods.Length; ++i)
                Periods[i] = new Period(dash.Periods[i]);
            Array.Sort(Periods);
        }

        public override string ToString()
        {
            return
                "presentation:" +
                (MediaPresentationDuration?.ToString() ?? "(-)") +
                ", min:" +
                (MinBufferTime?.ToString() ?? "(-)");
        }
    }
    /// <inheritdoc />
    /// <summary>
    /// Class represent each Period in MPD file 
    /// </summary>
    public class Period: IComparable
    {
        public TimeSpan ? Start { get; internal set; }
        public TimeSpan ? Duration { get; internal set; }
        public Media[] Sets { get; }

        public Period(Node.Period period)
        {
            Start = period.Start;
            Duration = period.Duration;
            Sets = new Media[period.AdaptationSets.Length];
            for (var i = 0; i < Sets.Length; ++i)
                Sets[i] = new Media(period.AdaptationSets[i]);
        }

        public override string ToString()
        {
            return
                (Start?.ToString() ?? "(-)") +
                " + " +
                (Duration?.ToString() ?? "(-)");
        }

        public TimeSpan? Longest()
        {
            TimeSpan? current = null;
            foreach (var set in Sets)
            {
                var length = set.Longest();
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
            var p = (Period)obj;
            if (Start == null)
            {
                if (p.Start == null)
                {
                    return 0;
                }
                return -1;
            }
            if (p.Start == null)
            {
                return 1;
            }
            return TimeSpan.Compare(
                (TimeSpan)Start,
                (TimeSpan)p.Start);
        }
    }

    /// <summary>
    /// Class represent AdaptationSet in MPD file
    /// </summary>
    public class Media
    {
        public uint? Id { get; }
        public uint? Group { get; }
        public string Lang { get; }
        public MimeType Type { get; }
        public Role[] Roles { get; }
        public Representation[] Representations { get; }

        public Media(Node.AdaptationSet set)
        {
            Id = set.Id;
            Group = set.Group;
            Lang = set.GetLang() ?? "und";

            Roles = new Role[set.Roles.Length];
            for (var i = 0; i < Roles.Length; ++i)
            {
                var r = set.Roles[i];
                Roles[i] = new Role(r.SchemeIdUri, r.Value);
            }

            Representations = new Representation[set.Representations.Length];
            for (var i = 0; i < Representations.Length; ++i)
                Representations[i] = new Representation(set.Representations[i]);

            Type = new MimeType(set.GetContentType() ??
                GuessFromMimeType(set.MimeType) ??
                GuessFromRepresentations());
        }

        public bool HasRole(MediaRole kind)
        {
            if (kind == MediaRole.Other)
                return false;

            return Roles.Any(r => r.Kind == kind);
        }

        public bool HasRole(string uri, string kind)
        {
            if (uri.Equals(Node.AdaptationSet.UrnRole) ||
                uri.Equals(Node.AdaptationSet.UrnRole2011))
                return HasRole(Role.ParseDashUrn(kind));

            return Roles.Where(r => r.Kind == MediaRole.Other).Any(r => r.Scheme.Equals(uri) && r.Value.Equals(kind));
        }

        public override string ToString()
        {
            string result = Lang + " / " + Type;
            if (Roles.Length > 0)
                result += " [" + string.Join(", ", (IEnumerable<Role>)Roles) + "]";
            return result + " (length:" + (Longest()?.ToString() ?? "-") + ")";
        }

        private static string GuessFromMimeType(string mimeType)
        {
            var mime = mimeType?.Split(new[] { '/' }, 2);
            return mime?.Length > 1 ? mime[0] : null;
        }

        private string GuessFromRepresentations()
        {
            string guessed = null;
            foreach (var r in Representations)
            {
                var cand = GuessFromMimeType(r.MimeType);
                if (cand == null) continue;
                if (guessed == null) guessed = cand;
                else if (guessed != cand) return null; // At least two different types
            }
            return guessed;
        }

        public TimeSpan? Longest()
        {
            TimeSpan? current = null;
            foreach (var repr in Representations)
            {
                var length = repr.Segments?.Duration;
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
    /// <summary>
    /// Class represents Representation in MPD file
    /// </summary>
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
                var channels = (from d in repr.AudioChannelConfigurations where !string.IsNullOrEmpty(d.Value) select d.Value).FirstOrDefault();
                if (channels == null)
                {
                    foreach (var d in repr.AudioChannelConfigurations)
                    {
                        if (string.IsNullOrEmpty(d.Value)) continue;
                        channels = d.Value;
                        break;
                    }
                }
                if (channels != null)
                    NumChannels = System.Xml.XmlConvert.ToUInt32(channels);
            }

            if (SampleRate != null) return;
            var sampling = repr.AudioSamplingRate ?? repr.AdaptationSet.AudioSamplingRate;
            if (sampling != null)
                SampleRate = System.Xml.XmlConvert.ToUInt32(sampling);
        }

        private static string GetMimeType(string type, string codecs)
        {
            if (string.IsNullOrEmpty(type))
                return null;

            if (string.IsNullOrEmpty(codecs))
                return type;

            return type + "; codecs=" + codecs;
        }

        private static string Size(uint? width, uint? height)
        {
            if (width == null && height == null)
                return null;
            return (width?.ToString() ?? "-") + "x" + (height?.ToString() ?? "-");
        }

        private static string Channels(uint? num)
        {
            if (num == null)
                return null;
            if (num.Value == 1)
                return "mono";
            if (num.Value == 2)
                return "stereo";
            return num.Value + "ch";
        }

        private static string Rate(ulong? rate)
        {
            if (rate == null)
                return null;
            var suffix = "Hz";
            var val = rate.Value * 10;

            string[] fixes = { "kHz", "MHz", "GHz" };

            foreach (var fix in fixes)
            {
                if (val < 10000)
                    break;
                val /= 1000;
                suffix = fix;
            }

            if (val % 10 == 0)
                return val / 10 + suffix;
            return val / 10 + "." + val % 10 + suffix;
        }

        public override string ToString()
        {
            var result = Bandwidth?.ToString() ?? "-";
            if (Profile != null)
                result += " / " + Profile;
            result += " / " + (GetMimeType(MimeType, Codecs) ?? "-");

            var size = Size(Width, Height);
            var frate = FrameRate;

            if (size != null || frate != null)
            {
                result += " [" + (size ?? "");
                if (frate != null)
                    result += "@" + frate;
                result += "]";
            }

            var rate = Rate(SampleRate);
            var ch = Channels(NumChannels);

            if (rate == null && ch == null)
                return result + " (duration:" + (Segments?.Duration?.ToString() ?? "-") + ")";
            result += " [" + (rate ?? "");
            if (rate != null && ch != null)
                result += " ";
            result += (ch ?? "") + "]";

            return result + " (duration:" + (Segments?.Duration?.ToString() ?? "-") + ")";
        }
    }

    /// <summary>
    /// Type of AdaptationSet roles
    /// </summary>
    public enum MediaRole {
        Main,
        Alternate,
        Captions,
        Subtitle,
        Supplementary,
        Commentary,
        Dub,
        Other
    };
    /// <summary>
    /// Class represents Role of AdaptationSet
    /// </summary>
    public class Role {
        public MediaRole Kind { get; }
        public string Scheme { get; }
        public string Value { get; }
        public Role(string scheme, string value) {
            Kind = MediaRole.Other;
            Scheme = scheme;
            Value = value;
            if (scheme.Equals(Node.AdaptationSet.UrnRole) ||
                scheme.Equals(Node.AdaptationSet.UrnRole2011)) {
                Kind = ParseDashUrn(value);
            }
        }
        public static MediaRole ParseDashUrn(string value)
        {
            switch (value)
            {
                case "main":
                    return MediaRole.Main;
                case "alternate":
                    return MediaRole.Alternate;
                case "captions":
                    return MediaRole.Captions;
                case "subtitle":
                    return MediaRole.Subtitle;
                case "supplementary":
                    return MediaRole.Supplementary;
                case "commentary":
                    return MediaRole.Commentary;
                case "dub":
                    return MediaRole.Dub;
                default:
                    return MediaRole.Other;
            }
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

    /// <summary>
    /// Class represent mime type of AdaptationSet
    /// </summary>
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
            switch (value)
            {
                case "video":
                    return MediaType.Video;
                case "audio":
                    return MediaType.Audio;
                case "application":
                    return MediaType.Application;
                case "text":
                    return MediaType.Text;
                default:
                    return MediaType.Other;
            }
        }
        public override string ToString()
        {
            return Key;
        }
    }



}
