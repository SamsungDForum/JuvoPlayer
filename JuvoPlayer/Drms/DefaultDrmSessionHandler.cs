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

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace JuvoPlayer.Drms
{
    public class DefaultDrmSessionHandler : IDrmSessionHandler
    {
        private readonly string _licenseServerUrl;
        private readonly Dictionary<string, string> _requestHeaders;
        private static readonly HttpClient HttpClient = new HttpClient();

        public DefaultDrmSessionHandler(
            string licenseServerUrl,
            Dictionary<string, string> requestHeaders)
        {
            _licenseServerUrl = licenseServerUrl;
            _requestHeaders = requestHeaders;
        }

        public DefaultDrmSessionHandler(string licenseServerUrl)
        {
            _licenseServerUrl = licenseServerUrl;
        }

        public virtual async Task<byte[]> AcquireLicense(
            string sessionId,
            byte[] requestData)
        {
            using (var content = new ByteArrayContent(requestData))
            {
                if (_requestHeaders != null)
                {
                    foreach (var requestHeader in _requestHeaders)
                    {
                        var requestKey = requestHeader.Key;
                        var requestValue = requestHeader.Value;
                        content.Headers.Add(
                            requestKey,
                            requestValue);
                    }
                }

                using (var response = await HttpClient.PostAsync(
                    _licenseServerUrl,
                    content))
                {
                    var responseContent = response.Content;
                    return await responseContent.ReadAsByteArrayAsync();
                }
            }
        }
    }
}