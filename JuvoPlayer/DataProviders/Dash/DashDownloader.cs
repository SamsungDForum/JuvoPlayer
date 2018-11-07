// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using JuvoLogger;
using JuvoPlayer.Common;
using System;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Utils;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class ByteRange
    {
        private static readonly string Tag = "JuvoPlayer";
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);
        public long Low { get; }
        public long High { get; }

        public ByteRange(string range)
        {
            Low = 0;
            High = 0;
            var ranges = range.Split('-');
            if (ranges.Length != 2)
                throw new ArgumentException("Range cannot be parsed.");

            try
            {
                Low = long.Parse(ranges[0]);
                High = long.Parse(ranges[1]);

                if (Low > High)
                    throw new ArgumentException("Range Low param cannot be higher then High param");
            }
            catch (Exception ex)
            {
                Logger.Error(ex + " Cannot parse range.");
            }
        }

        public override string ToString()
        {
            return $"{Low}-{High}";
        }
    }

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
            var request = (HttpWebRequest) base.GetWebRequest(address);
            if (request != null)
            {
                request.Accept = "*/*";
                if (to != null && from != null)
                {
                    request.AddRange((int) from, (int) to);
                }
            }

            return request;
        }
    }

    /// <summary>
    /// Download request data structure. Will be passed back to download handlers along with
    /// data.
    /// </summary>
    internal class DownloadRequest
    {
        public MpdParser.Node.Dynamic.Segment DownloadSegment { get; set; }
        public bool IgnoreError { get; set; }
        public uint? SegmentId { get; set; }
        public StreamType StreamType { get; set; }
    }

    internal class DownloadResponse
    {
        public MpdParser.Node.Dynamic.Segment DownloadSegment { get; set; }
        public uint? SegmentId { get; set; }
        public StreamType StreamType { get; set; }
        public byte[] Data { get; set; }
    }

    internal class DashDownloaderException : Exception
    {
        public DownloadRequest DownloadRequest { get; }

        public DashDownloaderException(DownloadRequest request, string message, Exception inner) : base(message, inner)
        {
            DownloadRequest = request;
        }
    }

    /// <summary>
    /// Download request class for handling download requests.
    /// </summary>
    internal class DashDownloader
    {
        private const string Tag = "JuvoPlayer";

        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private CancellationToken cancellationToken;

        private readonly ByteRange downloadRange;
        private readonly DownloadRequest request;
        private readonly IThroughputHistory throughputHistory;

        private int downloadErrorCount;

        private DashDownloader(DownloadRequest downloadRequest, CancellationToken cancellationToken,
            IThroughputHistory throughputHistory)
        {
            request = downloadRequest;

            if (!string.IsNullOrEmpty(request.DownloadSegment.ByteRange))
                downloadRange = new ByteRange(request.DownloadSegment.ByteRange);

            this.cancellationToken = cancellationToken;
            this.throughputHistory = throughputHistory;
        }

        public static Task<DownloadResponse> DownloadDataAsync(DownloadRequest downloadRequest,
            CancellationToken cancellationToken, IThroughputHistory throughputHistory)
        {
            var dashDownloader = new DashDownloader(downloadRequest, cancellationToken, throughputHistory);

            return dashDownloader.DownloadInternalAsync();
        }

        private async Task<DownloadResponse> DownloadInternalAsync()
        {
            var segmentId = SegmentId(request.SegmentId);
            Exception lastException;

            do
            {
                try
                {
                    await Task.Delay(CalculateSleepTime(), cancellationToken);

                    return await DownloadDataTaskAsync();
                }
                catch (WebException e)
                {
                    Logger.Warn($"{request.StreamType}: Segment: {segmentId} NetError: {e.Message}");

                    lastException = e;
                    cancellationToken.ThrowIfCancellationRequested();

                    if (e.InnerException is OperationCanceledException)
                        ExceptionDispatchInfo.Capture(e.InnerException).Throw();

                    ++downloadErrorCount;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    lastException = e;
                    Logger.Warn($"{request.StreamType}: Segment: {segmentId} Error: {e.Message}");
                    ++downloadErrorCount;
                }
            } while (!request.IgnoreError && downloadErrorCount < 3);

            var message = $"Cannot download segment: {segmentId}.";

            throw new DashDownloaderException(request, message, lastException);
        }

        private async Task<DownloadResponse> DownloadDataTaskAsync()
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var dataDownloader = new WebClientEx())
            using (cancellationToken.Register(dataDownloader.CancelAsync))
            {
                if (downloadRange != null)
                    dataDownloader.SetRange(downloadRange.Low, downloadRange.High);

                cancellationToken.ThrowIfCancellationRequested();

                Logger.Info(
                    $"{request.StreamType}: Segment: {SegmentId(request.SegmentId)} Requested. URL: {request.DownloadSegment.Url} Range: {downloadRange}");

                TimeSpan time;
                byte[] data;
                using (new TimeCounter(t => time = t))
                {
                    data = await dataDownloader.DownloadDataTaskAsync(request.DownloadSegment.Url).ConfigureAwait(false);
                }

                throughputHistory.Push(data.Length, time);

                return new DownloadResponse
                {
                    StreamType = request.StreamType,
                    DownloadSegment = request.DownloadSegment,
                    SegmentId = request.SegmentId,
                    Data = data
                };
            }
        }

        private int CalculateSleepTime()
        {
            // sleep at least 1ms to make delay async
            if (!request.IgnoreError || downloadErrorCount == 0)
                return 1;

            // Exponential backoff
            // Sleep:
            // 0-100ms for 1st error (DownloadErrorCount = 1)
            // 100-300ms for 2nd error (DownloadErrorCount = 2)
            // 200-700ms for 3rd error (DownloadErrorCount = 3)
            // ... and so on
            var rnd = new Random();

            var sleepTime = rnd.Next(
                                (downloadErrorCount - 1) * 100 + 1,
                                (downloadErrorCount * downloadErrorCount - (downloadErrorCount - 1)) * 100) + 1;

            return sleepTime;
        }

        private static string SegmentId(uint? segmentId)
        {
            return segmentId.HasValue ? segmentId.ToString() : "INIT";
        }
    }
}