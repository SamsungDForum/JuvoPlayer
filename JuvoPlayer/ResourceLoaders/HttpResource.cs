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
using System.Net.Http;
using System.Threading.Tasks;

namespace JuvoPlayer.ResourceLoaders
{
    public class HttpResource : IResource
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        private readonly Uri _path;
        private Task<HttpResponseMessage> _responseTask;
        private bool _disposed;

        public HttpResource(string path)
        {
            _path = new Uri(path);
        }

        public HttpResource(Uri path)
        {
            _path = path;
        }

        public Task<Stream> ReadAsStreamAsync()
        {
            return Task.Run(async () =>
            {
                var response = await GetAsync();
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStreamAsync();
            });
        }

        public Task<string> ReadAsStringAsync()
        {
            return Task.Run(async () =>
            {
                var response = await GetAsync();
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            });
        }

        private Task<HttpResponseMessage> GetAsync()
        {
            if (_responseTask != null) return _responseTask;
            _responseTask = HttpClient.GetAsync(_path);
            return _responseTask;
        }

        public IResource Resolve(string path)
        {
            return new HttpResource(new Uri(_path, path));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var response = _responseTask?.Status == TaskStatus.RanToCompletion ? _responseTask.Result : null;
            response?.Dispose();
        }
    }
}