using System;
using JuvoPlayer.Common.Logging;
using NUnit.Framework;
using MpdParser;
using System.Net;


namespace JuvoPlayer.Tests
{
    [TestFixture]
    public class MpdParserDebug
    {
        private static Media Find(Period p, string language, MediaType type, MediaRole role = MediaRole.Main)
        {
            Media missingRole = null;
            foreach (var set in p.Sets)
            {
                if (set.Type.Value != type)
                {
                    continue;
                }

                if (language != "und" && set.Lang != language)
                {
                    continue;
                }

                if (set.HasRole(role))
                {
                    return set;
                }

                if (set.Roles.Length == 0)
                {
                    missingRole = set;
                }
            }
            return missingRole;
        }

        [Test]
        public void DEBUG_MpdParser()
        {
            LoggerBase CreateLogger(string channel, LogLevel level) => new DummyLogger(channel, level);
            LoggerManager.Configure(CreateLogger);
            //string url = "http://profficialsite.origin.mediaservices.windows.net/c51358ea-9a5e-4322-8951-897d640fdfd7/tearsofsteel_4k.ism/manifest(format=mpd-time-csf)";
            string url = "http://yt-dash-mse-test.commondatastorage.googleapis.com/media/car-20120827-manifest.mpd";
            WebClient wc = new WebClient();
            String xml;
            Document doc;

            try
            {
                xml = wc.DownloadString(url);

                doc = Document.FromText(xml, url);

                xml = xml;

                foreach (var period in doc.Periods)
                {
                   

                    Media audio = Find(period, "en", MediaType.Audio) ??
                            Find(period, "und", MediaType.Audio);

                    Media video = Find(period, "en", MediaType.Video) ??
                            Find(period, "und", MediaType.Video);

                    // TODO(p.galiszewsk): is it possible to have period without audio/video?
                    if (audio != null && video != null)
                    {
                        

                        // TODO(p.galiszewsk): unify time management
                        //if (period.Duration.HasValue)
                        //    ClipDurationChanged?.Invoke(period.Duration.Value);

                        return;
                    }
                }

            }
            catch (Exception ex)
            {
               
                return;
            }

            return;
 
        }
    }
}
