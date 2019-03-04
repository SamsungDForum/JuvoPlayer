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
using JuvoLogger;

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

    // TODO: This class is a copy of WebClientEx in DashDownloader
    // TODO: Consider moving cross application common classes to a separate project
    // TODO: so they can be used across application without too much dependency hell.
    internal class WebClientEx : WebClient
    {
        private long? from;
        private long? to;

        public void SetRange(long from, long to)
        {
            this.from = from;
            this.to = to;
        }

        public void ClearRange()
        {
            from = null;
            to = null;
        }

        public ulong GetBytes(Uri address)
        {
            OpenRead(address.ToString());
            return Convert.ToUInt64(ResponseHeaders["Content-Length"]);
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = (HttpWebRequest)base.GetWebRequest(address);
            if (request != null)
            {
                request.Accept = "*/*";
                if (to != null && from != null)
                {
                    request.AddRange(from.Value, to.Value);
                }
            }

            return request;
        }
    }
}
