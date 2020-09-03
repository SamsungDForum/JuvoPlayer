/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2020, Samsung Electronics Co., Ltd
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
 *
 */

using System.IO;
using System.Linq;
using JuvoPlayer.Common;
using JuvoPlayer.Dash;
using NUnit.Framework;

namespace JuvoPlayer.Tests.Impl.Blocks.Dash
{
    [TestFixture]
    public class DashManifestParserTests
    {
        private static readonly string SingleSegmentRepresentationMpd =
            Path.Combine("res", "SingleSegmentRepresentation.mpd");

        private static readonly string SegmentTemplateMpd = Path.Combine("res", "SegmentTemplate.mpd");

        private static readonly string SegmentTemplateWithTimelineMpd =
            Path.Combine("res", "SegmentTemplate_SegmentTimeline_ContentProtection.mpd");

        private static readonly string SubtitlesMpd = Path.Combine("res", "Subtitles.mpd");
        private static readonly string DynamicMpd = Path.Combine("res", "Dynamic.mpd");
        private const string SampleMpdBaseUrl = "http://test.org/sample.mpd";

        public static string[] AllMpds =
        {
            SingleSegmentRepresentationMpd, SegmentTemplateMpd, SegmentTemplateWithTimelineMpd, SubtitlesMpd,
            DynamicMpd
        };

        [Test]
        [TestCaseSource(typeof(DashManifestParserTests), nameof(AllMpds))]
        public void Parsing_MpdParsed_DoesNotThrow(string mpdPath)
        {
            Assert.DoesNotThrow(() =>
            {
                using (var mpdStream = File.OpenRead(mpdPath))
                {
                    var parser = new DashManifestParser();
                    var manifest = parser.Parse(mpdStream, SampleMpdBaseUrl);
                    Assert.That(manifest, Is.Not.Null);
                }
            });
        }

        [Test]
        public void Parsing_SingleSegmentRepresentationParsed_ReturnsProperRepresentation()
        {
            using (var mpdStream = File.OpenRead(SingleSegmentRepresentationMpd))
            {
                var parser = new DashManifestParser();
                var manifest = parser.Parse(mpdStream, SampleMpdBaseUrl);
                var periods = manifest.Periods;
                Assert.That(periods.Count, Is.EqualTo(1));

                var period = periods[0];
                var adaptationSets = period.AdaptationSets;
                Assert.That(adaptationSets.Count, Is.EqualTo(2));

                var videoAdaptationSet = adaptationSets.Single(set => set.ContentType == ContentType.Video);
                Assert.That(videoAdaptationSet, Is.Not.Null);

                var representations = videoAdaptationSet.Representations;
                Assert.That(representations.Count, Is.EqualTo(6));

                var firstRepresentation = representations.Single(repr => repr.Format.Id == "1");
                Assert.That(firstRepresentation, Is.Not.Null);

                var index = firstRepresentation.GetIndex();
                Assert.That(index, Is.Null);

                var indexUri = firstRepresentation.GetIndexUri();
                Assert.That(indexUri, Is.Not.Null);

                var initializationUri = firstRepresentation.GetInitializationUri();
                Assert.That(initializationUri, Is.Not.Null);
            }
        }

        [Test]
        public void Parsing_DynamicMpdParsed_ReturnsProperRepresentation()
        {
            using (var mpdStream = File.OpenRead(DynamicMpd))
            {
                var parser = new DashManifestParser();
                var manifest = parser.Parse(mpdStream, SampleMpdBaseUrl);
                Assert.That(manifest.Dynamic, Is.True);

                var utcTiming = manifest.UtcTiming;
                Assert.That(utcTiming, Is.Not.Null);
                Assert.That(utcTiming.SchemeIdUri, Is.EqualTo("urn:mpeg:dash:utc:direct:2014"));
                Assert.That(utcTiming.Value, Is.EqualTo("2020-06-02T14:50:10Z"));

                var timeShiftBufferDepth = manifest.TimeShiftBufferDepth;
                Assert.That(timeShiftBufferDepth, Is.Not.Null);

                var availabilityStartTime = manifest.AvailabilityStartTime;
                Assert.That(availabilityStartTime, Is.Not.Null);

                var minimumUpdatePeriod = manifest.MinimumUpdatePeriod;
                Assert.That(minimumUpdatePeriod, Is.Not.Null);
            }
        }

        [Test]
        public void Parsing_MpdWithSubtitles_ReturnsProperRepresentation()
        {
            using (var mpdStream = File.OpenRead(SubtitlesMpd))
            {
                var parser = new DashManifestParser();
                var manifest = parser.Parse(mpdStream, SampleMpdBaseUrl);
                var periods = manifest.Periods;
                Assert.That(periods.Count, Is.EqualTo(1));

                var period = periods[0];
                var adaptationSets = period.AdaptationSets;
                Assert.That(adaptationSets.Count, Is.EqualTo(8));

                var textAdaptationSets =
                    adaptationSets.Where(adaptationSet => adaptationSet.ContentType == ContentType.Text).ToList();
                Assert.That(textAdaptationSets.Count, Is.EqualTo(4));

                var enRepresentation = textAdaptationSets.Single(adaptationSet =>
                    adaptationSet.Representations[0].Format.Language == "en");
                Assert.That(enRepresentation, Is.Not.Null);
            }
        }

        [Test]
        public void Parsing_MpdWithSegmentTemplate_ReturnsProperRepresentation()
        {
            using (var mpdStream = File.OpenRead(SegmentTemplateMpd))
            {
                var parser = new DashManifestParser();
                var manifest = parser.Parse(mpdStream, SampleMpdBaseUrl);
                var periods = manifest.Periods;
                Assert.That(periods.Count, Is.EqualTo(1));

                var period = periods[0];
                var adaptationSets = period.AdaptationSets;
                Assert.That(adaptationSets.Count, Is.EqualTo(2));

                var videoAdaptationSet =
                    adaptationSets.Single(adaptationSet => adaptationSet.ContentType == ContentType.Video);

                var representations = videoAdaptationSet.Representations;
                Assert.That(representations.Count, Is.EqualTo(5));

                var firstRepresentation = representations[0];
                var segmentIndex = firstRepresentation.GetIndex();
                Assert.That(segmentIndex, Is.Not.Null);

                var indexUri = firstRepresentation.GetIndexUri();
                Assert.That(indexUri, Is.Null);
            }
        }

        [Test]
        public void Parsing_MpdWithSegmentTemplateWithTimeline_ReturnsProperRepresentation()
        {
            using (var mpdStream = File.OpenRead(SegmentTemplateWithTimelineMpd))
            {
                var parser = new DashManifestParser();
                var manifest = parser.Parse(mpdStream, SampleMpdBaseUrl);
                var periods = manifest.Periods;
                Assert.That(periods.Count, Is.EqualTo(1));

                var period = periods[0];
                var adaptationSets = period.AdaptationSets;
                Assert.That(adaptationSets.Count, Is.EqualTo(2));

                var videoAdaptationSet =
                    adaptationSets.Single(adaptationSet => adaptationSet.ContentType == ContentType.Video);

                var representations = videoAdaptationSet.Representations;
                Assert.That(representations.Count, Is.EqualTo(12));

                var firstRepresentation = representations[0];
                var segmentIndex = firstRepresentation.GetIndex();
                Assert.That(segmentIndex, Is.Not.Null);

                var indexUri = firstRepresentation.GetIndexUri();
                Assert.That(indexUri, Is.Null);
            }
        }
    }
}
