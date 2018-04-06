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
using System.Collections.Generic;
using System.Net;
using System.Text;
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
            {
                throw new ArgumentException("Range cannot be parsed.");
            }
            try
            {
                Low = long.Parse(ranges[0]);
                High = long.Parse(ranges[1]);

                if (Low > High)
                {
                    throw new ArgumentException("Range Low param cannot be higher then High param");
                }

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

    /// <summary>
    /// Download request class for handling download requests.
    /// </summary>
    public class DownloadRequest : IDisposable
    {
        private const string Tag = "JuvoPlayer";

        /// <summary>
        /// WebClient Task as returned by Async requests.
        /// </summary>
        public Task<byte[]> DownloadTask { get; internal set; }

        /// <summary>
        /// Request Task - Task which downloads data in asynchronous way.
        /// </summary>
        public Task RequestTask { get; internal set; }

        /// <summary>
        /// Download request data associated with this instance of request.
        /// </summary>
        public DownloadRequestData requestData { get; internal set; }

        private ByteRange downloadRange;
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        /// <summary>
        /// DownloadTask state handlers. Will be called, Task State Dependant, when Process() is executed
        /// </summary>
        public Action<DownloadRequestData> Created { get; set; }
        public Action<DownloadRequestData> WaitingForActivation { get; set; }
        public Action<DownloadRequestData> WaitingToRun { get; set; }
        public Action<DownloadRequestData> Running { get; set; }
        public Action<DownloadRequestData> WaitingForChildrenToComplete { get; set; }
        public Action<byte[], DownloadRequestData> RanToCompletion { get; set; }
        public Action<DownloadRequestData> Canceled { get; set; }
        public Action<Exception, DownloadRequestData, bool> Faulted { get; set; }
        public Action<Exception, DownloadRequestData, bool> RequestFailed { get; set; }
        public EventWaitHandle CompletionNotifier { get; set; }

        /// <summary>
        /// Download error counter
        /// </summary>
        public int DownloadErrorCount { get; internal set; } = 0;

        /// <summary>
        /// Flag passed to fail handler idicating desired behavior on error.
        /// </summary>
        private bool ignoreError;

        /// <summary>
        /// Creates a Download Request object
        /// </summary>
        /// <param name="downloadRequest">Download Request Object</param>
        /// <param name="downloadErrorIgnore">Ignore/Process download errors</param>
        public DownloadRequest(DownloadRequestData downloadRequest, bool downloadErrorIgnore)
        {

            requestData = downloadRequest;

            if (!String.IsNullOrEmpty(requestData.DownloadSegment.ByteRange))
                downloadRange = new ByteRange(requestData.DownloadSegment.ByteRange);

            ignoreError = downloadErrorIgnore;

        }

        /// <summary>
        /// External API for issuing a download. Calls internal Task method to preforma actual
        /// work related with download startup.
        /// </summary>
        public void Download()
        {
            RequestTask = Task.Run(() => DownloadInternal());
        }

        private async Task DownloadInternal()
        {
            // I am opened to suggestions on WebClient reuse.
            // Initail test show it to be... unreliable with tendency to randomly "crash"
            // and refuse to service any further requests.
            //
            // TO CHECK: Do so only if download failes, in case of internal class failure
            // kill & creatre new?
            //
            using (var dataDownloader = new WebClientEx())
            {
                if (downloadRange != null)
                {
                    dataDownloader.SetRange(downloadRange.Low, downloadRange.High);
                }


                // In case of download errors...
                //
                if (DownloadErrorCount != 0)
                {
                    // Exponential backoff
                    // Sleep:
                    // 0-100ms for 1st error (DownloadErrorCount = 1)
                    // 100-300ms for 2nd error (DownloadErrorCount = 2)
                    // 200-700ms for 3rd error (DownloadErrorCount = 3)
                    // ... and so on
                    var rnd = new Random();

                    var sleepTime = rnd.Next(
                    (DownloadErrorCount - 1) * 100,
                    ((DownloadErrorCount * DownloadErrorCount) - (DownloadErrorCount - 1)) * 100);

                    await Task.Delay(sleepTime);

                    //Check for being disposed. If so, do not proceed.
                    if (disposedValue == true)
                    {
                        Logger.Warn($"{requestData.StreamType}: Downloader Disposed. Segment: {requestData.SegmentID} Requested. URL: {requestData.DownloadSegment.Url}.");
                        return;
                    }
                }

                // In case of reloads, get rid off previous DownloadTask.
                DownloadTask?.Dispose();

                try
                {

                    DownloadTask = dataDownloader.DownloadDataTaskAsync(requestData.DownloadSegment.Url);

                    Logger.Info($"{requestData.StreamType}: Segment: {requestData.SegmentID} Requested. URL: {requestData.DownloadSegment.Url} Range: {downloadRange?.ToString()}");

                    await DownloadTask;


                }
                catch { } //Dummy catcher. Error handling is done during common Process of all awaiters
                finally
                {
                    //signall Completion Notifier that we're done.
                    CompletionNotifier?.Set();
                }

            }
        }

        /// <summary>
        /// process download task based on its current status through
        /// state handlers.
        /// </summary>
        /// <returns>Current download task status.</returns>
        public TaskStatus? Process()
        {
            // There is no need to check for disposing state as dispose should never happen in parallel
            // to calling Process()

            TaskStatus? res = RequestTask?.Status;

            // Check for failures at request task.
            if (res == TaskStatus.Faulted)
            {
                DownloadErrorCount++;
                RequestFailed?.Invoke(RequestTask.Exception, requestData, ignoreError);
                return RequestTask?.Status;
            }

            // Wait for request to complete - there's a wait on DownloadRequest. We do not
            // want that to bomb out if we remove task while thread hasn't processed the Wait.
            if (res < TaskStatus.RanToCompletion)
                return res;

            //Request task ok. Check download task status.
            res = DownloadTask?.Status;
            switch (res)
            {
                case TaskStatus.Created:
                    Created?.Invoke(requestData);
                    break;
                case TaskStatus.WaitingForActivation:
                    WaitingForActivation?.Invoke(requestData);
                    break;
                case TaskStatus.WaitingToRun:
                    WaitingToRun?.Invoke(requestData);
                    break;
                case TaskStatus.Running:
                    WaitingToRun?.Invoke(requestData);
                    break;
                case TaskStatus.WaitingForChildrenToComplete:
                    WaitingForChildrenToComplete?.Invoke(requestData);
                    break;
                case TaskStatus.RanToCompletion:
                    RanToCompletion?.Invoke(DownloadTask.Result, requestData);
                    break;
                case TaskStatus.Canceled:
                    Canceled?.Invoke(requestData);
                    break;
                case TaskStatus.Faulted:
                    DownloadErrorCount++;
                    Faulted?.Invoke(DownloadTask.Exception.InnerException, requestData, ignoreError);
                    break;
                default:
                    //Do nothing. Most likely task not created yet.
                    break;
            }

            return res;

        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        DownloadTask?.Dispose();
                    }
                    catch (Exception)
                    {

                    }
                }
                DownloadTask = null;
                RequestTask = null;

                Created = null;
                WaitingForActivation = null;
                WaitingToRun = null;
                Running = null;
                WaitingForChildrenToComplete = null;
                RanToCompletion = null;
                Canceled = null;
                Faulted = null;

                downloadRange = null;
                requestData = null;

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
