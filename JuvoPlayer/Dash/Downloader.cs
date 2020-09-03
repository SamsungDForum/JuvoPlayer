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
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using Overby.Extensions.AsyncBinaryReaderWriter;

namespace JuvoPlayer.Dash
{
    public class Downloader : IDownloader
    {
        private const int ChunkSize = 64 * 1024;
        private static readonly HttpClient HttpClient = new HttpClient();

        public async Task Download(
            string uri,
            long? start,
            long? length,
            Action<byte[]> onChunkDownloaded,
            IThroughputHistory throughputHistory,
            CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var totalBytesReceived = 0;
            try
            {
                var requestMessage =
                    CreateHttpRequestMessage(uri, start, length);
                using (var response =
                    await HttpClient.SendAsync(
                        requestMessage,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    var content = response.Content;
                    using (var stream = await content.ReadAsStreamAsync())
                    using (var reader = new AsyncBinaryReader(stream))
                    {
                        while (true)
                        {
                            var chunk =
                                await reader.ReadBytesAsync(
                                    ChunkSize,
                                    cancellationToken);
                            if (chunk.Length == 0)
                                return;
                            cancellationToken.ThrowIfCancellationRequested();
                            onChunkDownloaded.Invoke(chunk);
                            totalBytesReceived += chunk.Length;
                        }
                    }
                }
            }
            finally
            {
                throughputHistory.Push(totalBytesReceived, watch.Elapsed);
            }
        }

        public async Task<byte[]> Download(
            string uri,
            long? start,
            long? length,
            IThroughputHistory throughputHistory,
            CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            var requestMessage =
                CreateHttpRequestMessage(uri, start, length);
            using (var response =
                await HttpClient.SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var content = response.Content;
                var bytes = await content.ReadAsByteArrayAsync();
                var totalBytesReceived = bytes.Length;
                throughputHistory.Push(totalBytesReceived, watch.Elapsed);
                return bytes;
            }
        }

        private static HttpRequestMessage CreateHttpRequestMessage(
            string uri,
            long? start,
            long? length)
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
            if (start.HasValue && length.HasValue)
                httpRequest.Headers.Range = new RangeHeaderValue(start, start + length - 1);
            return httpRequest;
        }
    }
}
