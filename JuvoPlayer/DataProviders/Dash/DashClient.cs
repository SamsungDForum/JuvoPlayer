using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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
        private bool isEosSent;

        private IRepresentationStream currentStreams;
        private TimeSpan? currentStreamDuration;

        private byte[] initStreamBytes;

        private Task<DownloadResponse> downloadDataTask;
        private Task processDataTask;
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Contains information about timing data for last requested segment
        /// </summary>
        private TimeRange lastDownloadSegmentTimeRange = new TimeRange(TimeSpan.Zero, TimeSpan.Zero);

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

        /// <summary>
        /// Storage holders for initial packets PTS/DTS values.
        /// Used in Trimming Packet Handler to truncate down PTS/DTS values.
        /// First packet seen acts as flip switch. Fill initial values or not.
        /// </summary>
        private TimeSpan? trimmOffset;

        /// <summary>
        /// Storage place for ALL, audio & video trimm offset. Used for retrieval of
        /// smallest offset from audio or video.
        /// </summary>
        private static readonly ConcurrentDictionary<StreamType, TimeSpan> trimmOffsetStorage = new ConcurrentDictionary<StreamType, TimeSpan>();

        public DashClient(IThroughputHistory throughputHistory, ISharedBuffer sharedBuffer, StreamType streamType)
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
            if (!currentSegmentId.HasValue || !newTime.HasValue)
                LogError($"Seek Pos Req: {position} failed. No segment/TimeRange found");

            currentTime = newTime ?? position;
            LogInfo($"Seek Pos Req: {position} Seek to: ({currentTime}) SegId: {currentSegmentId}");
            return currentTime;
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
                if (IsEndOfContent(bufferTime))
                {
                    Stop();
                    return;
                }

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
                    LogInfo($"Segment: [{currentSegmentId}] NULL stream");
                    if (IsDynamic)
                        return;

                    LogWarn("Stopping player");

                    Stop();
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
            // Grab a copy (its a struct) of cancellation token, so one token is used throughout entire operation
            var cancelToken = cancellationTokenSource.Token;

            downloadDataTask = CreateDownloadTask(segment, IsDynamic, currentSegmentId, cancelToken);
            processDataTask = downloadDataTask.ContinueWith(response =>
            {
                bool shouldContinue;
                if (response.IsCanceled)
                    shouldContinue = HandleCancelledDownload();
                else if (response.IsFaulted)
                    shouldContinue = HandleFailedDownload(response);
                else // always continue on successfull download
                    shouldContinue = HandleSuccessfullDownload(response.Result);

                // throw exception so continuation wont run
                if (!shouldContinue)
                    throw new Exception();

            }, TaskScheduler.Default);

            processDataTask.ContinueWith(_ => ScheduleNextSegDownload(),
                cancelToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
        }

        private bool HandleCancelledDownload()
        {
            LogInfo("Segment download cancelled");

            // if download was cancelled by timeout cancellation token than reschedule download
            return !cancellationTokenSource.IsCancellationRequested;
        }

        private void DownloadInitSegment(Segment segment)
        {
            if (initStreamBytes == null)
            {
                // Grab a copy (its a struct) of cancellation token so it is not referenced through cancellationTokenSource each time.
                var cancelToken = cancellationTokenSource.Token;

                downloadDataTask = CreateDownloadTask(segment, true, null, cancelToken);
                processDataTask = downloadDataTask.ContinueWith(response =>
                {
                    var shouldContinue = true;
                    if (response.IsFaulted)
                    {
                        HandleFailedInitDownload(GetErrorMessage(response));
                        shouldContinue = false;
                    }
                    else if (response.IsCanceled)
                        shouldContinue = false;
                    else // always continue on successfull download
                        InitDataDownloaded(response.Result);

                    // throw exception so continuation wont run
                    if (!shouldContinue)
                        throw new Exception();

                }, TaskScheduler.Default);

                processDataTask.ContinueWith(_ => ScheduleNextSegDownload(),
                    cancelToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
            }
            else
            {
                // Already have init segment. Push it down the pipeline & schedule next download
                var initData = new DownloadResponse
                {
                    Data = initStreamBytes,
                    SegmentId = null
                };

                LogInfo("Skipping INIT segment download");
                InitDataDownloaded(initData);
                ScheduleNextSegDownload();
            }
        }

        private static string GetErrorMessage(Task response)
        {
            return response.Exception?.Flatten().InnerExceptions[0].Message;
        }

        private bool HandleSuccessfullDownload(DownloadResponse responseResult)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                return false;

            sharedBuffer.WriteData(responseResult.Data);

            var segment = responseResult.DownloadSegment;
            lastDownloadSegmentTimeRange = segment.Period.Copy();
            bufferTime = segment.Period.Start + segment.Period.Duration - (trimmOffset ?? TimeSpan.Zero);

            currentSegmentId = currentStreams.NextSegmentId(currentSegmentId);

            var timeInfo = segment.Period.ToString();

            LogInfo($"Segment: {responseResult.SegmentId} received {timeInfo}");

            return true;
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

        private bool HandleFailedDownload(Task response)
        {
            var errorMessage = GetErrorMessage(response);
            LogError(errorMessage);

            if (IsDynamic)
                return true;

            StopAsync();

            var exception = response.Exception?.Flatten().InnerExceptions[0];
            if (exception is DashDownloaderException downloaderException)
            {
                var segmentTime = downloaderException.DownloadRequest.DownloadSegment.Period.Start;
                var segmentDuration = downloaderException.DownloadRequest.DownloadSegment.Period.Duration;

                var segmentEndTime = segmentTime + segmentDuration - (trimmOffset ?? TimeSpan.Zero);
                if (IsEndOfContent(segmentEndTime))
                    return false;
            }

            Error?.Invoke(errorMessage);

            return false;
        }

        private void HandleFailedInitDownload(string message)
        {
            LogError(message);

            StopAsync();

            Error?.Invoke(message);
        }

        private void StopAsync()
        {
            cancellationTokenSource?.Cancel();
            SendEosEvent();
        }

        public void Reset()
        {
            cancellationTokenSource?.Cancel();

            // Temporary prevention caused by out of order download processing.
            // Wait for download task to complete. Stale cancellations
            // may happen during FF/REW operations. 
            // If received after client start may result in lack of further download requests 
            // being issued. Once download handler are serialized, should be safe to remove.
            WaitForTaskCompletionNoError(downloadDataTask);
            WaitForTaskCompletionNoError(processDataTask);

            LogInfo("Data downloader stopped");
        }

        private static void WaitForTaskCompletionNoError(Task task)
        {
            try
            {
                if (task?.Status > TaskStatus.Created)
                    task.Wait();
            }
            catch (AggregateException)
            {
            }
        }

        public void Stop()
        {
            Reset();
            SendEosEvent();
        }

        public void SetRepresentation(Representation representation)
        {
            // representation has changed, so reset initstreambytes
            if (currentRepresentation != null)
                initStreamBytes = null;

            currentRepresentation = representation;
            currentStreams = currentRepresentation.Segments;

            // Prepare Stream for playback.
            if (!currentStreams.PrepeareStream())
            {
                LogError("Failed to prepare stream. No playable segments");

                // Static content - treat as EOS.
                if (!IsDynamic)
                    Stop();

                return;
            }

            currentStreamDuration = IsDynamic
                ? currentStreams.GetDocumentParameters().Document.MediaPresentationDuration
                : currentStreams.Duration;

            UpdateTimeBufferDepth();
        }

        /// <summary>
        /// Updates representation based on Manifest Update
        /// </summary>
        /// <param name="representation"></param>
        public void UpdateRepresentation(Representation representation)
        {
            if (!IsDynamic)
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

            // Sanity check on streams (needed if stream fails its own IO - index download, etc.)
            if (!currentStreams.PrepeareStream())
            {
                LogError("Failed to prepare stream. No playable segments");

                // Static content - treat as EOS.
                if (!IsDynamic)
                    Stop();

                currentSegmentId = null;
                return;
            }

            currentStreamDuration = IsDynamic
                ? currentStreams.GetDocumentParameters().Document.MediaPresentationDuration
                : currentStreams.Duration;

            UpdateTimeBufferDepth();

            if (lastDownloadSegmentTimeRange == null)
            {
                currentSegmentId = currentStreams.StartSegmentId(currentTime, timeBufferDepth);
                LogInfo($"Rep. Swap. Start Seg: [{currentSegmentId}]");
                return;
            }

            var newSeg = currentStreams.NextSegmentId(lastDownloadSegmentTimeRange.Start);
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

            LogInfo($"Rep. Swap. Last Seg: {currentSegmentId}/{lastDownloadSegmentTimeRange.Start}-{lastDownloadSegmentTimeRange.Duration} {message}");

            currentSegmentId = newSeg;

            LogInfo("Representations swapped.");
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            // Ignore time updated events when EOS is already sent
            if (isEosSent)
                return;

            currentTime = time;

            ScheduleNextSegDownload();
        }

        private void SendEosEvent()
        {
            // Send EOS only when init data has been processed.
            // Stops demuxer being blown to high heavens.
            if (initStreamBytes == null)
                return;

            sharedBuffer.WriteData(null, true);

            isEosSent = true;
        }

        private bool IsEndOfContent(TimeSpan time)
        {
            var endTime = !currentStreamDuration.HasValue || currentStreamDuration.Value == TimeSpan.Zero
                ? TimeSpan.MaxValue
                : currentStreamDuration.Value;

            return time >= endTime;
        }

        private async Task<DownloadResponse> CreateDownloadTask(Segment segment, bool ignoreError, uint? segmentId, CancellationToken cancelToken)
        {
            var requestData = new DownloadRequest
            {
                DownloadSegment = segment,
                IgnoreError = ignoreError,
                SegmentId = segmentId,
                StreamType = streamType
            };

            if (trimmOffset.HasValue == false && segment.Period != null)
            {
                trimmOffset = segment.Period.Start;
                if (!trimmOffsetStorage.TryAdd(streamType, trimmOffset.Value))
                {
                    LogWarn($"Failed to store trimm offset {trimmOffset}. A/V may be out of sync");
                }
            }

            var timeout = CalculateDownloadTimeout(segment);
            using (var timeoutCancellationTokenSource = new CancellationTokenSource(timeout))
            using (var downloadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancelToken, timeoutCancellationTokenSource.Token))
            {
                return await DashDownloader.DownloadDataAsync(requestData, downloadCancellationTokenSource.Token,
                    throughputHistory);
            }
        }

        private TimeSpan CalculateDownloadTimeout(Segment segment)
        {
            var timeout = TimeBufferDepthDefault;
            var avarageThroughput = throughputHistory.GetAverageThroughput();
            if (avarageThroughput > 0 && currentRepresentation.Bandwidth.HasValue && segment.Period != null)
            {
                var bandwith = currentRepresentation.Bandwidth.Value;
                var duration = segment.Period.Duration.TotalSeconds;
                var segmentSize = bandwith * duration;
                var calculatedTimeNeeded = TimeSpan.FromSeconds(segmentSize / avarageThroughput * 1.5);
                var manifestMinBufferDepth = currentStreams.GetDocumentParameters().Document.MinBufferTime ?? TimeSpan.Zero;
                timeout = calculatedTimeNeeded > manifestMinBufferDepth ? calculatedTimeNeeded : manifestMinBufferDepth;
            }

            return timeout;
        }

        private void TimeBufferDepthDynamic()
        {
            // For dynamic content, use Manifest buffer time 
            timeBufferDepth = currentStreams.GetDocumentParameters().Document.MinBufferTime ?? TimeBufferDepthDefault;
        }

        private void TimeBufferDepthStatic()
        {
            // Buffer depth is calculated as:
            //
            // Case: AverageSegmentDuration >= manifestBufferTime
            // timeBufferDepth = AverageSegmentDuration + 10% of AverageSegmentDuration
            // 
            // Cases: AverageSegmentDuration < manifestBufferTime
            // TimeBufferDepth = 
            // 1 AverageSegmentDuration (one being played out) + 
            // bufferTime in multiples of AverageSegmentDuration (Rounded down)
            // 
            // Buffer time is cliped to maximum of 15 seconds or average segment size.
            //
            var duration = currentStreams.Duration;
            var segments = currentStreams.Count;
            var manifestMinBufferDepth = currentStreams.GetDocumentParameters().Document.MinBufferTime ?? TimeBufferDepthDefault;

            //Get average segment duration = Total Duration / number of segments.
            var avgSegmentDuration = TimeSpan.FromSeconds(
                    ((double)(duration.Value.TotalSeconds) / (double)segments));

            // Always buffer 1 downloadable segment
            timeBufferDepth = avgSegmentDuration;

            if (avgSegmentDuration >= manifestMinBufferDepth)
            {
                timeBufferDepth += TimeSpan.FromSeconds(avgSegmentDuration.TotalSeconds * 0.1);
            }
            else
            {
                var muliples = Math.Floor(manifestMinBufferDepth.TotalSeconds / avgSegmentDuration.TotalSeconds);
                timeBufferDepth += TimeSpan.FromSeconds((avgSegmentDuration.TotalSeconds * muliples));
            }

            // Truncate buffer time down to 15 seconds max or Average Segment Size 
            // IF average segment size is larger.
            var maxBufferTime = TimeSpan.FromSeconds(Math.Max(15, avgSegmentDuration.TotalSeconds));
            if (timeBufferDepth > maxBufferTime)
                timeBufferDepth = maxBufferTime;

            LogInfo($"Average Segment Duration: {avgSegmentDuration} Manifest Min. Buffer Time: {manifestMinBufferDepth}");
        }

        private void UpdateTimeBufferDepth()
        {
            if (IsDynamic)
                TimeBufferDepthDynamic();
            else
                TimeBufferDepthStatic();

            LogInfo($"TimeBufferDepth: {timeBufferDepth}");
        }

        public TimeSpan GetTrimmOffset()
        {
            // Wait till Audio and Video trimm offsets are available OR untill we got cancelled
            //
            if (trimmOffsetStorage.Count != 2)
            {
                var waitFor = (streamType == StreamType.Video ? StreamType.Audio : StreamType.Video);
                LogInfo($"Waiting for {waitFor} trimm Offset");
                SpinWait.SpinUntil(() => trimmOffsetStorage.Count == 2 || cancellationTokenSource.IsCancellationRequested);

                if (cancellationTokenSource.IsCancellationRequested)
                    return TimeSpan.Zero;

                LogInfo($"{trimmOffsetStorage.Count} Trimm Offsets available");
            }

            // Concurrent collection does not seem to offer Max extension method
            // as defined in docs
            // https://msdn.microsoft.com/library/dd287191(v=vs.110).aspx
            // thus scan content manually
            //
            TimeSpan res = TimeSpan.MaxValue;

            foreach (var v in trimmOffsetStorage.Values)
            {
                if (v < res)
                    res = v;
            }

            return res;
        }

        #region Logging Functions

        private void LogInfo(string logMessage, [CallerFilePath] string file = "", [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Info(streamType + ": " + logMessage, file, method, line);
        }
        private void LogDebug(string logMessage, [CallerFilePath] string file = "", [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Debug(streamType + ": " + logMessage, file, method, line);
        }
        private void LogWarn(string logMessage, [CallerFilePath] string file = "", [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Warn(streamType + ": " + logMessage, file, method, line);
        }
        private void LogFatal(string logMessage, [CallerFilePath] string file = "", [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Fatal(streamType + ": " + logMessage, file, method, line);
        }
        private void LogError(string logMessage, [CallerFilePath] string file = "", [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Error(streamType + ": " + logMessage, file, method, line);
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        public void Dispose()
        {
            if (disposedValue)
                return;

            // Remove previous entry from trimmOffsetStorage
            trimmOffsetStorage.TryRemove(streamType, out var tmp);

            cancellationTokenSource?.Dispose();

            disposedValue = true;
        }

        #endregion
    }
}

