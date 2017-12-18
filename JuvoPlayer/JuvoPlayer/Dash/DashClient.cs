using System;
using JuvoPlayer.Common;
using JuvoPlayer.Dash.MpdParser;
using System.Net.Http;
using System.Net;

namespace JuvoPlayer.Dash
{
    class DashClient : IDashClient
    {
        static string Tag = "JuvoPlayer";
        private ISharedBuffer _sharedBuffer;
        private DashManifest _manifest;
        private int currentPeriod;

        public DashClient(
            DashManifest dashManifest,
            ISharedBuffer sharedBuffer)
        {
            _manifest = dashManifest ??
                throw new ArgumentNullException(
                    nameof(dashManifest),
                    "dashManifest cannot be null");
            _sharedBuffer = sharedBuffer;
            if (_manifest.Document.Periods.Length == 0)
            {
                Tizen.Log.Error(Tag, "No periods present in MPD file.");
            }
            foreach (var p in _manifest.Document.Periods)
            {
                Tizen.Log.Info(Tag, p.ToString());
                try
                {
                    var m = Find(p, "eng", MediaType.Video) ??
                        Find(p, "und", MediaType.Video);
                    Tizen.Log.Info(Tag, "Media: " + m);
                    string segmentUrl = null;
                    foreach (var r in m.Representations)
                    {
                        Tizen.Log.Info(Tag, r.ToString());
                        var segments = r.Segments;
                        Tizen.Log.Info(
                            Tag,
                            "Segment: " + segments.MediaSegmentAtPos(0).Url);
                        segmentUrl =
                            segments.MediaSegmentAtPos(0).Url.ToString();
                        break; // only first representation
                    }
                    DownloadSegment(segmentUrl);
                    Tizen.Log.Info(Tag, "data: " + sharedBuffer.ReadData(1024));
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

        public bool UpdateManifest(DashManifest newManifest)
        {
            if (newManifest == null)
                return false;
            _manifest = newManifest;
            return true;
        }

        private static Media Find(
            Period p,
            string language,
            MediaType type,
            MediaRole role = MediaRole.Main)
        {
            Media missingRole = null;
            foreach (var set in p.Sets)
            {
                if (set.Type.Value == type && set.Lang == language)
                {
                    if (set.HasRole(role))
                    {
                        return set;
                    }
                    if (set.Roles.Length == 0)
                    {
                        missingRole = set;

                    }
                }
            }
            return missingRole;

        }
        private bool DownloadSegment(
            string url)
        {
            try
            {
                var client = new HttpClient();
                var response = client.GetAsync(
                    url,
                    HttpCompletionOption.ResponseHeadersRead).Result;
                response.EnsureSuccessStatusCode();
                _sharedBuffer.WriteData(
                    response.Content.ReadAsByteArrayAsync().Result);
                return true;
            }
            catch (Exception ex)
            {
                Tizen.Log.Error(
                    Tag,
                    "Cannot download segment file. Error: " + ex.Message);
                return false;
            }
        }
    }

    public class DashTest
    {
        public static void Print(string val)
        {
            Tizen.Log.Info("JuvoPlayer", val);
        }
    }

    public class WebClientEx : WebClient
    {
        private readonly long _from;
        private readonly long _to;

        public WebClientEx(long from, long to)
        {
            _from = from;
            _to = to;
        }

        public UInt64 GetBytes(Uri address)
        {
            OpenRead(address.ToString());
            return Convert.ToUInt64(ResponseHeaders["Content-Length"]);

        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = (HttpWebRequest)base.GetWebRequest(address);
            request.AddRange(_from, _to);
            return request;
        }
    }
}
