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
using System.Net;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Tests.UnitTests;
using MpdParser;
using NUnit.Framework;

namespace JuvoPlayer.Tests.DebugCode
{
    [TestFixture]
    public class MpdParserDebug
    {
        private static AdaptationSet Find(Period p, string language, MediaType type, MediaRole role = MediaRole.Main)
        {
            AdaptationSet missingRole = null;
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
        [Ignore("Disabled temporarily")]
        [Category("Positive")]
        public async Task DEBUG_MpdParser()
        {
            LoggerBase CreateLogger(string channel, LogLevel level) => new DummyLogger(channel, level);
            LoggerManager.Configure(CreateLogger);
            //string url = "http://profficialsite.origin.mediaservices.windows.net/c51358ea-9a5e-4322-8951-897d640fdfd7/tearsofsteel_4k.ism/manifest(format=mpd-time-csf)";
            //string url = "http://dash.edgesuite.net/envivio/dashpr/clear/Manifest.mpd";
            string url = null;
            WebClient wc = new WebClient();
            String xml;
            Document doc;

            try
            {
                xml = wc.DownloadString(url);

                doc = await Document.FromText(xml, url);

                foreach (var period in doc.Periods)
                {

                    AdaptationSet audio = Find(period, "en", MediaType.Audio) ??
                            Find(period, "und", MediaType.Audio);

                    AdaptationSet video = Find(period, "en", MediaType.Video) ??
                            Find(period, "und", MediaType.Video);

                    if (audio != null && video != null)
                    {
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
