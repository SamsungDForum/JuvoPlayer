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

﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using JuvoPlayer.Utils;
using MpdParser;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    public class MPDData
    {
        public string url { get; set; }
        public string file { get; set; }
        public bool ignore_mpd_comparison { get; set; }
    }

    public class DOCData
    {
        public string url { get; set; }
        public string rawXML { get; set; }
        public Document parsedmpd { get; set; }
        public Media audioMedia { get; set; }
        public Media videoMedia { get; set; }
        public Representation audioRepresentation { get; set; }
        public Representation videoRepresentation { get; set; }

        public MpdParser.Node.IRepresentationStream audioStream { get; set; }
        public MpdParser.Node.IRepresentationStream videoStream { get; set; }

        public bool ignore_mpd_comparison {get;set;}


    }

    [TestFixture]
    [Description("" +
        "Tests parsing of MPDs defined in mpddata.json file" +
        "MPDs along with mpddata.json are stored in MPD Parser Data"
        )]
    class TSMPDParser
    {

        private static List<MPDData> mpds = null;
        private static List<DOCData> parsedMpds = new List<DOCData>();

        public TSMPDParser()
        {
            var jsonText = ReadEmbeddedFile("mpddata.json");

            Assert.IsNotNull(jsonText);

            Assert.DoesNotThrow(() => mpds = JSONFileReader.DeserializeJsonText<List<MPDData>>(jsonText));
            Assert.IsNotNull(mpds);
            Assert.AreNotEqual(mpds.Count, 0, "No data found");
        }

        private static string ReadEmbeddedFile(string fName)
        {
            string data = null;

            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] names = assembly.GetManifestResourceNames();
            foreach (var name in names)
            {
                if (!name.Contains(fName)) continue;
                var stream = assembly.GetManifestResourceStream(name);
                var reader = new StreamReader(stream);
                data = reader.ReadToEnd();
                reader.Close();
                break;
            }

            return data;
        }
        private static Media Find(MpdParser.Period p, string language, MediaType type, MediaRole role = MediaRole.Main)
        {
            Media res = null;
            for (int i = 0; i < p.Sets.Length; i++)
            {
                if (p.Sets[i].Type.Value != type)
                {
                    continue;
                }

                if (language != null)
                {
                    if (p.Sets[i].Lang != language)
                    {
                        continue;
                    }
                }

                if (p.Sets[i].HasRole(role))
                {
                    res = p.Sets[i];
                    break;
                }

                if (p.Sets[i].Roles.Length == 0)
                {
                    res = p.Sets[i];
                    break;
                }
            }

            return res;
        }

        [Test]
        [Description("FromText returns a parsed mpd (Document) OK")]
        [Property("SPEC", "MpdParser.Document.FromText M")]
        public static void AppParser_OK()
        {
            foreach(var tc in mpds)
            {

                DOCData item = new DOCData();

                item.audioMedia = null;
                item.videoMedia = null;
                item.audioRepresentation = null;
                item.videoRepresentation = null;
                item.audioStream = null;
                item.videoStream = null;
                item.url = null;
                item.ignore_mpd_comparison = false;

                item.rawXML = ReadEmbeddedFile(tc.file);

                Assert.DoesNotThrowAsync(async () =>
                    item.parsedmpd = await Document.FromText(item.rawXML, tc.url)
                    );
                Assert.IsNotNull(item.parsedmpd);

                item.url = tc.url;
                item.ignore_mpd_comparison = tc.ignore_mpd_comparison;

                parsedMpds.Add(item);

            }
        }

        [Test]
        [Description("Audio & Video Media extractable from mpd (Document) OK")]
        [Property("SPEC", "MpdParser.Document.Periods M")]
        public static void HasAVMedia_OK()
        {
            if(parsedMpds.Count == 0 )
            {
                AppParser_OK();
            }

            foreach (var item in parsedMpds)
            {
                foreach (var period in item.parsedmpd.Periods)
                {
                    Assert.DoesNotThrow(() =>
                    {
                        item.videoMedia = Find(period, "en", MediaType.Audio) ??
                        Find(period, "und", MediaType.Audio) ??
                        Find(period, null, MediaType.Audio);
                    }
                        );

                    Assert.DoesNotThrow(() =>
                    {
                        item.audioMedia = Find(period, "en", MediaType.Video) ??
                        Find(period, "und", MediaType.Video) ??
                        Find(period, null, MediaType.Video);
                    }
                        );

                    Assert.IsNotNull(item.videoMedia);
                    Assert.IsNotNull(item.audioMedia);
                }
            }
        }




        [Test]
        [Description("Audio & Video Streams extractable from mpd (Document) OK")]
        [Property("SPEC", "MpdParser.Media.Representation M")]
        public static void HasRepresentation_OK()
        {
            if (parsedMpds.Count == 0)
            {
                HasAVMedia_OK();
            }

            foreach (var item in parsedMpds)
            {

                Assert.DoesNotThrow(() =>
                        {
                            item.audioRepresentation = item.audioMedia.Representations.First();
                        }
                     );

                Assert.DoesNotThrow(() =>
                        {
                            item.videoRepresentation = item.videoMedia.Representations.First();
                        }
                    );

                Assert.IsNotNull(item.audioRepresentation);
                Assert.IsNotNull(item.videoRepresentation);

            }
        }

        static string schema = "urn:mpeg:dash:schema:mpd:2011";

        public static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [Test]
        [Description("Compares 2 DASH Documents, application parsed and system parsed.")]
        [Property("SPEC", "MpdParser.DASH M")]
        public static async Task XMLData_AppParserSysParser_same_OK()
        {
            if (parsedMpds.Count == 0)
            {
                HasRepresentation_OK();
            }

            bool allTestsResult = true;
            foreach (var item in parsedMpds)
            {

                // Schema Name replacement is done in "raw XML string".
                // Schema/namespace modifications seem to work... oddly and throwing
                // exception if schema name already exists in XSD file (WTF?)
                // so the simplest way is to perform checks on raw XML string.
                // Ugly...
                string tmpXml = string.Copy(item.rawXML);
                if (tmpXml.IndexOf(schema, StringComparison.Ordinal) == -1)
                {
                    int idx = tmpXml.IndexOf(schema, StringComparison.OrdinalIgnoreCase);
                    if (idx == -1)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Incorrect input schema in {item.url}. caseless urn:mpeg:dash:schema:mpd:2011 not found. Ignoring TC");
                        continue;
                    }

                    //Case mismatch. Move everything to lower case
                    tmpXml = tmpXml.Replace(tmpXml.Substring(idx, schema.Length), schema);
                }

                // ok... so we are "re-parsing" internal xml here...
                // but do it from original data (not schema replaced one)
                MpdParser.Node.DASH IntMPD = await Document.FromTextInternal(item.rawXML, item.url);

                XmlSerializer serializer = new XmlSerializer(typeof(MPDtype));
                System.IO.StringReader reader = new System.IO.StringReader(tmpXml);
                //Stream XMLStream = GenerateStreamFromString(tmpxml);
                XmlReader XReader = XmlReader.Create(reader);

                // Call the Deserialize method to restore the object's state.
                MPDtype ExtMPD = null;

                try
                {
                    ExtMPD = (MPDtype)serializer.Deserialize(XReader);
                }
                catch(Exception e)
                {
                    System.Diagnostics.Debug.WriteLine($"Unparsable MPD (System Parser): {item.url}");
                    System.Diagnostics.Debug.WriteLine($"Exception: {e.Message} {e.Source}");
                    System.Diagnostics.Debug.WriteLine($"Exception: {e.InnerException}");
                    System.Diagnostics.Debug.WriteLine($"Unparsable MPD will NOT influence final result as comparison is not possible");

                    continue;
                }

                MpdParser.Node.DASH itemFromExtMPD = DASHConverter.Convert(ExtMPD,item.url);

                System.Diagnostics.Debug.WriteLine($"Checking URL: {item.url} ...");
                bool res = DASHConverter.Same(itemFromExtMPD, "Sys Parser", IntMPD, "App Parser");
                if (item.ignore_mpd_comparison)
                {
                    System.Diagnostics.Debug.WriteLine($"Same: {res} (Result is ignored by JSON specification");
                }
                else
                {
                    allTestsResult &= res;
                    System.Diagnostics.Debug.WriteLine($"Same: {res}");
                }
                System.Diagnostics.Debug.WriteLine($"Done URL: {item.url}");
            }

            System.Diagnostics.Debug.WriteLine($"All MPDs same: {allTestsResult}");

            Assert.IsTrue(allTestsResult);
        }

    }



}
