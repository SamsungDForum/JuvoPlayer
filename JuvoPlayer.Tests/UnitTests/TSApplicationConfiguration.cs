/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2019, Samsung Electronics Co., Ltd
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
using NUnit.Framework;
using Configuration;

namespace JuvoPlayer.Tests.IntegrationTests
{
    [TestFixture]
    [Description("Application Configuration Tests")]
    class TSApplicationConfiguration
    {
        private static Random rnd = new Random((int)DateTime.Now.Ticks);

        private class TestSeekLogic : Configuration.Defaults.SeekLogic
        {
            public new readonly TimeSpan DefaultSeekInterval = TimeSpan.FromSeconds(rnd.Next());
            public new readonly TimeSpan DefaultSeekAccumulateInterval = TimeSpan.FromSeconds(rnd.Next());
            public new readonly double DefaultMaximumSeekIntervalPercentOfContentTotalTime = rnd.NextDouble();
            public new readonly TimeSpan DefaultSeekIntervalValueThreshold = TimeSpan.FromSeconds(rnd.Next());

            public bool SameAs(object obj)
            {
                return obj is TestSeekLogic logic &&
                       DefaultSeekInterval == logic.DefaultSeekInterval &&
                       DefaultSeekAccumulateInterval == logic.DefaultSeekAccumulateInterval &&
                       Math.Abs(DefaultMaximumSeekIntervalPercentOfContentTotalTime - logic.DefaultMaximumSeekIntervalPercentOfContentTotalTime) < 0.1 &&
                       DefaultSeekIntervalValueThreshold == logic.DefaultSeekIntervalValueThreshold;
            }
        }

        private class TestDashClient : Configuration.Defaults.DashClient
        {
            public new readonly TimeSpan TimeBufferDepthDefault = TimeSpan.FromSeconds(rnd.Next());
            public new readonly TimeSpan MaxBufferTime = TimeSpan.FromSeconds(rnd.Next());
            public new readonly TimeSpan MinBufferTime = TimeSpan.FromSeconds(rnd.Next());
            public new readonly TimeSpan MinBufferDownloadTime = TimeSpan.FromSeconds(rnd.Next());

            public bool SameAs(object obj)
            {
                return obj is TestDashClient client &&
                       TimeBufferDepthDefault == client.TimeBufferDepthDefault &&
                       MaxBufferTime == client.MaxBufferTime &&
                       MinBufferTime == client.MinBufferTime &&
                       MinBufferDownloadTime == client.MinBufferDownloadTime;
            }
        }

        private class TestDashDownloader : Configuration.Defaults.DashDownloader
        {
            public new readonly int ChunkSize = rnd.Next();

            public bool SameAs(object obj)
            {
                return obj is TestDashDownloader downloader &&
                       ChunkSize == downloader.ChunkSize;
            }
        }

        private class TestDashManifest : Configuration.Defaults.DashManifest
        {
            public new readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(rnd.Next());
            public new readonly int MaxManifestDownloadRetries = rnd.Next();
            public new readonly TimeSpan ManifestDownloadDelay = TimeSpan.FromMilliseconds(rnd.Next());
            public new readonly TimeSpan ManifestReloadDelay = TimeSpan.FromMilliseconds(rnd.Next());

            public bool SameAs(object obj)
            {
                return obj is TestDashManifest manifest &&
                       DownloadTimeout == manifest.DownloadTimeout &&
                       MaxManifestDownloadRetries == manifest.MaxManifestDownloadRetries &&
                       ManifestDownloadDelay == manifest.ManifestDownloadDelay &&
                       ManifestReloadDelay == manifest.ManifestReloadDelay;
            }
        }

        public class TestDashMediaPipeline : Configuration.Defaults.DashMediaPipeline
        {
            public new readonly TimeSpan SegmentEps = TimeSpan.FromSeconds(0.5);

            public bool SameAs(object obj)
            {
                return obj is TestDashMediaPipeline pipeline &&
                       SegmentEps == pipeline.SegmentEps;
            }
        }

        public class TestHLSDataProvider : Configuration.Defaults.HLSDataProvider
        {
            public new readonly TimeSpan MaxBufferHealth = TimeSpan.FromSeconds(10);

            public bool SameAs(object obj)
            {
                return obj is TestHLSDataProvider provider &&
                       MaxBufferHealth == provider.MaxBufferHealth;
            }
        }

        public class TestRTSPDataProvider : Configuration.Defaults.RTSPDataProvider
        {
            public new readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(2);

            public bool SameAs(object obj)
            {
                return obj is TestRTSPDataProvider provider &&
                       ConnectionTimeout == provider.ConnectionTimeout;
            }
        }

        public class TestEWMAThroughputHistory : Configuration.Defaults.EWMAThroughputHistory
        {
            public new readonly double SlowEWMACoeff = 0.99;
            public new readonly double FastEWMACoeff = 0.98;
            public new readonly double SlowBandwidth = 20000000;
            public new readonly double FastBandwidth = 20000000;

            public bool SameAs(object obj)
            {
                return obj is TestEWMAThroughputHistory history &&
                       Math.Abs(SlowEWMACoeff - history.SlowEWMACoeff) < 0.1 &&
                       Math.Abs(FastEWMACoeff - history.FastEWMACoeff) < 0.1 &&
                       Math.Abs(SlowBandwidth - history.SlowBandwidth) < 0.1 &&
                       Math.Abs(FastBandwidth - history.FastBandwidth) < 0.1;
            }
        }

        public class TestThroughputHistory : Configuration.Defaults.ThroughputHistory
        {
            public new readonly int MaxMeasurementsToKeep = 20;
            public new readonly int AverageThroughputSampleAmount = 4;
            public new readonly int MinimumThroughputSampleAmount = 2;

            public new readonly double ThroughputDecreaseScale = 1.3;
            public new readonly double ThroughputIncreaseScale = 1.3;

            public bool SameAs(object obj)
            {
                return obj is TestThroughputHistory history &&
                       MaxMeasurementsToKeep == history.MaxMeasurementsToKeep &&
                       AverageThroughputSampleAmount == history.AverageThroughputSampleAmount &&
                       MinimumThroughputSampleAmount == history.MinimumThroughputSampleAmount &&
                       Math.Abs(ThroughputDecreaseScale - history.ThroughputDecreaseScale) < 0.1 &&
                       Math.Abs(ThroughputIncreaseScale - history.ThroughputIncreaseScale) < 0.1;
            }
        }

        public class TestFFmpegDemuxer : Configuration.Defaults.FFmpegDemuxer
        {
            public new readonly ulong
                BufferSize =
                    64 * 1024; // 32kB seems to be "low level standard", but content downloading pipeline works better for 64kB

            public new readonly int
                ProbeSize =
                    32 * 1024; // higher values may cause problems when probing certain kinds of content (assert "len >= s->orig_buffer_size" in aviobuf)

            public new readonly TimeSpan MaxAnalyzeDuration = TimeSpan.FromSeconds(10);

            public bool SameAs(object obj)
            {
                return obj is TestFFmpegDemuxer demuxer &&
                       BufferSize == demuxer.BufferSize &&
                       ProbeSize == demuxer.ProbeSize &&
                       MaxAnalyzeDuration == demuxer.MaxAnalyzeDuration;
            }
        }

        public class TestCencSession : Configuration.Defaults.CencSession
        {
            public new readonly int MaxDecryptRetries = 5;
            public new readonly TimeSpan DecryptBufferFullSleepTime = TimeSpan.FromMilliseconds(1000);

            public bool SameAs(object obj)
            {
                return obj is TestCencSession session &&
                       MaxDecryptRetries == session.MaxDecryptRetries &&
                       DecryptBufferFullSleepTime == session.DecryptBufferFullSleepTime;
            }
        }

        public class TestEsStream : Configuration.Defaults.EsStream
        {
            public new readonly TimeSpan TransferChunk = TimeSpan.FromSeconds(2);

            public bool SameAs(object obj)
            {
                return obj is TestEsStream stream &&
                       TransferChunk == stream.TransferChunk;
            }
        }

        public static void SetDefaultConfiguration()
        {
            SeekLogic.Config = new Configuration.Defaults.SeekLogic();
            DashClient.Config = new Configuration.Defaults.DashClient();
            DashDownloader.Config = new Configuration.Defaults.DashDownloader();
            DashManifest.Config = new Configuration.Defaults.DashManifest();
            DashMediaPipeline.Config = new Configuration.Defaults.DashMediaPipeline();
            HLSDataProvider.Config = new Configuration.Defaults.HLSDataProvider();
            RTSPDataProvider.Config = new Configuration.Defaults.RTSPDataProvider();
            EWMAThroughputHistory.Config = new Configuration.Defaults.EWMAThroughputHistory();
            ThroughputHistory.Config = new Configuration.Defaults.ThroughputHistory();
            FFmpegDemuxer.Config = new Configuration.Defaults.FFmpegDemuxer();
            CencSession.Config = new Configuration.Defaults.CencSession();
            EsStream.Config = new Configuration.Defaults.EsStream();
        }

        [Test]
        [Description("Application configuration accessible")]
        public static void DefaultConfigurationExists()
        {
            SetDefaultConfiguration();

            Assert.IsNotNull(SeekLogic.Config);
            Assert.IsNotNull(DashClient.Config);
            Assert.IsNotNull(DashDownloader.Config);
            Assert.IsNotNull(DashManifest.Config);
            Assert.IsNotNull(DashMediaPipeline.Config);
            Assert.IsNotNull(HLSDataProvider.Config);
            Assert.IsNotNull(RTSPDataProvider.Config);
            Assert.IsNotNull(EWMAThroughputHistory.Config);
            Assert.IsNotNull(ThroughputHistory.Config);
            Assert.IsNotNull(FFmpegDemuxer.Config);
            Assert.IsNotNull(CencSession.Config);
            Assert.IsNotNull(EsStream.Config);
        }

        [Test]
        [Description("Application configuration changeable")]
        public static void ConfigurationCanChange()
        {
            SeekLogic.Config = null;
            DashClient.Config = null;
            DashDownloader.Config = null;
            DashManifest.Config = null;
            DashMediaPipeline.Config = null;
            HLSDataProvider.Config = null;
            RTSPDataProvider.Config = null;
            EWMAThroughputHistory.Config = null;
            ThroughputHistory.Config = null;
            FFmpegDemuxer.Config = null;
            CencSession.Config = null;
            EsStream.Config = null;

            Assert.IsNull(SeekLogic.Config);
            Assert.IsNull(DashClient.Config);
            Assert.IsNull(DashDownloader.Config);
            Assert.IsNull(DashManifest.Config);
            Assert.IsNull(DashMediaPipeline.Config);
            Assert.IsNull(HLSDataProvider.Config);
            Assert.IsNull(RTSPDataProvider.Config);
            Assert.IsNull(EWMAThroughputHistory.Config);
            Assert.IsNull(ThroughputHistory.Config);
            Assert.IsNull(FFmpegDemuxer.Config);
            Assert.IsNull(CencSession.Config);
            Assert.IsNull(EsStream.Config);

            TestSeekLogic seekLogic = new TestSeekLogic();
            TestDashClient dashConfig = new TestDashClient();
            TestDashDownloader dashDataDownloader = new TestDashDownloader();
            TestDashManifest dashManifestProvider = new TestDashManifest();
            TestDashMediaPipeline dashMediaPipeline = new TestDashMediaPipeline();
            TestHLSDataProvider hlsDataProvider = new TestHLSDataProvider();
            TestRTSPDataProvider rtspDataProvider = new TestRTSPDataProvider();
            TestEWMAThroughputHistory ewmaThroughgputHistory = new TestEWMAThroughputHistory();
            TestThroughputHistory throughputHistory = new TestThroughputHistory();
            TestFFmpegDemuxer ffmpegDemuxer = new TestFFmpegDemuxer();
            TestCencSession cencSession = new TestCencSession();
            TestEsStream esStream = new TestEsStream();

            SeekLogic.Config = seekLogic;
            DashClient.Config = dashConfig;
            DashDownloader.Config = dashDataDownloader;
            DashManifest.Config = dashManifestProvider;
            DashMediaPipeline.Config = dashMediaPipeline;
            HLSDataProvider.Config = hlsDataProvider;
            RTSPDataProvider.Config = rtspDataProvider;
            EWMAThroughputHistory.Config = ewmaThroughgputHistory;
            ThroughputHistory.Config = throughputHistory;
            FFmpegDemuxer.Config = ffmpegDemuxer;
            CencSession.Config = cencSession;
            EsStream.Config = esStream;

            Assert.IsNotNull(SeekLogic.Config);
            Assert.IsNotNull(DashClient.Config);
            Assert.IsNotNull(DashDownloader.Config);
            Assert.IsNotNull(DashManifest.Config);
            Assert.IsNotNull(DashMediaPipeline.Config);
            Assert.IsNotNull(HLSDataProvider.Config);
            Assert.IsNotNull(RTSPDataProvider.Config);
            Assert.IsNotNull(EWMAThroughputHistory.Config);
            Assert.IsNotNull(ThroughputHistory.Config);
            Assert.IsNotNull(FFmpegDemuxer.Config);
            Assert.IsNotNull(CencSession.Config);
            Assert.IsNotNull(EsStream.Config);

            Assert.IsTrue(seekLogic.SameAs(SeekLogic.Config));
            Assert.IsTrue(dashConfig.SameAs(DashClient.Config));
            Assert.IsTrue(dashDataDownloader.SameAs(DashDownloader.Config));
            Assert.IsTrue(dashManifestProvider.SameAs(DashManifest.Config));
            Assert.IsTrue(dashMediaPipeline.SameAs(DashMediaPipeline.Config));
            Assert.IsTrue(hlsDataProvider.SameAs(HLSDataProvider.Config));
            Assert.IsTrue(rtspDataProvider.SameAs(RTSPDataProvider.Config));
            Assert.IsTrue(ewmaThroughgputHistory.SameAs(EWMAThroughputHistory.Config));
            Assert.IsTrue(throughputHistory.SameAs(ThroughputHistory.Config));
            Assert.IsTrue(ffmpegDemuxer.SameAs(FFmpegDemuxer.Config));
            Assert.IsTrue(cencSession.SameAs(CencSession.Config));
            Assert.IsTrue(esStream.SameAs(EsStream.Config));
        }
    }
}
