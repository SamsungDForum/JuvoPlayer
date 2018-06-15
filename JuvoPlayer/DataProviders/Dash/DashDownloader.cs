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

    public class WebClientEx : WebClient
    {
        private static readonly TimeSpan WebRequestTimeout = TimeSpan.FromSeconds(10);

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
                request.Timeout = (int)WebRequestTimeout.TotalMilliseconds;
                if (to != null && from != null)
                {
                    request.AddRange((int)from, (int)to);
                }
            }
            return request;
        }

    }
    /// <summary>
    /// Download request data structure. Will be passed back to download handlers along with
    /// data.
    /// </summary>
    public class DownloadRequestData
    {
        public MpdParser.Node.Dynamic.Segment DownloadSegment { get; set; }
        public uint? SegmentID { get; set; }
        public StreamType StreamType { get; set; }
    }

    public class DownloadResponse
    {
        public MpdParser.Node.Dynamic.Segment DownloadSegment { get; set; }
        public uint? SegmentID { get; set; }
        public StreamType StreamType { get; set; }
        public byte[] Data { get; set; }
        
    }

    /// <summary>
    /// Download request class for handling download requests.
    /// </summary>
    public class DownloadRequest
    {
        private const string Tag = "JuvoPlayer";

        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private CancellationToken cancellationToken;
        private readonly DownloadRequestData requestData;
        private readonly ByteRange downloadRange;

        private int downloadErrorCount;
        private readonly bool ignoreError;

        private DownloadRequest(DownloadRequestData downloadRequest, bool downloadErrorIgnore, CancellationToken cancellationToken)
        {
            requestData = downloadRequest;

            if (!string.IsNullOrEmpty(requestData.DownloadSegment.ByteRange))
                downloadRange = new ByteRange(requestData.DownloadSegment.ByteRange);

            ignoreError = downloadErrorIgnore;

            this.cancellationToken = cancellationToken;
        }

        public static Task<DownloadResponse> CreateDownloadRequestAsync(DownloadRequestData downloadRequestData, bool downloadErrorIgnore, CancellationToken cancellationToken)
        {
            var downloadRequest = new DownloadRequest(downloadRequestData, downloadErrorIgnore, cancellationToken);

            return downloadRequest.DownloadAsync();
        }

        private async Task<DownloadResponse> DownloadAsync()
        {
            string segmentID = SegmentID(requestData.SegmentID);
            do
            {
                try
                {
                    await Task.Delay(CalculateSleepTime(), cancellationToken);
                    return await DownloadDataTaskAsync();
                }
                catch (WebException e)
                {
                    if (e.InnerException is TaskCanceledException)
                        ExceptionDispatchInfo.Capture(e.InnerException).Throw();

                    Logger.Warn($"{requestData.StreamType}: Segment: {segmentID} NetError: {e.Message}");
                    ++downloadErrorCount;
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger.Warn($"{requestData.StreamType}: Segment: {segmentID} Error: {e.Message}");
                    ++downloadErrorCount;
                }
            } while (ignoreError && downloadErrorCount < 3);

            throw new Exception($"{requestData.StreamType}: Segment: {segmentID} Max retry count reached."); ;   
        }

        private async Task<DownloadResponse> DownloadDataTaskAsync()
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var dataDownloader = new WebClientEx())
            {
                if (downloadRange != null)
                    dataDownloader.SetRange(downloadRange.Low, downloadRange.High);

                using (cancellationToken.Register(dataDownloader.CancelAsync))
                {
                    Logger.Info($"{requestData.StreamType}: Segment: {SegmentID(requestData.SegmentID)} Requested. URL: {requestData.DownloadSegment.Url} Range: {downloadRange}");

                    cancellationToken.ThrowIfCancellationRequested();

                    return new DownloadResponse
                    {
                        StreamType = requestData.StreamType,
                        DownloadSegment = requestData.DownloadSegment,
                        SegmentID = requestData.SegmentID,
                        Data = await dataDownloader.DownloadDataTaskAsync(requestData.DownloadSegment.Url)
                    };
                }
            }
        }

        private int CalculateSleepTime()
        {
            if (ignoreError || downloadErrorCount == 0)
                return 0;

            // Exponential backoff
            // Sleep:
            // 0-100ms for 1st error (DownloadErrorCount = 1)
            // 100-300ms for 2nd error (DownloadErrorCount = 2)
            // 200-700ms for 3rd error (DownloadErrorCount = 3)
            // ... and so on
            var rnd = new Random();

            var sleepTime = rnd.Next(
                (downloadErrorCount - 1) * 100,
                (downloadErrorCount * downloadErrorCount - (downloadErrorCount - 1)) * 100);

            return sleepTime;
        }

        private static string SegmentID(uint? segmentId)
        {
            return segmentId.HasValue ? segmentId.ToString() : "INIT";
        }
 
    }
}

