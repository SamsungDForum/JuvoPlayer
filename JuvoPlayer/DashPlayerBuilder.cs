/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

using System;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Dash;
using JuvoPlayer.Demuxers.FFmpeg;
using JuvoPlayer.Drms;
using JuvoPlayer.Players;

namespace JuvoPlayer
{
    public class DashPlayerBuilder
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private Configuration _configuration;
        private string _mpdUri;
        private IWindow _window;
        private string _keySystem;
        private IDrmSessionHandler _drmSessionHandler;

        public DashPlayerBuilder SetMpdUri(string mpdUri)
        {
            _mpdUri = mpdUri;
            return this;
        }

        public DashPlayerBuilder SetWindow(IWindow window)
        {
            _window = window;
            return this;
        }

        public DashPlayerBuilder SetConfiguration(Configuration configuration)
        {
            _configuration = configuration;
            return this;
        }

        public DashPlayerBuilder SetKeySystem(string keySystem)
        {
            _keySystem = keySystem;
            return this;
        }

        public DashPlayerBuilder SetDrmSessionHandler(IDrmSessionHandler drmSessionHandler)
        {
            _drmSessionHandler = drmSessionHandler;
            return this;
        }

        public IPlayer Build()
        {
            if (_keySystem != null && _drmSessionHandler == null)
            {
                throw new ArgumentNullException(
                    $"KeySystem set without DrmSessionHandler");
            }

            if (_drmSessionHandler != null && _keySystem == null)
            {
                _logger.Warn("DrmSessionHandler set without KeySystem." +
                             "Ignoring DrmSessionHandler");
            }

            CdmContext cdmContext = null;
            if (_keySystem != null)
            {
                var platform = Platform.Current;
                var capabilities = platform.Capabilities;
                if (!capabilities.SupportsKeySystem(_keySystem))
                {
                    throw new NotSupportedException(
                        $"{_keySystem} is not supported");
                }

                cdmContext = new CdmContext(_drmSessionHandler);
                cdmContext.InitCdmInstance(_keySystem);
            }

            var clock = new Clock(new StopwatchClockSource());
            var player = new Player(
                () => Platform.Current.CreatePlatformPlayer(),
                cdmContext,
                clock,
                _window,
                _configuration);
            var dashStreamProvider = new DashStreamProvider(
                new ManifestLoader(),
                new ThroughputHistory(),
                new Downloader(),
                () => new FFmpegDemuxer(new FFmpegGlue()),
                clock,
                _mpdUri);
            player.SetStreamProvider(dashStreamProvider);
            return player;
        }
    }
}