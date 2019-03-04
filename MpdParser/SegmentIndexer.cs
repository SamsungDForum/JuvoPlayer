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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using MpdParser.Network;
using MpdParser.Node.Atom;

namespace MpdParser.Node.Dynamic
{
    public class SegmentIndexerException : Exception
    {
        public SegmentIndexerException()
        {
        }

        public SegmentIndexerException(string message) : base(message)
        {
        }

        public SegmentIndexerException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    internal class SegmentIndexer
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private static readonly Random DelayGenerator = new Random();
        private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(3);

        public List<Segment> Segments { get; set; } = new List<Segment>();
        public TimeSpan Duration { get; set; } = TimeSpan.Zero;

        public static Task<SegmentIndexer> Initialize(Segment indexSource, Uri mediaUrl, CancellationToken token)
        {
            if (indexSource == null)
                throw new ArgumentNullException(nameof(indexSource));

            return Task.Factory.StartNew(
                async () =>
                    await SegmentIndexer.ExecuteInitialize(indexSource, mediaUrl, token)
                        .ConfigureAwait(false), token)
                        .Unwrap();
        }

        private static async Task<SegmentIndexer> ExecuteInitialize(Segment indexSource, Uri mediaUrl, CancellationToken token)
        {
            var si = new SegmentIndexer();

            ByteRange range = indexSource.ByteRange.Length > 0 ?
                new ByteRange(indexSource.ByteRange) : null;

            var retryCount = 0;
            byte[] rawIndexData = null;

            do
            {
                Logger.Info($"Downloading Index Segment #{retryCount} {indexSource.Url} {range}");

                try
                {
                    using (var webClient = new WebClientEx())
                    using (var downloadTermination = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        var registrationCb =
                            downloadTermination.Token.Register((o) => { (o as WebClientEx)?.CancelAsync(); },
                                webClient);

                        if (range != null)
                            webClient.SetRange(range.Low, range.High);

                        downloadTermination.CancelAfter(DownloadTimeout);

                        rawIndexData = await webClient.DownloadDataTaskAsync(indexSource.Url).ConfigureAwait(false);

                        registrationCb.Dispose();

                        // Cancelled externally. Return "empty" Stream Indexer
                        if (token.IsCancellationRequested)
                            return si;

                        downloadTermination.Token.ThrowIfCancellationRequested();
                    }

                    break;
                }
                catch (WebException we)
                {
                    var errorCode = (we.Response as HttpWebResponse)?.StatusCode;
                    if (errorCode == HttpStatusCode.NotFound)
                    {
                        Logger.Error(we);
                        throw new SegmentIndexerException("Index Segment download failure", we);
                    }

                }
                catch (OperationCanceledException oce)
                {
                    Logger.Warn($"{indexSource.Url} {oce}");
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    throw e;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(DelayGenerator.Next(100, 250))).ConfigureAwait(false);

            } while (retryCount++ < 3);

            if (rawIndexData == null)
                throw new SegmentIndexerException("Index Segment download failure");

            si.ProcessIndexData(rawIndexData, (ulong)(range?.High ?? 0), mediaUrl);

            if (si.Segments.Count > 0)
            {
                var lastPeriod = si.Segments[si.Segments.Count - 1].Period;
                si.Duration = lastPeriod.Start + lastPeriod.Duration;
            }

            Logger.Info($"Index {indexSource.Url} Entries {si.Segments.Count} Duration {si.Duration}");

            return si;
        }

        private void ProcessIndexData(byte[] data, ulong dataStart, Uri mediaUrl)
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

                    Segments.Add(new Segment(mediaUrl, rng, new TimeRange(startTime, duration)));
                }
            }
        }
    }
}
