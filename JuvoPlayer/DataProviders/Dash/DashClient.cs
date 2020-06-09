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
using System.Collections.Generic;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using JuvoLogger;
using JuvoPlayer.Common;
using MpdParser.Node;
using MpdParser.Node.Dynamic;
using Representation = MpdParser.Representation;
using static Configuration.DashClient;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashClient : IDashClient
    {
        private enum DownloadLoopStatus
        {
            Continue,
            GiveUp
        }

        private const string Tag = "JuvoPlayer";

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private readonly IThroughputHistory throughputHistory;
        private readonly StreamType streamType;

        private Representation currentRepresentation;
        private Representation newRepresentation;
        private TimeSpan currentTime = TimeSpan.Zero;
        private TimeSpan bufferTime = TimeSpan.Zero;
        private uint? currentSegmentId;
        private bool isEosSent;

        private IRepresentationStream currentStreams;
        private TimeSpan? currentStreamDuration;

        private readonly IList<byte[]> initStreamBytes = new List<byte[]>();

        private Task<DownloadResponse> downloadDataTask;
        private Task processDataTask = Task.CompletedTask;
        private CancellationTokenSource cancellationTokenSource;
        private Task downloadCompletedTask;

        /// <summary>
        /// Contains information about timing data for last requested segment
        /// </summary>
        private TimeRange lastDownloadSegmentTimeRange;

        private TimeSpan _dataClockLimit;

        /// <summary>
        /// A shorthand for retrieving currently played out document type
        /// True - Content is dynamic
        /// False - Content is static.
        /// </summary>
        private bool IsDynamic => currentStreams.GetDocumentParameters().Document.IsDynamic;

        /// <summary>
        /// Flag indicating if DashClient is initializing. During INIT, adaptive bitrate switching is not
        /// allowed. Init in progress = Download of InitSegment + minBufferTime of data
        /// </summary>
        private bool initInProgress;

        private bool initSegmentReloadRequired;
        private TimeSpan firstSegmentClock;

        private readonly Subject<string> errorSubject = new Subject<string>();
        private readonly Subject<Unit> readySubject = new Subject<Unit>();
        private readonly Subject<byte[]> chunkReadySubject = new Subject<byte[]>();

        public DashClient(IThroughputHistory throughputHistory, StreamType streamType)
        {
            this.throughputHistory = throughputHistory ??
                                     throw new ArgumentNullException(nameof(throughputHistory),
                                         "throughputHistory cannot be null");
            this.streamType = streamType;
        }

        public bool IsDataAvailable()
        {
            // No last download segment = Initial stream start.
            // Data availability not checked, initial segment will be used.
            if (lastDownloadSegmentTimeRange == null)
                return true;

            // Make sure stream info is up to date.
            SwapRepresentation();

            var segmentId = currentStreams.NextSegmentId(lastDownloadSegmentTimeRange.Start);

            if (!segmentId.HasValue)
                return false;

            if (!IsDynamic)
                return true;

            // Dynamic content. Make sure it will be downloaded.
            if (IsBufferSpaceAvailable())
                return true;

            // Dynamic content & buffer full. Check if segment will not time out
            // considering buffer is currently full. Do so by checking if previous segment
            // defined by nextSeg.Start - buffer overhead time is available
            var nextSegTimeRange = currentStreams.SegmentTimeRange(segmentId);
            if (nextSegTimeRange == null)
                return false;

            var timeToNextSegmentDownload = _dataClockLimit - bufferTime
                                            + DynamicSegmentAvailabilityOverhead;

            var prevSegmentTimeIndex = nextSegTimeRange.Start - timeToNextSegmentDownload;

            segmentId = currentStreams.NextSegmentId(prevSegmentTimeIndex);

            return segmentId.HasValue;
        }

        public void SetDataRequest(TimeSpan request)
        {
            if (cancellationTokenSource?.IsCancellationRequested != false)
                return;

            // When starting stream - initial pts is not known.
            // Player will issue data requests assuming start from zero.
            // For non zero start stream (like live), initial is necessary.
            if (firstSegmentClock > request)
                request += firstSegmentClock;

            // Reduce message output to changed end clock.
            if (request != _dataClockLimit)
                LogInfo($"Data clock update: {_dataClockLimit}->{request}");

            _dataClockLimit = request;
            ScheduleNextSegDownload();
        }

        public TimeSpan Seek(TimeSpan position)
        {
            // A workaround for a case when we seek to the end of a content while audio and video streams have
            // a slightly different duration.
            if (position > currentStreams.Duration)
                position = (TimeSpan)currentStreams.Duration;

            currentSegmentId = currentStreams.SegmentId(position);
            var previousSegmentId = currentStreams.PreviousSegmentId(currentSegmentId);
            lastDownloadSegmentTimeRange = currentStreams.SegmentTimeRange(previousSegmentId);

            var seekToTimeRange = currentStreams.SegmentTimeRange(currentSegmentId);

            // We are not expecting NULL segments after seek.
            // Termination will occur after restarting
            if (seekToTimeRange == null)
            {
                LogError($"Seek Pos Req: {position} failed. No segment/TimeRange found");
                throw new ArgumentOutOfRangeException();
            }

            // Seek operation clears all buffered data (bufferTime=0). bufferTime is set to currentTime
            // during Start(). bufferTime will get updated after chunk download.
            currentTime = TimeSpan.Zero;

            // Clear dataClockLimit. Download will start after new dataClockLimit is
            // received.
            _dataClockLimit = TimeSpan.Zero;

            LogInfo(
                $"Seek Pos Req: {position} Seek to: ({seekToTimeRange.Start}-{seekToTimeRange.Start + seekToTimeRange.Duration}/{currentTime}) SegId: {currentSegmentId}");

            return seekToTimeRange.Start;
        }

        public void Start(bool initReloadRequired)
        {
            SwapRepresentation();

            if (currentRepresentation == null)
                throw new Exception("currentRepresentation has not been set");

            initInProgress = true;
            initSegmentReloadRequired = initReloadRequired;

            // Clear init bytes early. If client terminates before new init segment is downloaded
            // (i.e. seek operation), stale init data will be present.
            if (initReloadRequired)
                initStreamBytes.Clear();

            if (cancellationTokenSource == null || cancellationTokenSource?.IsCancellationRequested == true)
            {
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = new CancellationTokenSource();
            }

            bufferTime = currentTime;

            if (currentSegmentId.HasValue == false)
            {
                currentSegmentId = currentRepresentation.Segments.StartSegmentId();
                firstSegmentClock = currentStreams.SegmentTimeRange(currentSegmentId).Start;
            }

            if (currentStreams.InitSegment == null)
                initInProgress = false;

            LogInfo($"{streamType}: Started");
            ScheduleNextSegDownload();
        }

        private bool IsBufferSpaceAvailable()
        {
            if (!currentSegmentId.HasValue)
                return _dataClockLimit > TimeSpan.Zero;

            var currentChunk = currentStreams.SegmentTimeRange(currentSegmentId).Duration;

            if (_dataClockLimit >= bufferTime + currentChunk)
                return true;

            LogInfo($"Full. BufferTime {bufferTime} Max {_dataClockLimit} Chunk {currentChunk} ({bufferTime - currentTime})");

            return false;
        }

        public void ScheduleNextSegDownload()
        {
            if (IsEndOfContent(bufferTime))
            {
                LogInfo("End of content");
                // DashClient termination. This may be happening as part of scheduleDownloadNextTask.
                // Clear reference held in scheduleDownloadNextTask to prevent Stop() from trying to wait
                // for itself. Otherwise DashClient will try to chase its own tail (deadlock)
                downloadCompletedTask = null;

                // Generate EOS asap
                StopAsync();
                return;
            }

            if (!processDataTask.IsCompleted || cancellationTokenSource == null || cancellationTokenSource.IsCancellationRequested)
                return;

            if (!IsBufferSpaceAvailable())
                return;

            SwapRepresentation();

            if (initInProgress)
            {
                if (currentStreams.InitSegment != null)
                {
                    DownloadInitSegment(currentStreams.InitSegment, initSegmentReloadRequired);
                    return;
                }

                initInProgress = false;
            }

            var segment = currentStreams.MediaSegment(currentSegmentId);

            if (segment == null)
            {
                LogInfo($"Segment: [{currentSegmentId}] NULL stream");
                if (IsDynamic)
                    return;

                LogWarn("Stopping player");

                // Generate EOS asap
                StopAsync();
                return;
            }

            DownloadSegment(segment);
        }

        private void DownloadSegment(Segment segment)
        {
            // Grab a copy (its a struct) of cancellation token, so one token is used throughout entire operation
            var cancelToken = cancellationTokenSource.Token;
            var chunkDownloadedHandler = new Action<byte[]>(chunkReadySubject.OnNext);
            downloadDataTask = CreateDownloadTask(segment, IsDynamic, currentSegmentId, chunkDownloadedHandler,
                cancelToken);
            processDataTask = downloadDataTask.ContinueWith(response =>
            {
                DownloadLoopStatus loopStatus;
                if (cancelToken.IsCancellationRequested)
                    loopStatus = DownloadLoopStatus.GiveUp;
                else if (response.IsCanceled)
                    loopStatus = HandleCancelledDownload(cancelToken);
                else if (response.IsFaulted)
                    loopStatus = HandleFailedDownload(response);
                else // always continue on successful download
                    loopStatus = HandleSuccessfulDownload(response.Result);

                // throw exception so continuation wont run
                if (loopStatus == DownloadLoopStatus.GiveUp)
                    throw new Exception();
            }, TaskScheduler.Default);
            downloadCompletedTask = processDataTask.ContinueWith(
                _ => readySubject.OnNext(Unit.Default),
                TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private DownloadLoopStatus HandleCancelledDownload(CancellationToken token)
        {
            LogInfo($"Segment: download cancelled. Continue? {!cancellationTokenSource.IsCancellationRequested}");

            // if download was cancelled by timeout cancellation token than reschedule download
            return token.IsCancellationRequested == false ? DownloadLoopStatus.Continue : DownloadLoopStatus.GiveUp;
        }

        private void DownloadInitSegment(Segment segment, bool initReloadRequired)
        {
            LogInfo($"Forcing INIT segment reload: {initReloadRequired}");

            // Grab a copy (its a struct) of cancellation token so it is not referenced through cancellationTokenSource each time.
            var cancelToken = cancellationTokenSource.Token;
            if (initStreamBytes.Count == 0 || initReloadRequired)
            {
                initStreamBytes.Clear();
                var chunkDownloadedHandler = new Action<byte[]>(bytes =>
                {
                    initStreamBytes.Add(bytes);
                    chunkReadySubject.OnNext(bytes);
                });

                downloadDataTask = CreateDownloadTask(segment, true, null, chunkDownloadedHandler, cancelToken);
                processDataTask = downloadDataTask.ContinueWith(response =>
                {
                    var loopStatus = DownloadLoopStatus.Continue;
                    if (cancelToken.IsCancellationRequested)
                        loopStatus = DownloadLoopStatus.GiveUp;
                    else if (response.IsFaulted)
                    {
                        HandleFailedInitDownload(GetErrorMessage(response));
                        loopStatus = DownloadLoopStatus.GiveUp;
                    }
                    else if (response.IsCanceled)
                    {
                        initStreamBytes.Clear();
                        loopStatus = DownloadLoopStatus.GiveUp;
                    }
                    else // always continue on successful download
                        InitDataDownloaded();

                    // throw exception so continuation wont run
                    if (loopStatus == DownloadLoopStatus.GiveUp)
                        throw new Exception();
                }, TaskScheduler.Default);

                downloadCompletedTask = processDataTask.ContinueWith(
                    _ =>
                    {
                        LogInfo("Init Done. Poking downloadCompleted");
                        readySubject.OnNext(Unit.Default);
                    },
                    TaskContinuationOptions.OnlyOnRanToCompletion);
            }
            else
            {
                // Already have init segment. Push it down the pipeline & schedule next download
                LogInfo("Segment: INIT Reusing already downloaded data");
                foreach (var chunk in initStreamBytes)
                    chunkReadySubject.OnNext(chunk);
                InitDataDownloaded();
                readySubject.OnNext(Unit.Default);
            }
        }

        private static string GetErrorMessage(Task response)
        {
            return response.Exception?.Flatten().InnerExceptions[0].Message;
        }

        private DownloadLoopStatus HandleSuccessfulDownload(DownloadResponse responseResult)
        {
            if (cancellationTokenSource.IsCancellationRequested)
                return DownloadLoopStatus.GiveUp;
            var segment = responseResult.DownloadSegment;
            lastDownloadSegmentTimeRange = segment.Period.Copy();
            bufferTime = segment.Period.Start + segment.Period.Duration;
            currentSegmentId = currentStreams.NextSegmentId(currentSegmentId);

            var timeInfo = segment.Period.ToString();
            LogInfo($"Segment: {responseResult.SegmentId} enqueued {timeInfo}");

            return DownloadLoopStatus.Continue;
        }

        private void InitDataDownloaded()
        {
            LogInfo("Segment: INIT enqueued.");
            initInProgress = false;
        }

        private DownloadLoopStatus HandleFailedDownload(Task response)
        {
            var errorMessage = GetErrorMessage(response);
            LogError(errorMessage);
            var exception = response.Exception?.Flatten().InnerExceptions[0] as DashDownloaderException;
            if (IsDynamic)
            {
                var statusCode = exception?.StatusCode;
                if (statusCode == HttpStatusCode.NotFound)
                    currentSegmentId = currentStreams.NextSegmentId(currentSegmentId);
                return DownloadLoopStatus.Continue;
            }

            StopAsync();

            if (!IsDynamic)
                errorSubject.OnNext(errorMessage);

            return DownloadLoopStatus.GiveUp;
        }

        private void HandleFailedInitDownload(string message)
        {
            LogError(message);
            initInProgress = false;
            StopAsync();
            errorSubject.OnNext(message);
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
            WaitForTaskCompletionNoError(downloadCompletedTask);
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

            // Set state variables to initial values
            lastDownloadSegmentTimeRange = null;
            currentSegmentId = null;
            isEosSent = false;
            currentTime = TimeSpan.Zero;
            bufferTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Updates representation based on Manifest Update
        /// </summary>
        /// <param name="representation"></param>
        public void UpdateRepresentation(Representation representation)
        {
            Interlocked.Exchange(ref newRepresentation, representation);
            LogInfo("newRepresentation set");
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

            initStreamBytes.Clear();

            currentRepresentation = newRep;
            currentStreams = currentRepresentation.Segments;
            var docParams = currentStreams.GetDocumentParameters();
            if (docParams == null)
                throw new ArgumentNullException("currentStreams.GetDocumentParameters() returns null");

            currentStreamDuration = IsDynamic
                ? docParams.Document.MediaPresentationDuration
                : currentStreams.Duration;

            if (lastDownloadSegmentTimeRange == null)
            {
                currentSegmentId = currentRepresentation.Segments.StartSegmentId();
                firstSegmentClock = currentStreams.SegmentTimeRange(currentSegmentId).Start;
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
        }

        private void SendEosEvent()
        {
            chunkReadySubject.OnNext(null);

            isEosSent = true;
        }

        private bool IsEndOfContent(TimeSpan time)
        {
            var endTime = !currentStreamDuration.HasValue || currentStreamDuration.Value == TimeSpan.Zero
                ? TimeSpan.MaxValue
                : currentStreamDuration.Value;
            return time >= endTime;
        }

        private Task<DownloadResponse> CreateDownloadTask(Segment segment, bool ignoreError, uint? segmentId,
            Action<byte[]> chunkDownloadedHandler, CancellationToken cancelToken)
        {
            return Task.Run(async () =>
            {
                var timeout = CalculateDownloadTimeout(segment);
                Logger.Info($"Calculated download timeout is {timeout.TotalMilliseconds}");
                var requestData = new DownloadRequest
                {
                    DownloadSegment = segment,
                    IgnoreError = ignoreError,
                    SegmentId = segmentId,
                    StreamType = streamType,
                    DataDownloaded = chunkDownloadedHandler
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
            }, cancelToken);
        }

        private TimeSpan CalculateDownloadTimeout(Segment segment)
        {
            if (!IsDynamic)
                return TimeSpan.MaxValue;
            var timeout = DataClockProviderConfig.TimeBufferDepthDefault;
            var averageThroughput = throughputHistory.GetAverageThroughput();
            if (averageThroughput > 0 && currentRepresentation.Bandwidth.HasValue && segment.Period != null)
            {
                var bandwidth = currentRepresentation.Bandwidth.Value;
                var duration = segment.Period.Duration.TotalSeconds;
                var segmentSize = bandwidth * duration;
                var calculatedTimeNeeded = TimeSpan.FromSeconds(segmentSize / averageThroughput * 1.5);
                var docParams = currentStreams.GetDocumentParameters();
                if (docParams == null)
                    throw new ArgumentNullException("currentStreams.GetDocumentParameters() returns null");
                var manifestMinBufferDepth =
                    docParams.Document.MinBufferTime ?? TimeSpan.Zero;
                timeout = calculatedTimeNeeded > manifestMinBufferDepth ? calculatedTimeNeeded : manifestMinBufferDepth;
            }

            return timeout;
        }

        public IObservable<string> ErrorOccurred()
        {
            return errorSubject.AsObservable();
        }

        public IObservable<Unit> Ready()
        {
            return readySubject.AsObservable();
        }

        public IObservable<byte[]> ChunkReady()
        {
            return chunkReadySubject.AsObservable();
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
            errorSubject.Dispose();
            readySubject.Dispose();
            chunkReadySubject.Dispose();
            disposedValue = true;
        }

        #endregion
    }
}
