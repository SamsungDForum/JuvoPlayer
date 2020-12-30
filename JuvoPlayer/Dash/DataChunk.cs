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

using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;

namespace JuvoPlayer.Dash
{
    public class DataChunk : IChunk
    {
        private readonly CancellationToken _cancellationToken;
        private readonly IDemuxer _demuxer;
        private readonly DashDemuxerClient _demuxerClient;
        private readonly IDownloader _downloader;
        private readonly long? _length;
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly long? _start;
        private readonly IThroughputHistory _throughputHistory;
        private readonly string _uri;
        private SegmentBuffer _segmentBuffer;
        private int _downloaded;

        public DataChunk(
            string uri,
            long? start,
            long? length,
            long segmentNum,
            IDownloader downloader,
            IThroughputHistory throughputHistory,
            IDemuxer demuxer,
            DashDemuxerClient demuxerClient,
            CancellationToken cancellationToken)
        {
            _uri = uri;
            _start = start;
            _length = length;
            SegmentNum = segmentNum;
            _downloader = downloader;
            _throughputHistory = throughputHistory;
            _demuxer = demuxer;
            _demuxerClient = demuxerClient;
            _cancellationToken = cancellationToken;
        }

        public long SegmentNum { get; }

        public async Task Load()
        {
            _logger.Info($"{_uri} {_start} {_length} starts");
            try
            {
                await _downloader.Download(
                    _uri,
                    _start,
                    _length,
                    HandleChunkDownloaded,
                    _throughputHistory,
                    _cancellationToken);
            }
            finally
            {
                if (_segmentBuffer != null)
                {
                    _segmentBuffer.CompleteAdding();
                    _demuxerClient.Offset += _downloaded;
                }

                _logger.Info($"{_uri} {_start} {_length} ends");
            }
        }

        private void HandleChunkDownloaded(byte[] bytes)
        {
            if (_segmentBuffer == null)
            {
                _segmentBuffer = new SegmentBuffer();
                _demuxerClient.AddSegmentBuffer(_segmentBuffer);
            }

            _downloaded += bytes.Length;
            _segmentBuffer.Add(bytes);
        }
    }
}
