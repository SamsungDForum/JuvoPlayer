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
    public static class SeekLogic
    {
        public static readonly TimeSpan DefaultSeekInterval = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan DefaultSeekAccumulateInterval = TimeSpan.FromSeconds(2);
        public const double DefaultMaximumSeekIntervalPercentOfContentTotalTime = 1.0;
        public static readonly TimeSpan DefaultSeekIntervalValueThreshold = TimeSpan.FromMilliseconds(200); // time between key events when key is being hold is ~100ms
    }

    public static class DashClient
    {
        public static readonly TimeSpan TimeBufferDepthDefault = TimeSpan.FromSeconds(10);
        public static readonly TimeSpan MaxBufferTime = TimeSpan.FromSeconds(15);
        public static readonly TimeSpan MinBufferTime = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan MinBufferDownloadTime = TimeSpan.FromSeconds(4);
    }

    public static class DashDownloader
    {
        public const int ChunkSize = 64 * 1024;
    }

    public static class DashManifest
    {
        public static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(3);
        public const int MaxManifestDownloadRetries = 3;
        public static readonly TimeSpan ManifestDownloadDelay = TimeSpan.FromMilliseconds(1000);
        public static readonly TimeSpan ManifestReloadDelay = TimeSpan.FromMilliseconds(1500);
    }

    public static class DashMediaPipeline
    {
        public static readonly TimeSpan SegmentEps = TimeSpan.FromSeconds(0.5);
    }

    public static class HLSDataProvider
    {
        public static readonly TimeSpan MaxBufferHealth = TimeSpan.FromSeconds(10);
    }

    public static class RTSPDataProvider
    {
        public static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(2);
    }

    public static class EWMAThroughputHistory
    {
        public const double SlowEWMACoeff = 0.99;
        public const double FastEWMACoeff = 0.98;
        public const double SlowBandwidth = 20000000;
        public const double FastBandwidth = 20000000;
    }

    public static class ThroughputHistory
    {
        public const int MaxMeasurementsToKeep = 20;
        public const int AverageThroughputSampleAmount = 4;
        public const int MinimumThroughputSampleAmount = 2;

        public const double ThroughputDecreaseScale = 1.3;
        public const double ThroughputIncreaseScale = 1.3;
    }

    public static class FFmpegDemuxer
    {
        public const int BufferSize = 64 * 1024; // 32kB seems to be "low level standard", but content downloading pipeline works better for 64kB
        public const int ProbeSize = 32 * 1024; // higher values may cause problems when probing certain kinds of content (assert "len >= s->orig_buffer_size" in aviobuf)
    }

    public static class CencSession
    {
        public const int MaxDecryptRetries = 5;
        public static readonly TimeSpan DecryptBufferFullSleepTime = TimeSpan.FromMilliseconds(1000);
    }

    public static class EsStream
    {
        public static readonly TimeSpan TransferChunk = TimeSpan.FromSeconds(2);
    }

    public static class EsStreamController
    {
        public static readonly TimeSpan PreBufferDuration = TimeSpan.FromSeconds(2);
    }
}
