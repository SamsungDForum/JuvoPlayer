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
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Demuxers.FFmpeg;
using System.Runtime.InteropServices;

namespace JuvoPlayer.DataProviders.Dash
{
    public class DashDataProviderFactory : IDataProviderFactory
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        public IDataProvider Create(ClipDefinition clip)
        {
            Logger.Info("Create.");
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip), "Clip cannot be null.");
            }

            if (!SupportsClip(clip))
            {
                throw new ArgumentException("Unsupported clip type.");
            }

            var audioPipeline = CreateMediaPipeline(StreamType.Audio);
            audioPipeline.DisableAdaptiveStreaming = true;
            var videoPipeline = CreateMediaPipeline(StreamType.Video);
            //For sake of the TV emulator performance it's important to stick with the lowest representation that is available.
            videoPipeline.DisableAdaptiveStreaming = (RuntimeInformation.ProcessArchitecture == Architecture.X86);

            return new DashDataProvider(clip.Url, audioPipeline, videoPipeline);
        }

        private static DashMediaPipeline CreateMediaPipeline(StreamType streamType)
        {
            var throughputHistory = new ThroughputHistory();
            var dashClient = new DashClient(throughputHistory, streamType);
            var controller = new DemuxerController(new FFmpegDemuxer(new FFmpegGlue()));
            controller.SetDataSource(dashClient.ChunkReady());

            return new DashMediaPipeline(dashClient, controller, throughputHistory, streamType);
        }

        public bool SupportsClip(ClipDefinition clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip), "Clip cannot be null!");
            }

            return string.Equals(clip.Type, "Dash", StringComparison.CurrentCultureIgnoreCase);
        }
    }
}