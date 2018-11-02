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
        private static readonly TimeSpan timeBufferDepthDefault = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan maxBufferTime = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan minBufferTime = TimeSpan.FromSeconds(5);

        private TimeSpan timeBufferDepth = timeBufferDepthDefault;

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
        private Task scheduleNextTask;

        private bool buffering;

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
        /// has occured.
        /// </summary>
        public event Error Error;

        /// <summary>
        /// Storage holders for initial packets PTS/DTS values.
        /// Used in Trimming Packet Handler to truncate down PTS/DTS values.
        /// First packet seen acts as flip switch. Fill initial values or not.
        /// </summary>
        private TimeSpan? trimmOffset;

        /// <summary>
        /// Flag indicating if DashClient is initializing. During INIT, adaptive bitrate switching is not
        /// allowed.
        /// </summary>
        private bool initInProgress;

        public event BufferingStarted BufferingStarted;
        public event BufferingCompleted BufferingCompleted;

        public DashClient(IThroughputHistory throughputHistory, ISharedBuffer sharedBuffer, StreamType streamType)
        {
            this.throughputHistory = throughputHistory ??
                                     throw new ArgumentNullException(nameof(throughputHistory),
                                         "throughputHistory cannot be null");
            this.sharedBuffer = sharedBuffer ??
                                throw new ArgumentNullException(nameof(sharedBuffer), "sharedBuffer cannot be null");
            this.streamType = streamType;
        }

        public TimeSpan Seek(TimeSpan position)
        {
            currentSegmentId = currentStreams.SegmentId(position);
            var seekToTimeRange = currentStreams.SegmentTimeRange(currentSegmentId);

            // We are not expecting NULL segments after seek.
            // Termination will occour after restarting
            if (seekToTimeRange == null)
            {
                LogError($"Seek Pos Req: {position} failed. No segment/TimeRange found");
                currentTime = position;
                return currentTime;
            }
            else
            {
                // Set "current time" to end time of segment which will be downloaded.
                // Rational: In case of long segment duration and short buffer times we may buffer just one segment.
                // If IFrame is at the very end of downloaded segment, underlaying player may NOT have enough
                // data to generate OnTime events, effectively stopping playback
                //
                // TODO: Add to SMPlayer.cs generation of OnCurrentTime for packets discarded during IFrame seek operation
                // This will allow DashClient to recieve info on consumed data releasing buffer for further downloads.
                // Such change will be more consistant and not require modification of current time during seek as it is done
                // now.

                currentTime = seekToTimeRange.Start + seekToTimeRange.Duration;

                LogInfo(
                    $"Seek Pos Req: {position} Seek to: ({seekToTimeRange.Start}/{currentTime}) SegId: {currentSegmentId}");

                return seekToTimeRange.Start;
            }
        }

        public void Start(bool initReloadRequired)
        {
            if (currentRepresentation == null)
                throw new Exception("currentRepresentation has not been set");

            initInProgress = true;

            LogInfo("DashClient start.");

            if (cancellationTokenSource == null || cancellationTokenSource?.IsCancellationRequested == true)
            {
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = new CancellationTokenSource();
            }

            // clear garbage before appending new data
            sharedBuffer?.ClearData();

            bufferTime = currentTime;

            if (currentSegmentId.HasValue == false)
                currentSegmentId = currentRepresentation.AlignedStartSegmentID;

            if (!trimmOffset.HasValue)
                trimmOffset = currentRepresentation.AlignedTrimmOffset;

            var initSegment = currentStreams.InitSegment;
            if (initSegment != null)
            {
                DownloadInitSegment(initSegment, initReloadRequired);
            }
            else
            {
                initInProgress = false;
                ScheduleNextSegDownload();
            }
        }

        private void ScheduleNextSegDownload()
        {
            if (!Monitor.TryEnter(this))
                return;

            try
            {
                if (IsEndOfContent(bufferTime))
                {
                    // DashClient termination. This may be happening as part of scheduleDownloadNextTask.
                    // Clear referce held in scheduleDownloadNextTask to prevent Stop() from trying to wait
                    // for itself. Otherwise DashClient will try to chase its own tail (deadlock)
                    scheduleNextTask = null;
                    Stop();
                    return;
                }

                if (!processDataTask.IsCompleted || cancellationTokenSource.IsCancellationRequested)
                    return;

                if (BufferFull)
                {
                    LogInfo(
                        $"Full buffer: ({bufferTime}-{currentTime}) {bufferTime - currentTime} > {timeBufferDepth}.");
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

        private Task CreateScheduleNextTask(CancellationToken token)
        {
            Task newScheduleNext;

            newScheduleNext = processDataTask.ContinueWith(_ => ScheduleNextSegDownload(),
                token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

            return newScheduleNext;
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

            scheduleNextTask = CreateScheduleNextTask(cancelToken);
        }

        private bool HandleCancelledDownload()
        {
            LogInfo($"Segment: download cancelled. Continue? {!cancellationTokenSource.IsCancellationRequested}");

            // if download was cancelled by timeout cancellation token than reschedule download
            return !cancellationTokenSource.IsCancellationRequested;
        }

        private void DownloadInitSegment(Segment segment, bool initReloadRequired)
        {
            LogInfo($"Forcing INIT segment reload: {initReloadRequired}");

            // Grab a copy (its a struct) of cancellation token so it is not referenced through cancellationTokenSource each time.
            var cancelToken = cancellationTokenSource.Token;

            if (initStreamBytes == null || initReloadRequired)
            {
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
                    else // always continue on successful download
                        InitDataDownloaded(response.Result);

                    // throw exception so continuation wont run
                    if (!shouldContinue)
                        throw new Exception();
                }, TaskScheduler.Default);

                scheduleNextTask = CreateScheduleNextTask(cancelToken);
            }
            else
            {
                // Already have init segment. Push it down the pipeline & schedule next download
                var initData = new DownloadResponse
                {
                    Data = initStreamBytes,
                    SegmentId = null
                };

                LogInfo("Segment: INIT Reusing already downloaded data");
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

            LogInfo($"Segment: {responseResult.SegmentId} enqueued {timeInfo}");

            if (IsBufferingCompleted())
                SendBufferingCompletedEvent();

            return true;
        }

        private bool IsBufferingNeeded()
        {
            return !buffering
                   && bufferTime.Subtract(currentTime) <= TimeSpan.FromSeconds(2)
                   && currentTime > TimeSpan.FromSeconds(5);
        }

        private void SendBufferingStartedEvent()
        {
            if (buffering)
                return;
            Task.Run(() => BufferingStarted?.Invoke());
            buffering = true;
        }

        private bool IsBufferingCompleted()
        {
            return buffering && bufferTime.Subtract(currentTime) >= TimeSpan.FromSeconds(5);
        }

        private void SendBufferingCompletedEvent()
        {
            if (!buffering)
                return;
            Task.Run(() => BufferingCompleted?.Invoke());
            buffering = false;
        }

        private void InitDataDownloaded(DownloadResponse responseResult)
        {
            if (responseResult.Data != null)
                sharedBuffer.WriteData(responseResult.Data);

            // Assign initStreamBytes AFTER it has been pushed down the shared buffer.
            // When issuing EOS, initStreamBytes will be checked for NULLnes.
            // We do not want to send EOS before init data - will kill demuxer.
            initStreamBytes = responseResult.Data;
            initInProgress = false;
            LogInfo("Segment: INIT enqueued.");
        }

        private bool HandleFailedDownload(Task response)
        {
            var errorMessage = GetErrorMessage(response);
            LogError(errorMessage);

            var exception = response.Exception?.Flatten().InnerExceptions[0] as DashDownloaderException;

            if (IsDynamic)
            {
                // Http 404 Not Found. Increment Segment ID
                // TODO: Use Response Codes rather then text search
                //
                if (exception != null)
                {
                    if (exception.InnerException.Message.Contains("(404)") == true)
                        currentSegmentId = currentStreams.NextSegmentId(currentSegmentId);
                }

                return true;
            }

            StopAsync();

            if (exception != null)
            {
                var segmentTime = exception.DownloadRequest.DownloadSegment.Period.Start;
                var segmentDuration = exception.DownloadRequest.DownloadSegment.Period.Duration;

                var segmentEndTime = segmentTime + segmentDuration - (trimmOffset ?? TimeSpan.Zero);
                if (IsEndOfContent(segmentEndTime))
                    return false;
            }

            // Commented out on Dr. Boo request. No error message on failed download, just playback
            // termination.
            // Error?.Invoke(errorMessage);

            return false;
        }

        private void HandleFailedInitDownload(string message)
        {
            LogError(message);
            initInProgress = false;

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
            WaitForTaskCompletionNoError(scheduleNextTask);
            WaitForTaskCompletionNoError(downloadDataTask);
            WaitForTaskCompletionNoError(processDataTask);

            LogInfo("Data downloader stopped");
        }

        private static void WaitForTaskCompletionNoError(Task task)
        {
            try
            {
                if (task?.Status > TaskStatus.Created)
                    task?.Wait();
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

            currentStreamDuration = IsDynamic
                ? currentStreams.GetDocumentParameters().Document.MediaPresentationDuration
                : currentStreams.Duration;

            UpdateTimeBufferDepth();

            if (lastDownloadSegmentTimeRange == null)
            {
                currentSegmentId = currentRepresentation.AlignedStartSegmentID;
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

            LogInfo(
                $"Rep. Swap. Last Seg: {currentSegmentId}/{lastDownloadSegmentTimeRange.Start}-{lastDownloadSegmentTimeRange.Duration} {message}");

            currentSegmentId = newSeg;

            LogInfo("Representations swapped.");
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            // Ignore time updated events when EOS is already sent
            if (isEosSent)
                return;

            currentTime = time;

            if (IsBufferingNeeded())
                SendBufferingStartedEvent();

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

        private async Task<DownloadResponse> CreateDownloadTask(Segment segment, bool ignoreError, uint? segmentId,
            CancellationToken cancelToken)
        {
            var timeout = CalculateDownloadTimeout(segment);

            Logger.Info($"Calculated download timeout is {timeout.TotalMilliseconds}");

            var requestData = new DownloadRequest
            {
                DownloadSegment = segment,
                IgnoreError = ignoreError,
                SegmentId = segmentId,
                StreamType = streamType
            };

            var timeoutCancellationTokenSource = new CancellationTokenSource();
            if (timeout != TimeSpan.MaxValue)
                timeoutCancellationTokenSource.CancelAfter(timeout);

            using (timeoutCancellationTokenSource)
            using (var downloadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancelToken, timeoutCancellationTokenSource.Token))
            {
                return await DashDownloader.DownloadDataAsync(requestData, downloadCancellationTokenSource.Token,
                    throughputHistory);
            }
        }

        private TimeSpan CalculateDownloadTimeout(Segment segment)
        {
            if (!IsDynamic)
                return TimeSpan.MaxValue;

            var timeout = timeBufferDepthDefault;
            var avarageThroughput = throughputHistory.GetAverageThroughput();
            if (avarageThroughput > 0 && currentRepresentation.Bandwidth.HasValue && segment.Period != null)
            {
                var bandwith = currentRepresentation.Bandwidth.Value;
                var duration = segment.Period.Duration.TotalSeconds;
                var segmentSize = bandwith * duration;
                var calculatedTimeNeeded = TimeSpan.FromSeconds(segmentSize / avarageThroughput * 1.5);
                var manifestMinBufferDepth =
                    currentStreams.GetDocumentParameters().Document.MinBufferTime ?? TimeSpan.Zero;
                timeout = calculatedTimeNeeded > manifestMinBufferDepth ? calculatedTimeNeeded : manifestMinBufferDepth;
            }

            return timeout;
        }

        private void TimeBufferDepthDynamic()
        {
            // For dynamic content, use TimeShiftBuffer depth as it defines "available" content.
            // 1/4 of the buffer is "used up" when selecting start segment.
            // Start Segment = First Available + 1/4 of Time Shift Buffer.
            // Use max 1/2 of TimeShiftBufferDepth.
            //
            var tsBuffer = (int)(currentStreams.GetDocumentParameters().Document.TimeShiftBufferDepth?.TotalSeconds ??
                                  0);
            tsBuffer = tsBuffer / 2;

            // If value is out of range, truncate to max 15 seconds.
            timeBufferDepth = (tsBuffer == 0) ? timeBufferDepthDefault : TimeSpan.FromSeconds(tsBuffer);
        }

        private void TimeBufferDepthStatic()
        {
            var duration = currentStreams.Duration;
            var segments = currentStreams.Count;
            var manifestMinBufferDepth =
                currentStreams.GetDocumentParameters().Document.MinBufferTime ?? timeBufferDepthDefault;

            //Get average segment duration = Total Duration / number of segments.
            var avgSegmentDuration = TimeSpan.FromSeconds(
                duration.Value.TotalSeconds / segments);

            var portionOfSegmentDuration = TimeSpan.FromSeconds(avgSegmentDuration.TotalSeconds * 0.15);

            var safeTimeBufferDepth = TimeSpan.FromTicks(avgSegmentDuration.Ticks * 3) + portionOfSegmentDuration;

            if (safeTimeBufferDepth >= manifestMinBufferDepth)
            {
                timeBufferDepth = safeTimeBufferDepth;
            }
            else
            {
                timeBufferDepth = manifestMinBufferDepth;

                var bufferLeft = manifestMinBufferDepth - portionOfSegmentDuration;
                if (bufferLeft < portionOfSegmentDuration)
                    timeBufferDepth += portionOfSegmentDuration;
            }

            LogInfo(
                $"Average Segment Duration: {avgSegmentDuration} Manifest Min. Buffer Time: {manifestMinBufferDepth}");
        }

        private void UpdateTimeBufferDepth()
        {
            if (IsDynamic)
                TimeBufferDepthDynamic();
            else
                TimeBufferDepthStatic();

            if (timeBufferDepth > maxBufferTime)
                timeBufferDepth = maxBufferTime;
            else if (timeBufferDepth < minBufferTime)
                timeBufferDepth = minBufferTime;

            LogInfo($"TimeBufferDepth: {timeBufferDepth}");
        }

        public bool CanStreamSwitch()
        {
            // Allow stream change ONLY if not performing initialization.
            // If needed, initInProgress flag could be used to delay stream switching
            // i.e. reset not after INIT segment but INIT + whatever number of data segments.
            //
            return !initInProgress && bufferTime >= minBufferTime;
        }

        #region Logging Functions

        private void LogInfo(string logMessage, [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Info(streamType + ": " + logMessage, file, method, line);
        }

        private void LogDebug(string logMessage, [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Debug(streamType + ": " + logMessage, file, method, line);
        }

        private void LogWarn(string logMessage, [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Warn(streamType + ": " + logMessage, file, method, line);
        }

        private void LogFatal(string logMessage, [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
        {
            Logger.Fatal(streamType + ": " + logMessage, file, method, line);
        }

        private void LogError(string logMessage, [CallerFilePath] string file = "",
            [CallerMemberName] string method = "", [CallerLineNumber] int line = 0)
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

            cancellationTokenSource?.Dispose();

            disposedValue = true;
        }

        #endregion
    }
}
