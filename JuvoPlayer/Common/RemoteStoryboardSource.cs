/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
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
 *
 */

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using JuvoPlayer.Utils;

namespace JuvoPlayer.Common
{
    class RemoteStoryboardSource : IStoryboardSource
    {
        private readonly Uri _remotePath;
        private static readonly HttpClient HttpClient = new HttpClient();

        public RemoteStoryboardSource(string remotePath)
        {
            _remotePath = new Uri(remotePath);
        }

        public async Task<StoryboardsMap> GetStoryboardsMap()
        {
            using (var response = await HttpClient.GetAsync(_remotePath))
            {
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JSONFileReader.DeserializeJsonText<StoryboardsMap>(content);
            }
        }

        public async Task<Stream> GetBitmap(Storyboard storyboard)
        {
            var response = await HttpClient.GetAsync(ResolveUri(storyboard));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        private Uri ResolveUri(Storyboard storyboard)
        {
            return new Uri(_remotePath, storyboard.Filename);
        }
    }
}