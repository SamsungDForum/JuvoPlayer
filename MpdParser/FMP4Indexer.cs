/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using MpdParser.Network;
using MpdParser.Node.Atom;

namespace MpdParser.Node.Dynamic
{
    public class FMp4IndexerException : Exception
    {
        public FMp4IndexerException(string message) : base(message)
        {
        }

        public FMp4IndexerException(string message, Exception cause) : base(message, cause)
        {
        }
    }

    internal class FMp4Indexer
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public static async Task<IList<Segment>> Download(Segment indexSource, Uri mediaUrl, CancellationToken token)
        {
            var policy = HttpClientProvider.GetPolicySendAsync((e) =>
            {

                // All exception other then Cancellation due to timeout
                // result in termination
                if (!(e is OperationCanceledException))
                {
                    Logger.Error($"DownloadError {e.Message}");
                    return false;
                }

                // Check if timeout or external cancellation.
                // Both reported as OperationCancelledException
                if (token.IsCancellationRequested)
                {
                    Logger.Warn($"Download Cancelled: {indexSource.Url}");
                    return false;
                }

                Logger.Warn($"Download Timeout: {e.Message} {indexSource.Url}");
                return true;
            });

            long rangeHigh = 0;
            long rangeLow = 0;
            RangeHeaderValue ranges = null;

            if (indexSource.ByteRange.Length > 0)
            {
                var byteRanges = new ByteRange(indexSource.ByteRange);
                ranges = new RangeHeaderValue(byteRanges.Low, byteRanges.High);
                rangeHigh = byteRanges.High;
                rangeLow = byteRanges.Low;
            }

            try
            {
                byte[] rawIndexData;

                using (var response = await policy.ExecuteAsync(() =>
                {
                    Logger.Info($"Download {indexSource.Url} {rangeLow}-{rangeHigh} {rangeHigh - rangeLow}");

                    // Watch out for https://github.com/App-vNext/Polly/issues/313
                    using (var request = new HttpRequestMessage(HttpMethod.Get, indexSource.Url))
                    {
                        if (ranges != null)
                            request.Headers.Range = ranges;

                        return HttpClientProvider.NetClient.SendAsync(request, token);
                    }
                }))
                {
                    rawIndexData = await response.Content.ReadAsByteArrayAsync();
                }

                var index = ProcessIndexData(rawIndexData, (ulong)rangeHigh, mediaUrl);

                Logger.Info($"{index.Count} indexes {indexSource.Url}");

                return index;
            }
            catch (Exception e)
            {
                throw new FMp4IndexerException($"Index {indexSource.Url} failed", e);
            }
        }

        private static IList<Segment> ProcessIndexData(byte[] data, ulong dataStart, Uri mediaUrl)
        {
            var sidx = new SIDXAtom();
            sidx.ParseAtom(data, dataStart + 1);

            //SIDXAtom.SIDX_index_entry should contain a list of other sidx atoms containing
            //with index information. They could be loaded by updating range info in current
            //streamSegment and recursively calling DownloadIndexSegment - but about that we can worry later...
            //TO REMEMBER:
            //List of final sidxs should be "sorted" from low to high. In case of one it is not an issue,
            //it may be in case of N index boxes in hierarchy order (daisy chain should be ok too I think...)
            //so just do sanity check if we have such chunks
            if (sidx.SIDXIndexCount > 0)
            {
                throw new NotImplementedException("Daisy chained / Hierarchical chunks not implemented...");
            }

            var indexData = new List<Segment>();
            for (uint i = 0; i < sidx.MovieIndexCount; ++i)
            {
                ulong lb;
                ulong hb;
                TimeSpan startTime;
                TimeSpan duration;

                (lb, hb, startTime, duration) = sidx.GetRangeData(i);
                if (lb != hb)
                {
                    string rng = lb + "-" + hb;

                    indexData.Add(new Segment(mediaUrl, rng, new TimeRange(startTime, duration)));
                }
            }

            return indexData;
        }
    }
}
