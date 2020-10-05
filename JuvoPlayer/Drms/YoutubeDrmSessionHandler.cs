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
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace JuvoPlayer.Drms
{
    public class YoutubeDrmSessionHandler : DefaultDrmSessionHandler
    {
        public YoutubeDrmSessionHandler(
            string licenseServerUrl,
            Dictionary<string, string> requestHeaders)
            : base(licenseServerUrl, requestHeaders)
        {
        }

        public YoutubeDrmSessionHandler(string licenseServerUrl)
            : base(licenseServerUrl)
        {
        }

        public override async Task<byte[]> AcquireLicense(string sessionId, byte[] requestData)
        {
            var response = await base.AcquireLicense(
                sessionId,
                requestData);
            var stringResponse = Encoding
                .GetEncoding(437)
                .GetString(response);
            if (!stringResponse.StartsWith("GLS/1.0 0 OK"))
                return response;
            const string headerMark = "\r\n\r\n";
            var headerMarkIndex = stringResponse.IndexOf(
                headerMark,
                StringComparison.Ordinal);
            if (headerMarkIndex > -1)
            {
                stringResponse = stringResponse.Substring(
                    headerMarkIndex + headerMark.Length);
            }

            return Encoding
                .GetEncoding(437)
                .GetBytes(stringResponse);
        }
    }
}