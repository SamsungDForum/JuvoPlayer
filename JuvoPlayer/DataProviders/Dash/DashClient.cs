using System;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.SharedBuffers;
using MpdParser.Node;
using MpdParser.Node.Dynamic;
using Representation = MpdParser.Representation;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashClient : IDashClient
    {
        private const string Tag = "JuvoPlayer";

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);
        private static readonly TimeSpan TimeBufferDepthDefault = TimeSpan.FromSeconds(10);
        private TimeSpan timeBufferDepth = TimeBufferDepthDefault;

        private readonly IThroughputHistory throughputHistory;
        private readonly ISharedBuffer sharedBuffer;
        private readonly StreamType streamType;

        private Representation currentRepresentation;
        private Representation newRepresentation;
        private TimeSpan currentTime = TimeSpan.Zero;
        private TimeSpan bufferTime = TimeSpan.Zero;
        private uint? currentSegmentId;

        private IRepresentationStream currentStreams;
        private TimeSpan? currentStreamDuration;

        private byte[] initStreamBytes;

        private Task processDataTask;
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Contains information about timing data for last requested segment
        /// </summary>
        private TimeRange lastRequestedPeriod = new TimeRange(TimeSpan.Zero, TimeSpan.Zero);

        /// <summary>
        /// Buffer full accessor.
        /// true - Underlying player received MagicBufferTime ammount of data
        /// false - Underlying player has at least some portion of MagicBufferTime left and can
        /// continue to accept data.
        /// 
        /// Buffer full is an indication of how much data (in units of time) has been pushed to the player.
        /// MagicBufferTime defines how much data (in units of time) can be pushed before Client needs to
        /// hold off further pushes. 
        /// TimeTicks (current time) received from the player are an indication of how much data (in units of time)
        /// player consumed.
        /// A difference between buffer time (data being pushed to player in units of time) and current tick time (currentTime)
        /// defines how much data (in units of time) is in the player and awaits presentation.
        /// </summary>
        private bool BufferFull => (bufferTime - currentTime) > timeBufferDepth;

        /// <summary>
        /// A shorthand for retrieving currently played out document type
        /// True - Content is dynamic
        /// False - Content is static.
        /// </summary>
        private bool IsDynamic => currentStreams.GetDocumentParameters().Document.IsDynamic;


        /// <summary>
        /// Notification event for informing dash pipeline that unrecoverable error
        /// has occoured.
        /// </summary>
        public event Error Error;

        public DashClient(IThroughputHistory throughputHistory, ISharedBuffer sharedBuffer,  StreamType streamType)
        {
            this.throughputHistory = throughputHistory ?? throw new ArgumentNullException(nameof(throughputHistory), "throughputHistory cannot be null");
            this.sharedBuffer = sharedBuffer ?? throw new ArgumentNullException(nameof(sharedBuffer), "sharedBuffer cannot be null");
            this.streamType = streamType;
        }

        public TimeSpan Seek(TimeSpan position)
        {
            currentSegmentId = currentStreams.SegmentId(position);
            var newTime = currentStreams.SegmentTimeRange(currentSegmentId)?.Start;

            // We are not expecting NULL segments after seek. 
            // Termination will occour after restarting 
            if(!currentSegmentId.HasValue || !newTime.HasValue)
            {
                LogError($"Seek Pos Req: {position} failed. No segment/TimeRange found");

                currentTime = position;
                newTime = position;
            }
            else
            {
                currentTime = newTime.Value;

            }
            LogInfo($"Seek Pos Req: {position} Seek to: ({newTime}) SegId: {currentSegmentId}");
            return newTime.Value;
        }

        public void Start()
        {
            if (currentRepresentation == null)
                throw new Exception("currentRepresentation has not been set");

            LogInfo("DashClient start.");
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();

            // clear garbage before appending new data
            sharedBuffer?.ClearData();

            bufferTime = currentTime;

            if (currentSegmentId.HasValue == false)
                currentSegmentId = currentStreams.StartSegmentId(currentTime, timeBufferDepth);

            var initSegment = currentStreams.InitSegment;
            if (initSegment != null)
                DownloadInitSegment(initSegment);
            else
                ScheduleNextSegDownload();
        }

        private void ScheduleNextSegDownload()
        {
            if (!Monitor.TryEnter(this))
                return;

            try
            {
                if (!processDataTask.IsCompleted || cancellationTokenSource.IsCancellationRequested)
                    return;

                if (BufferFull)
                {
                    LogInfo($"Full buffer: ({bufferTime}-{currentTime}) {bufferTime - currentTime} > {timeBufferDepth}.");
                    return;
                }

                SwapRepresentation();

                var segment = currentStreams.MediaSegment(currentSegmentId);
                if (segment == null)
                {
                    if (IsDynamic)
                        return;

                    LogWarn($"Segment: {currentSegmentId} NULL stream. Stoping player.");
                    Stop();
                    Error?.Invoke("Unexpected end of content");
                    return;
                }

                DownloadSegment(segment);
            }
            finally
            {
                Monitor.Exit(this);
            }
        }

        private void DownloadSegment(Segment segment)
        {
            var downloadTask = CreateDownloadTask(segment, true, currentSegmentId);

            downloadTask.ContinueWith(response => HandleFailedDownload(GetErrorMessage(response)),
                cancellationTokenSource.Token, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            processDataTask = downloadTask.ContinueWith(response => HandleSuccessfullDownload(response.Result),
                cancellationTokenSource.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
            processDataTask.ContinueWith(_ => ScheduleNextSegDownload(),
                cancellationTokenSource.Token, TaskContinuationOptions.NotOnCanceled, TaskScheduler.Default);
        }

        private void DownloadInitSegment(Segment segment)
        {
            if (initStreamBytes == null)
            {
                var downloadTask = CreateDownloadTask(segment, true, null);

                downloadTask.ContinueWith(response => HandleFailedInitDownload(GetErrorMessage(response)),
                    cancellationTokenSource.Token, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                processDataTask = downloadTask.ContinueWith(response => InitDataDownloaded(response.Result),
                    cancellationTokenSource.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
                processDataTask.ContinueWith(_ => ScheduleNextSegDownload(),
                    cancellationTokenSource.Token, TaskContinuationOptions.NotOnCanceled, TaskScheduler.Default);
            }
            else
            {
                // Already have init segment. Push it down the pipeline & schedule next download
                var initData = new DownloadResponse();
                initData.Data = initStreamBytes;
                initData.SegmentId = null;

                LogInfo($"Skipping INIT segment download");
                InitDataDownloaded(initData);
                ScheduleNextSegDownload();

            }
        }

        private static string GetErrorMessage(Task response)
        {
            return response.Exception?.Flatten().InnerExceptions[0].Message;
        }

        private void HandleSuccessfullDownload(DownloadResponse responseResult)
        {
            sharedBuffer.WriteData(responseResult.Data);
            lastRequestedPeriod = responseResult.DownloadSegment.Period.Copy();
            currentSegmentId = currentStreams.NextSegmentId(currentSegmentId);

            if (IsDynamic)
                bufferTime += responseResult.DownloadSegment.Period.Duration;
            else
                bufferTime = responseResult.DownloadSegment.Period.Start + responseResult.DownloadSegment.Period.Duration;

            var timeInfo = responseResult.DownloadSegment.Period.ToString();

            LogInfo($"Segment: {responseResult.SegmentId} received {timeInfo}");

            if (IsEndOfContent())
                Stop();
        }

        private void InitDataDownloaded(DownloadResponse responseResult)
        {
            if (responseResult.Data != null)
                sharedBuffer.WriteData(responseResult.Data);

            // Assign initStreamBytes AFTER it has been pushed down the shared buffer.
            // When issuing EOS, initStreamBytes will be checked for NULLnes.
            // We do not want to send EOS before init data - will kill demuxer.
            initStreamBytes = responseResult.Data;
            LogInfo("INIT segment downloaded.");
        }

        private void HandleFailedDownload(string message)
        {
            LogError(message);

            // In dynamic manifests, continue ONLY on non init segment failures         
            if (IsDynamic)
            {
                return;
            }

            // Stop Client and signall error.
            //
            Stop();

            Error?.Invoke(message);
        }

        private void HandleFailedInitDownload(string message)
        {
            LogError(message);

            Stop();

            Error?.Invoke(message);
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            SendEOSEvent();

            // Temporary prevention caused by out of order download processing.
            // Wait for download task to complete. Stale cancellations
            // may happen during FF/REW operations. 
            // If received after client start may result in lack of further download requests 
            // being issued. Once download handler are serialized, should be safe to remove.
            try
            {
                if (processDataTask?.Status > TaskStatus.Created)
                    processDataTask.Wait();
            }
            catch (AggregateException) { }


            LogInfo("Data downloader stopped");
        }

        public void SetRepresentation(Representation representation)
        {
            // representation has changed, so reset initstreambytes
            if (currentRepresentation != null)
                initStreamBytes = null;

            currentRepresentation = representation;

            currentStreams = currentRepresentation.Segments;
            UpdateTimeBufferDepth();
        }

        /// <summary>
        /// Updates representation based on Manifest Update
        /// </summary>
        /// <param name="representation"></param>
        public void UpdateRepresentation(Representation representation)
        {
            if (IsDynamic == false)
                return;

            Interlocked.Exchange(ref newRepresentation, representation);
            LogInfo("newRepresentation set");

            ScheduleNextSegDownload();
        }

        /// <summary>
        /// Swaps updated representation based on Manifest Reload.
        /// Updates segment information and base segment ID for the stream.
        /// </summary>
        /// <returns>bool. True. Representations were swapped. False otherwise</returns>
        private void SwapRepresentation()
        {
            // Exchange updated representation with "null". On subsequent calls, this will be an indication
            // that there is no new representations.
            var newRep = Interlocked.Exchange(ref newRepresentation, null);

            // Update internals with new representation if exists.
            if (newRep == null)
                return;

            currentRepresentation = newRep;
            currentStreams = currentRepresentation.Segments;
            currentStreamDuration = currentStreams.Duration;

            UpdateTimeBufferDepth();

            if (lastRequestedPeriod == null)
            {
                currentSegmentId = currentStreams.StartSegmentId(currentTime, timeBufferDepth);
                LogInfo($"Rep. Swap. Start Seg: [{currentSegmentId}]");
                return;
            }

            var newSeg = currentStreams.NextSegmentId(lastRequestedPeriod.Start);
            string message;

            if (newSeg.HasValue)
            {
                var segmentTimeRange = currentStreams.SegmentTimeRange(newSeg);
                message = $"Updated Seg: [{newSeg}]/({segmentTimeRange?.Start}-{segmentTimeRange?.Duration})";
            }
            else
            {
                message = "Not Found. Setting segment to null";
            }

            LogInfo($"Rep. Swap. Last Seg: [{currentSegmentId}]/({lastRequestedPeriod.Start}-{lastRequestedPeriod.Duration}) {message}");

            currentSegmentId = newSeg;

            LogInfo("Representations swapped.");
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            currentTime = time;

            ScheduleNextSegDownload();
        }

        private void SendEOSEvent()
        {
            // Send EOS only when init data has been processed.
            // Stops demuxer being blown to high heavens.
            if (initStreamBytes == null)
                return;

            sharedBuffer.WriteData(null, true);
        }

        private bool IsEndOfContent()
        {
            var endTime = currentStreamDuration ?? TimeSpan.MaxValue;
            return bufferTime >= endTime;
        }

        private Task<DownloadResponse> CreateDownloadTask(Segment stream, bool ignoreError, uint? segmentId)
        {
            var requestData = new DownloadRequest
            {
                DownloadSegment = stream,
                IgnoreError = ignoreError,
                SegmentId = segmentId,
                StreamType = streamType
            };

            return DashDownloader.DownloadDataAsync(requestData, cancellationTokenSource.Token, throughputHistory);
        }

        private void TimeBufferDepthDynamic()
        {
            // For dynamic content, use Manifest buffer time 
            timeBufferDepth = currentStreams.GetDocumentParameters().Document.MinBufferTime ?? TimeBufferDepthDefault;
        }

        private void TimeBufferDepthStatic()
        {
            // Buffer depth is calculated as so:
            // TimeBufferDepth = 
            // 1 avg. seg. duration (one being played out) +
            // bufferTime in multiples of avgSegment Duration with roundup
            // i.e:
            // minbuftime = 5 sek.
            // avgseg = 3 sek.
            // bufferTime = 6 sec.
            // timeBufferDepth = 3 sek + 6 sec.
            //
            var duration = currentStreams.Duration;
            var segments = currentStreams.Count;
            var manifestMinBufferDepth = currentStreams.GetDocumentParameters().Document.MinBufferTime ?? TimeSpan.Zero;

            //Get average segment duration = Total Duration / number of segments.
            var avgSegmentDuration = TimeSpan.FromSeconds(
                    ((double)(duration.Value.TotalSeconds) / (double)segments));

            // Compute multiples of manifest MinBufferTime in units of average segment duration
            // with round up
            var multiples = (uint)((manifestMinBufferDepth.TotalSeconds + avgSegmentDuration.TotalSeconds - 1) / avgSegmentDuration.TotalSeconds);
            var bufferTime = TimeSpan.FromSeconds(multiples * avgSegmentDuration.TotalSeconds);

            // Compose final timeBufferDepth.
            timeBufferDepth = avgSegmentDuration + bufferTime;

            LogInfo($"Average Segment Duration: {avgSegmentDuration} Manifest Min. Buffer Time: {manifestMinBufferDepth}");
        }

        private void UpdateTimeBufferDepth()
        {
            if (IsDynamic == true)
            {
                TimeBufferDepthDynamic();
            }
            else
            {
                TimeBufferDepthStatic();
            }

            LogInfo($"TimeBufferDepth: {timeBufferDepth}");

        }

        #region Logging Functions

        private void LogInfo(string logMessage)
        {
            Logger.Info(streamType + ": " + logMessage);
        }
        private void LogDebug(string logMessage)
        {
            Logger.Debug(streamType + ": " + logMessage);
        }
        private void LogWarn(string logMessage)
        {
            Logger.Warn(streamType + ": " + logMessage);
        }
        private void LogFatal(string logMessage)
        {
            Logger.Fatal(streamType + ": " + logMessage);
        }
        private void LogError(string logMessage)
        {
            Logger.Error(streamType + ": " + logMessage);
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        public void Dispose()
        {
            if (disposedValue)
                return;

            cancellationTokenSource?.Dispose();

            disposedValue = true;
        }

        #endregion
    }
}

