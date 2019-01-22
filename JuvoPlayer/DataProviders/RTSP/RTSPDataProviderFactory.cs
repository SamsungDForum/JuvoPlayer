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
using JuvoPlayer.Demuxers;
using JuvoPlayer.Demuxers.FFmpeg;

namespace JuvoPlayer.DataProviders.RTSP
{
    public class RTSPDataProviderFactory : IDataProviderFactory
    {
        public IDataProvider Create(ClipDefinition clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip), "clip cannot be null");
            }

            if (!SupportsClip(clip))
            {
                throw new ArgumentException("unsupported clip type");
            }

            var rtspClient = new RTSPClient();
            var demuxer = new FFmpegDemuxer(new FFmpegGlue());
            var demuxerController = new DemuxerController(demuxer);
            demuxerController.SetDataSource(rtspClient.ChunkReady());

            return new RTSPDataProvider(demuxerController, rtspClient, clip);
        }

        public bool SupportsClip(ClipDefinition clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip), "clip cannot be null");
            }

            return string.Equals(clip.Type, "Rtp", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(clip.Type, "Rtsp", StringComparison.CurrentCultureIgnoreCase);
        }
    }
}