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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;

namespace JuvoPlayer.Dash
{
    public class InitializationChunk : IChunk
    {
        private readonly CancellationToken _cancellationToken;
        private readonly IDownloader _downloader;
        private readonly string _initUri;
        private readonly long? _length;
        private readonly RepresentationWrapper _representationWrapper;
        private readonly long? _start;
        private readonly IThroughputHistory _throughputHistory;
        private readonly DashDemuxerDataSource _demuxerDataSource;
        private SegmentBuffer _segmentBuffer;
        private int _downloaded;

        public InitializationChunk(
            string uri,
            long? start,
            long? length,
            RepresentationWrapper wrapper,
            IDownloader downloader,
            IThroughputHistory throughputHistory,
            DashDemuxerDataSource demuxerDataSource,
            CancellationToken cancellationToken)
        {
            _initUri = uri;
            _start = start;
            _length = length;
            _representationWrapper = wrapper;
            _downloader = downloader;
            _throughputHistory = throughputHistory;
            _demuxerDataSource = demuxerDataSource;
            _cancellationToken = cancellationToken;
        }

        public async Task Load()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                Log.Info($"{_initUri} {_start} {_length} starts");
                _representationWrapper.InitData = new List<byte[]>();
                await _downloader.Download(
                    _initUri,
                    _start,
                    _length,
                    HandleChunkDownloaded,
                    _throughputHistory,
                    _cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _representationWrapper.InitData = null;
                throw;
            }
            finally
            {
                if (_segmentBuffer != null)
                {
                    _segmentBuffer.CompleteAdding();
                    _demuxerDataSource.Offset += _downloaded;
                }

                Log.Info($"{_initUri} {_start} {_length} ends. " +
                         $"Loading took {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        private void HandleChunkDownloaded(byte[] bytes)
        {
            _representationWrapper.InitData.Add(bytes);
            if (_segmentBuffer == null)
            {
                _segmentBuffer = new SegmentBuffer();
                _demuxerDataSource.AddSegmentBuffer(_segmentBuffer);
            }

            _downloaded += bytes.Length;
            _segmentBuffer.Add(bytes);
        }
    }
}
