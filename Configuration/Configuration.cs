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

namespace Configuration
{

    public static class SeekLogic
    {
        public static Defaults.SeekLogic Config { get; set; } = new Defaults.SeekLogic();
    }

    public static class DashClient
    {
        public static Defaults.DashClient Config { get; set; } = new Defaults.DashClient();
    }

    public static class DashDownloader
    {
        public static Defaults.DashDownloader Config { get; set; } = new Defaults.DashDownloader();
    }

    public static class DashManifest
    {
        public static Defaults.DashManifest Config { get; set; } = new Defaults.DashManifest();
    }

    public static class DashMediaPipeline
    {
        public static Defaults.DashMediaPipeline Config { get; set; } = new Defaults.DashMediaPipeline();
    }

    public static class HLSDataProvider
    {
        public static Defaults.HLSDataProvider Config { get; set; } = new Defaults.HLSDataProvider();
    }

    public static class RTSPDataProvider
    {
        public static Defaults.RTSPDataProvider Config { get; set; } = new Defaults.RTSPDataProvider();
    }

    public static class EWMAThroughputHistory
    {
        public static Defaults.EWMAThroughputHistory Config { get; set; } = new Defaults.EWMAThroughputHistory();
    }

    public static class ThroughputHistory
    {
        public static Defaults.ThroughputHistory Config { get; set; } = new Defaults.ThroughputHistory();
    }

    public static class FFmpegDemuxer
    {
        public static Defaults.FFmpegDemuxer Config { get; set; } = new Defaults.FFmpegDemuxer();
    }

    public static class CencSession
    {
        public static Defaults.CencSession Config { get; set; } = new Defaults.CencSession();
    }

    public static class EsStream
    {
        public static Defaults.EsStream Config { get; set; } = new Defaults.EsStream();
    }
}
