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

namespace Configuration
{
    namespace Defaults
    {
        public class SeekLogic
        {
            public readonly TimeSpan DefaultSeekInterval = TimeSpan.FromSeconds(5);
            public readonly TimeSpan DefaultSeekAccumulateInterval = TimeSpan.FromSeconds(2);
            public readonly double DefaultMaximumSeekIntervalPercentOfContentTotalTime = 1.0;

            public readonly TimeSpan
                DefaultSeekIntervalValueThreshold =
                    TimeSpan.FromMilliseconds(200); // time between key events when key is being hold is ~100ms   
        }

        public class DashClient
        {
            public readonly TimeSpan TimeBufferDepthDefault = TimeSpan.FromSeconds(10);
            public readonly TimeSpan MaxBufferTime = TimeSpan.FromSeconds(15);
            public readonly TimeSpan MinBufferTime = TimeSpan.FromSeconds(5);
            public readonly TimeSpan MinBufferDownloadTime = TimeSpan.FromSeconds(4);
        }

        public class DashDownloader
        {
            public readonly int ChunkSize = 64 * 1024;
        }

        public class DashManifest
        {
            public readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(3);
            public readonly int MaxManifestDownloadRetries = 3;
            public readonly TimeSpan ManifestDownloadDelay = TimeSpan.FromMilliseconds(1000);
            public readonly TimeSpan ManifestReloadDelay = TimeSpan.FromMilliseconds(1500);
        }

        public class DashMediaPipeline
        {
            public readonly TimeSpan SegmentEps = TimeSpan.FromSeconds(0.5);
        }

        public class HLSDataProvider
        {
            public readonly TimeSpan MaxBufferHealth = TimeSpan.FromSeconds(10);
        }

        public class RTSPDataProvider
        {
            public readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(2);
        }

        public class EWMAThroughputHistory
        {
            public readonly double SlowEWMACoeff = 0.99;
            public readonly double FastEWMACoeff = 0.98;
            public readonly double SlowBandwidth = 20000000;
            public readonly double FastBandwidth = 20000000;
        }

        public class ThroughputHistory
        {
            public readonly int MaxMeasurementsToKeep = 20;
            public readonly int AverageThroughputSampleAmount = 4;
            public readonly int MinimumThroughputSampleAmount = 2;

            public readonly double ThroughputDecreaseScale = 1.3;
            public readonly double ThroughputIncreaseScale = 1.3;
        }

        public class FFmpegDemuxer
        {
            public readonly ulong
                BufferSize =
                    64 * 1024; // 32kB seems to be "low level standard", but content downloading pipeline works better for 64kB

            public readonly int
                ProbeSize =
                    32 * 1024; // higher values may cause problems when probing certain kinds of content (assert "len >= s->orig_buffer_size" in aviobuf)

            public readonly TimeSpan MaxAnalyzeDuration = TimeSpan.FromSeconds(10);
        }

        public class CencSession
        {
            public readonly int MaxDecryptRetries = 5;
            public readonly TimeSpan DecryptBufferFullSleepTime = TimeSpan.FromMilliseconds(1000);
        }

        public class EsStream
        {
            public readonly TimeSpan TransferChunk = TimeSpan.FromSeconds(2);
        }
    }
}
