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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using JuvoLogger;
using Polly;

namespace JuvoPlayer.ResourceLoaders
{
    public class HttpResource : IResource
    {
        private static readonly HttpClient HttpClient = new HttpClient {Timeout = TimeSpan.FromSeconds(10)};

        private static readonly HttpStatusCode[] HttpStatusCodesWorthRetrying =
        {
            HttpStatusCode.RequestTimeout, HttpStatusCode.InternalServerError, HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout
        };

        private readonly Uri _path;
        private bool _disposed;
        private Task<HttpResponseMessage> _responseTask;

        public HttpResource(string path)
        {
            _path = new Uri(path);
        }

        public HttpResource(Uri path)
        {
            _path = path;
        }

        public string AbsolutePath => _path.ToString();

        public async Task<Stream> ReadAsStreamAsync()
        {
            var response = await GetAsync();
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<string> ReadAsStringAsync()
        {
            var response = await GetAsync();
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public IResource Resolve(string path)
        {
            return new HttpResource(new Uri(_path, path));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            var response = _responseTask?.Status == TaskStatus.RanToCompletion ? _responseTask.Result : null;
            response?.Dispose();
        }

        private Task<HttpResponseMessage> GetAsync()
        {
            if (_responseTask != null)
                return _responseTask;
            const int retryCount = 2;
            _responseTask = Policy
                .Handle<OperationCanceledException>()
                .Or<HttpRequestException>()
                .OrResult<HttpResponseMessage>(r => HttpStatusCodesWorthRetrying.Contains(r.StatusCode))
                .RetryAsync(retryCount,
                    (result, i) =>
                    {
                        Log.Warn(
                            $"Cannot download {_path} due to {result.Exception.Message}, retry count {i} of {retryCount}");
                    })
                .ExecuteAsync(() => HttpClient.GetAsync(_path));
            return _responseTask;
        }
    }
}
