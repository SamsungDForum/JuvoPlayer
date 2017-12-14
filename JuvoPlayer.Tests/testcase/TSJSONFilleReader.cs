using NUnit.Framework;
using System;
using System.Collections.Generic;
using JuvoPlayer.Common;

namespace JuvoPlayer.Tests
{
    [TestFixture]
    [Description("")]
    class JSONFileReaderTests
    {
        //TODO(p.galiszewsk): move it to resource: http://www.cauldwell.net/patrick/blog/PermaLink,guid,e9a1451b-108c-4da7-8be9-2b6c2316f7b1.aspx
        static string jsonText = @"
         [
          { 
            ""title"": ""Google DASH encrypted"",
            ""url"": ""http://yt-dash-mse-test.commondatastorage.googleapis.com/media/oops_cenc-20121114-signedlicenseurl-manifest.mpd"",
            ""type"": ""dash"",
            ""poster"": ""front/img/oops.jpg"",
            ""description"": ""This is clip with DASH content with DRM. User can choose desired video/audio representation""
          },
          {
            ""title"": ""Big Buck Bunny mp4"",
            ""url"": ""http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4"",
            ""subtitles"": [
              {
                ""subtitle"": ""./subs/sample_cyrilic.srt"",
                ""encoding"": ""windows-1251"",
                ""language"": ""en (external)"",
                ""isActive"": true,
                ""id"": ""11""
              },
              {
                ""subtitle"": ""./subs/sample_cyrilic_utf8.srt"",
                ""language"": ""ko (external)"",
                ""id"": ""10"",
                ""encoding"": ""windows-1251"",
              }
            ],
            ""type"": ""url"",
            ""poster"": ""front/img/bunny.jpg"",
            ""description"": ""This is clip played directly from URL""
          },
         ]";

        [SetUp]
        public static void Init()
        {
        }

        [TearDown]
        public static void Destroy()
        {
        }

        [Test]
        [Description("DeserializeJsonText throws on null argument")]
        [Property("SPEC", "JuvoPlayer.JSONFileReader.DeserializeJsonText M")]
        //[Property("COVPARAM", " ")]
        public static void DeserializeJsonText_ThrowsNull()
        {
            Assert.Throws<ArgumentNullException>(() => JSONFileReader.DeserializeJsonText<List<ClipDefinition>>(null));
        }

        [Test]
        [Description("DeserializeJsonText throws on empty argument")]
        [Property("SPEC", "JuvoPlayer.JSONFileReader.DeserializeJsonText M")]
        //[Property("COVPARAM", " ")]
        public static void DeserializeJsonText_ThrowsEmpty()
        {
            Assert.Throws<ArgumentException>(() => JSONFileReader.DeserializeJsonText<List<ClipDefinition>>(""));
        }

        [Test]
        [Description("DeserializeJsonText throws on invalid json")]
        [Property("SPEC", "JuvoPlayer.JSONFileReader.DeserializeJsonText M")]
        //[Property("COVPARAM", " ")]
        public static void DeserializeJsonText_ThrowsInvalid()
        {
            Assert.Throws<Exception>(() => JSONFileReader.DeserializeJsonText<List<ClipDefinition>>("invalid"));
        }

        [Test]
        [Description("DeserializeJsonText OK")]
        [Property("SPEC", "JuvoPlayer.JSONFileReader.DeserializeJsonText M")]
        //[Property("COVPARAM", " ")]
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
            Assert.IsNull(clips[0].Subtitles);

            Assert.IsNotNull(clips[1]);
            Assert.AreEqual(clips[1].Title, "Big Buck Bunny mp4");
            Assert.AreEqual(clips[1].Url, "http://distribution.bbb3d.renderfarming.net/video/mp4/bbb_sunflower_1080p_30fps_normal.mp4");
            Assert.AreEqual(clips[1].Type, "url");
            Assert.AreEqual(clips[1].Poster, "front/img/bunny.jpg");
            Assert.AreEqual(clips[1].Description, "This is clip played directly from URL");
            Assert.IsNotNull(clips[1].Subtitles);
            Assert.AreEqual(clips[1].Subtitles.Count, 2, "wrong subtitles count");
            Assert.IsNotNull(clips[1].Subtitles[0]);
            Assert.AreEqual(clips[1].Subtitles[0].Subtitle, "./subs/sample_cyrilic.srt");
            Assert.AreEqual(clips[1].Subtitles[0].Encoding, "windows-1251");
            Assert.AreEqual(clips[1].Subtitles[0].Language, "en(external)");
            Assert.AreEqual(clips[1].Subtitles[0].IsActive, true);
            Assert.AreEqual(clips[1].Subtitles[0].Id, "11");
        }

        [Test]
        [Description("DeserializeJsonFile throws on null argument")]
        [Property("SPEC", "JuvoPlayer.JSONFileReader.DeserializeJsonFile M")]
        //[Property("COVPARAM", " ")]
        public static void DeserializeJsonFile_ThrowsNull()
        {
            Assert.Throws<ArgumentNullException>(() => JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(null));
        }

        [Test]
        [Description("DeserializeJsonFile throws on empty argument")]
        [Property("SPEC", "JuvoPlayer.JSONFileReader.DeserializeJsonFile M")]
        //[Property("COVPARAM", " ")]
        public static void DeserializeJsonFile_ThrowsEmpty()
        {
            Assert.Throws<ArgumentException>(() => JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(""));
        }
    }
}
