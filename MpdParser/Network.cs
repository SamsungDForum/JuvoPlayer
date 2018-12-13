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
using System.Net;
using System.IO;
using JuvoLogger;

namespace MpdParser.Network
{
    public class ByteRange
    {
        protected static LoggerManager LogManager = LoggerManager.GetInstance();
        protected static ILogger Logger = LoggerManager.GetInstance().GetLogger(MpdParser.LogTag);

        public long Low { get; }
        public long High { get; }

        public ByteRange(string range)
        {
            var ranges = range.Split('-');
            if (ranges.Length != 2)
            {
                throw new ArgumentException("Invalid range");
            }
            try
            {
                Low = long.Parse(ranges[0]);
                High = long.Parse(ranges[1]);

                if (Low > High)
                {
                    throw new ArgumentException("Range Low param cannot be higher than High param");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Cannot parse range \"{range}\"");
            }
        }

        public static ByteRange FromString(string range)
        {
            if (range == null)
            {
                return null;
            }
            return new ByteRange(range);
        }

        public override string ToString() { return $"{Low}-{High}"; }
    }

    public class Downloader
    {
        public static byte[] DownloadData(Uri address, ByteRange range = null)
        {
            var request = HttpWebRequest.CreateDefault(address) as HttpWebRequest;

            request.AllowAutoRedirect = true;
            request.Timeout = _timeoutMs;
            if (range != null)
            {
                request.AddRange(range.Low, range.High);
            }

            var response = request.GetResponse() as HttpWebResponse;
            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent)
            {
                throw new WebException($"{address} [{range}] returned HTTP {response.StatusCode}");
            }

            var len = Convert.ToInt32(response.Headers["Content-Length"]);
            using (Stream stream = response.GetResponseStream(), mem = new MemoryStream(len != 0 ? len : avgDownloadSize))
            {
                stream.CopyTo(mem);
                return ((MemoryStream) mem).ToArray();
            }
        }

        //seems like a good default that won't drop data on slow-ish connections, yet not frustrate the user with wait times
        private static int _timeoutMs = (int)TimeSpan.FromSeconds(3).TotalMilliseconds;
        private const Int32 avgDownloadSize = 1024;
    }
}