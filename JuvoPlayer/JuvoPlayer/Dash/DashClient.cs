using System;
using System.Collections.Generic;
using System.Text;
using JuvoPlayer.Common;
using MpdParser;

namespace JuvoPlayer.Dash
{
    class DashClient : IDashClient
    {
        static string Tag = "JuvoPlayer";
        private ISharedBuffer sharedBuffer;
        private DashManifest manifest;
        private int currentPeriod;

        public DashClient(
            DashManifest dashManifest,
            ISharedBuffer sharedBuffer)
        {
            manifest = dashManifest ??
                throw new ArgumentNullException(
                    "manifest",
                    "dashManifest cannot be null");
            this.sharedBuffer = sharedBuffer;
            if (manifest.Document.Periods.Length == 0)
            {
                Tizen.Log.Error(Tag, "No periods present in MPD file.");
            }
            else
            {
                currentPeriod = 0;
            }
            foreach (Period p in manifest.Document.Periods)
            {
                Tizen.Log.Info(Tag, p.ToString());
                try
                {
                    Media m = Find(p, "eng", MediaType.Video) ??
                        Find(p, "und", MediaType.Video);
                    Tizen.Log.Info(Tag, "Media: " + m.ToString());
                    foreach (Representation r in m.Representations)
                    {
                        Tizen.Log.Info(Tag, r.ToString());
                        var segments = r.Segments;
                        Tizen.Log.Info(Tag, "Segment: " + segments.MediaSegmentAtPos(0).Url);
                    }
                }
                catch (Exception ex)
                {
                    Tizen.Log.Info(Tag, ex.Message);
                }
            }

        }

        public void Seek(int position)
        {
            throw new NotImplementedException();
        }

        public void Start(ClipDefinition clip)
        {
            Tizen.Log.Info(Tag, "DashClient start.");
            //throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public bool UpdateManifest(DashManifest manifest)
        {
            if (manifest == null) {
                return false;
            }
            else
            {
                this.manifest = manifest;
                return true;
            }
        }

        private static Media Find(
            Period p,
            string language,
            MediaType type,
            MediaRole role = MediaRole.Main)
        {
            Media missingRole = null;
            foreach (Media set in p.Sets)
            {
                if (set.Type.Value == type && set.Lang == language)
                {
                    if (set.HasRole(role))
                    {
                        return set;
                    }
                    else if (set.Roles.Length == 0)
                    {
                        missingRole = set;

                    }
                }
            }
            return missingRole;

        }
    }

    public class DashTest
    {
        public static void Print(string val)
        {
            Tizen.Log.Info("JuvoPlayer", val);
        }
    }

}
