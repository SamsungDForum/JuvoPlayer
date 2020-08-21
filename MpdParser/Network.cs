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
using System.Linq;
using System.Net;
using System.Net.Http;
using JuvoLogger;
using Polly;

namespace MpdParser.Network
{
    public class ByteRange
    {
        protected static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

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

    internal static class HttpClientProvider
    {
        public static HttpClient NetClient { get; }

        private static readonly HttpStatusCode[] RetryHttpCodes =
        {
            HttpStatusCode.RequestTimeout, // 408
            HttpStatusCode.InternalServerError, // 500
            HttpStatusCode.BadGateway, // 502
            HttpStatusCode.ServiceUnavailable, // 503
            HttpStatusCode.GatewayTimeout // 504
        };

        private static readonly Random Jitter;

        static HttpClientProvider()
        {
            NetClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(3)
            };

            Jitter = new Random();
        }
        public static IAsyncPolicy<HttpResponseMessage> GetPolicySendAsync(Func<Exception, bool> exceptionHandler)
        {

            return Policy
                .Handle(exceptionHandler)
                .OrResult<HttpResponseMessage>(hrm => RetryHttpCodes.Contains(hrm.StatusCode))
                .WaitAndRetryAsync(
                    3,
                    attempt => (TimeSpan.FromMilliseconds(Jitter.Next(0, 250)) +
                                TimeSpan.FromMilliseconds(50 * attempt))
                );
        }
    }
}
