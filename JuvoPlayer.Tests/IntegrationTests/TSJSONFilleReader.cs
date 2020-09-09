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
using System.IO;
using System.Reflection;
using JuvoPlayer.Common;
using JuvoPlayer.Utils;
using NUnit.Framework;

namespace JuvoPlayer.Tests.IntegrationTests
{
    [TestFixture]
    class JSONFileReaderTests
    {
        private static string jsonText;

        [SetUp]
        public static void Init()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] names = assembly.GetManifestResourceNames();
            foreach (var name in names)
            {
                if (!name.Contains("videoclips.json")) continue;
                var stream = assembly.GetManifestResourceStream(name);
                var reader = new StreamReader(stream);
                jsonText = reader.ReadToEnd();
                reader.Close();
                return;
            }

            Assert.Fail("Cannot find required embedded resource - videoclips.json");
        }

        [TearDown]
        public static void Destroy()
        {
        }

        [Test]
        [Category("Negative")]
        public static void DeserializeJsonText_ThrowsNull()
        {
            Assert.Throws<ArgumentNullException>(() => JSONFileReader.DeserializeJsonText<List<ClipDefinition>>(null));
        }

        [Test]
        [Category("Negative")]
        public static void DeserializeJsonText_ThrowsEmpty()
        {
            Assert.Throws<ArgumentException>(() => JSONFileReader.DeserializeJsonText<List<ClipDefinition>>(""));
        }

        [Test]
        [Category("Negative")]
        public static void DeserializeJsonText_ThrowsInvalid()
        {
            Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => JSONFileReader.DeserializeJsonText<List<ClipDefinition>>("invalid"));
        }

        [Test]
        [Category("Positive")]
        public static void DeserializeJsonText_OK()
        {
            List<ClipDefinition> clips = null;
            Assert.DoesNotThrow(() => clips = JSONFileReader.DeserializeJsonText<List<ClipDefinition>>(jsonText));
            Assert.IsNotNull(clips);
            Assert.AreEqual(clips.Count, 2, "wrong clips count");
            Assert.IsNotNull(clips[0]);
            Assert.AreEqual(clips[0].Title, "Google DASH encrypted");
            Assert.AreEqual(clips[0].Url, "http://yt-dash-mse-test.commondatastorage.googleapis.com/media/oops_cenc-20121114-signedlicenseurl-manifest.mpd");
            Assert.AreEqual(clips[0].Type, "dash");
            Assert.AreEqual(clips[0].Poster, "front/img/oops.jpg");
            Assert.AreEqual(clips[0].Description, "This is clip with DASH content with DRM. User can choose desired video/audio representation");
            Assert.IsNotNull(clips[0].DRMDatas);
            Assert.AreEqual(clips[0].DRMDatas.Count, 1, "wrong drm data count");
            Assert.AreEqual(clips[0].DRMDatas[0].Scheme, "playready");
            Assert.AreEqual(clips[0].DRMDatas[0].LicenceUrl, "http://drm-playready-licensing.axtest.net/AcquireLicense");
            Assert.IsNotNull(clips[0].DRMDatas[0].KeyRequestProperties);
            Assert.AreEqual(clips[0].DRMDatas[0].KeyRequestProperties.Count, 2, "wrong drm key properties count");
            Assert.IsNull(clips[0].Subtitles);
            Assert.AreEqual(clips[0].PixelCount, 0);
            Assert.AreEqual(clips[0].Format, ClipDefinition.VideoFormat.Custom);

            Assert.IsNotNull(clips[1]);
            Assert.AreEqual(clips[1].Title, "Big Buck Bunny mp4");
            Assert.AreEqual(clips[1].Url, "http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4");
            Assert.AreEqual(clips[1].Type, "url");
            Assert.AreEqual(clips[1].Poster, "front/img/bunny.jpg");
            Assert.AreEqual(clips[1].Description, "This is clip played directly from URL");
            Assert.IsNull(clips[1].DRMDatas);
            Assert.IsNotNull(clips[1].Subtitles);
            Assert.AreEqual(clips[1].Subtitles.Count, 2, "wrong subtitles count");
            Assert.IsNotNull(clips[1].Subtitles[0]);
            Assert.AreEqual(clips[1].Subtitles[0].Path, "./subs/sample_cyrilic.srt");
            Assert.AreEqual(clips[1].Subtitles[0].Encoding, "windows-1251");
            Assert.AreEqual(clips[1].Subtitles[0].Language, "en");
            Assert.AreEqual(clips[1].Subtitles[0].Id, 11);
            Assert.Greater(clips[1].PixelCount, 0);
            Assert.AreEqual(clips[1].Format, ClipDefinition.VideoFormat.FullHD);
        }

        [Test]
        [Category("Negative")]
        public static void DeserializeJsonFile_ThrowsNull()
        {
            Assert.Throws<ArgumentNullException>(() => JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(null));
        }

        [Test]
        [Category("Negative")]
        public static void DeserializeJsonFile_ThrowsEmpty()
        {
            Assert.Throws<ArgumentException>(() => JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(""));
        }
    }
}
