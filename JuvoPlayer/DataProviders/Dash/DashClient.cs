using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.SharedBuffers;
using MpdParser.Node;
using Representation = MpdParser.Representation;
using MpdParser.Node.Dynamic;
using System.Collections.Generic;

namespace JuvoPlayer.DataProviders.Dash
{

    internal class DashClient : IDashClient
    {
        private static readonly string Tag = "JuvoPlayer";
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);
        private static readonly TimeSpan TimeBufferDepthDefault = TimeSpan.FromSeconds(10);
        private static TimeSpan TimeBufferDepth = TimeBufferDepthDefault;
        private static readonly int MaxRetryCount = 3;

        private readonly ISharedBuffer sharedBuffer;
        private readonly StreamType streamType;
        private readonly AutoResetEvent timeUpdatedEvent = new AutoResetEvent(false);

        private Representation currentRepresentation;
        private Representation newRepresentation;
        private TimeSpan currentTime = TimeSpan.Zero;
        private TimeSpan bufferTime = TimeSpan.Zero;
        private uint? currentSegmentId = null;

        private bool playback;
        private IRepresentationStream currentStreams;
        private TimeSpan? currentStreamDuration;

        private byte[] initStreamBytes;
        private Task downloadTask;

        /// <summary>
        /// Download queue with max concurrent download counts.
        /// Linked List acts as FIFO for download request. New requests being pushed at the front
        /// of the queue, processing is done from end of the queue. This assures in-request-order
        /// placement of recieved data to the player regardless of their arrival order.
        /// </summary>
        private static readonly int maxSegmentDownloads = 2;
        private LinkedList<DownloadRequest> downloadRequestPool = new LinkedList<DownloadRequest>();

        /// <summary>
        /// Internal objects used for "suspending" player. Caller will hang on a semaphore
        /// untill player suspends. After that control is released to caller. 
        /// Player will hang on the same sempahore untill resume is called.
        /// </summary>
        private SemaphoreSlim PlayerSuspend = new SemaphoreSlim(0, 1);
        private bool Suspended = false;
        private TimeRange lastRequestedPeriod = null;

        /// <summary>
        /// Flags & timeouts used to limit number of messages displayed
        /// when waiting for time / downloads. Those messages will be displayed 
        /// only if time update arrives from underlying player or when 5 second timeout 
        /// is reached. There is not much point in seeing same messages over & over.
        /// dataTimeout defines time a client will wait for pending network IO
        /// clocktickTimeout defines time a client will wait for clock update from unerlying player
        /// </summary>
        private bool timeUpdated = false;
        private TimeSpan lastMessageTimeout = TimeSpan.Zero;
        private static readonly TimeSpan maxMessageTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan dataTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan clocktickTimeout = TimeSpan.FromMilliseconds(500);

        // Action holders for processing download results.
        private readonly Action<byte[], DownloadRequestData> ActionHolderInitSegmentDownloadOK = null;
        private readonly Action<byte[], DownloadRequestData> ActionHolderSegmentDownloadOK = null;
        private readonly Action<Exception, DownloadRequestData> ActionHolderStaticSegmentDownloadFailed = null;
        private readonly Action<Exception, DownloadRequestData> ActionHolderDynamicSegmentDownloadFailed = null;

        // Action users for OK/Fail. They will be changed based on dynamic flag.
        // this will allow to have "less" if this then thats in client loop
        private Action<Exception, DownloadRequestData> ActionSegmentDownloadFailed = null;

        /// <summary>
        /// Buffer full accessor.
        /// true - Underlying player recieved MagicBufferTime ammount of data
        /// false - Underlying player has at least some portion of MagicBufferTime left and can
        /// continue to accept data.
        /// 
        /// Buffer full is an indication of how much data (in units of time) has been pushed to the player.
        /// MagicBufferTime defines how much data (in units of time) can be pushed before Client needs to
        /// hold off further pushes. 
        /// TimeTicks (current time) recieved from the player are an indication of how much data (in units of time)
        /// player consumed.
        /// A difference between buffer time (data being pushed to player in units of time) and current tick time (currentTime)
        /// defines how much data (in units of time) is in the player and awaits presentation.
        /// </summary>
        private bool BufferFull
        {
            get
            {
                return ((bufferTime - currentTime) > TimeBufferDepth);
            }
        }

        /// <summary>
        /// DownloadSlotsAvailable accessor. Check if max concurrent segment download count
        /// exceeds number of items in download queue.
        /// </summary>
        private bool DownloadSlotsAvailable
        {
            get
            {
                return (downloadRequestPool.Count < maxSegmentDownloads);
            }
        }


        public DashClient(ISharedBuffer sharedBuffer, StreamType streamType)
        {
            this.sharedBuffer = sharedBuffer ?? throw new ArgumentNullException(nameof(sharedBuffer), "sharedBuffer cannot be null");
            this.streamType = streamType;

            // Get actions for download handlers. Do it once as it is "costly-ish" operation.
            ActionHolderInitSegmentDownloadOK = InitSegmentDownloadOK;
            ActionHolderSegmentDownloadOK = SegmentDownloadOK;
            ActionHolderStaticSegmentDownloadFailed = StaticSegmentDownloadFailed;
            ActionHolderDynamicSegmentDownloadFailed = DynamicSegmentDownloadFailed;

        }

        ~DashClient()
        {
            downloadRequestPool.Clear();
        }

        /// <summary>
        /// Selects download fail handlers to be used based on current document type.
        /// </summary>
        private void SelectDownloadHandlers()
        {
            if(currentRepresentation.Parameters.Document.IsDynamic == true)
            {
                ActionSegmentDownloadFailed = ActionHolderDynamicSegmentDownloadFailed;
            }
            else
            {
                ActionSegmentDownloadFailed = ActionHolderStaticSegmentDownloadFailed;
            }

        }

        /// <summary>
        /// Suspend/Resume player API. Suspend, suspends player. 
        /// Caller will be blocked until player is actually suspended. 
        /// After suspension, control to caller
        /// will be released.
        /// </summary>
        public void Suspend()
        {
            if (Suspended == false)
            {
                Logger.Info($"{streamType} Requesting Suspend. Suspended={Suspended}");
                Suspended = true;
                PlayerSuspend.Wait();
                Logger.Info($"{streamType} Suspend Request Completed");
            }
            else
            {
                Logger.Warn($"{streamType} Already Suspended");
            }
        }

        /// <summary>
        /// Intarnal suspend method. Called by Dowload thread. If there 
        /// is a pending suspend request, download thread will release caller
        /// and suspend itself of a semaphore awaiting Resume() call which will release
        /// download thread and allow it to continue
        /// </summary>
        /// <returns></returns>
        private bool DoSuspend()
        {
            if (Suspended == true)
            {
                PlayerSuspend.Release();
                Logger.Info($"{streamType} Suspended={Suspended}");
                PlayerSuspend.Wait();
                Logger.Info($"{streamType} Suspend Completed {Suspended}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Suspend/Resume Player API. Resumes player by signalling semaphore on which download
        /// thread is waiting.
        /// </summary>
        public void Resume()
        {
            if (Suspended == true)
            {
                Logger.Info($"{streamType} Resumed Request. Suspended={Suspended}");
                Suspended = false;
                PlayerSuspend.Release();
                Logger.Info($"{streamType} Resumed Request Completed");
            }
            else
            {
                Logger.Warn($"{streamType} Not Suspended");
            }
        }

        public TimeSpan Seek(TimeSpan position)
        {
            Logger.Info(string.Format("{0} Seek to: {1} ", streamType, position));

            var segmentId = currentStreams?.MediaSegmentAtTime(position);
            if (segmentId.HasValue == true)
            {
                currentTime = position;
                currentSegmentId = segmentId.Value;

                return currentStreams.MediaSegmentAtPos(currentSegmentId.Value).Period.Start;
            }

            return TimeSpan.Zero;
        }

        public void Start()
        {
            if (currentRepresentation == null)
                throw new Exception("currentRepresentation has not been set");

            Logger.Info(string.Format("{0} DashClient start.", streamType));
            playback = true;

            currentStreams = currentRepresentation.Segments;
            SelectDownloadHandlers();

            downloadTask = Task.Factory.StartNew(DownloadThread, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            // playback has been already stopped
            if (!playback)
                return;

            playback = false;
            timeUpdatedEvent.Set();

            sharedBuffer?.WriteData(null, true);
            downloadTask.Wait();

            Logger.Info(string.Format("{0} Data downloader stopped", streamType));
        }

        
        public void SetRepresentation(Representation representation)
        {
            // representation has changed, so reset initstreambytes
            if (currentRepresentation != null)
                initStreamBytes = null;

            currentRepresentation = representation;
        }

        /// <summary>
        /// Updates representation based on Manifest Update
        /// </summary>
        /// <param name="representation"></param>

        public void UpdateRepresentation(Representation representation)
        {
            if (currentRepresentation.Parameters.Document.IsDynamic == false)
                return;

            Interlocked.Exchange<Representation>(ref newRepresentation, representation);
            Logger.Info($"{streamType}: newRepresentation set");
        }

        /// <summary>
        /// Swaps updated representation based on Manifest Reload.
        /// Updates segment information and base segment ID for the stream.
        /// </summary>
        /// <returns>bool. True. Representations were swapped. False otherwise</returns>
        private bool SwapRepresentation()
        {
            // Exchange updated representation with "null". On subsequent calls, this will be an indication
            // that there is no new representations.
            Representation newRep;
            newRep = Interlocked.Exchange<Representation>(ref newRepresentation, null);

            // Update internals with new representation if exists.
            if (newRep == null)
                return false;

            currentRepresentation = newRep;
            currentStreams = currentRepresentation.Segments;
            currentStreamDuration = currentStreams.Duration;
            TimeBufferDepth = currentStreams.Parameters.Document.MinBufferTime??TimeBufferDepthDefault;

            SelectDownloadHandlers();

            uint? newSeg = null;
            if (lastRequestedPeriod != null)
            {
                newSeg = currentStreams.RefreshMediaSegment(currentSegmentId, lastRequestedPeriod);
            }
            else 
            {
                newSeg = currentStreams.GetStartSegment(currentTime, TimeBufferDepth);
            }

            if (newSeg.HasValue == false)
            {
                Logger.Warn($"{streamType}: Segment {currentSegmentId} failed to refresh.");
            }

            currentSegmentId = newSeg;

            Logger.Info($"{streamType}: Representations swapped. New duration: {currentStreamDuration}");
            return true;
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            currentTime = time;

            try
            {
                //this can throw when event is received after Dispose() was called
                timeUpdatedEvent.Set();
            }
            catch
            {
                // ignored
            }
            finally
            {

                Logger.Info($"{streamType}: TimeSync {currentTime}");
                timeUpdated = false;
            }
        }

        /// <summary>
        /// Unified method for Time Update wait with optional
        /// user message (reason for wait). Internal flag timeUpdate
        /// is used to prevent multiple message printouts.
        /// </summary>
        /// <param name="waitReason">Additional message to be displayed</param>
        private void WaitForUpdate(string waitReason)
        {
            // Wait on Download Request (if in progress)
            // or timer event. 

            // If all download slots are occupied & first issued download request
            // is still running, wait on download task.
            var firstPendingRequest = downloadRequestPool.Last?.Value.DownloadTask;
            if (DownloadSlotsAvailable == false && firstPendingRequest?.Status < TaskStatus.RanToCompletion)
            {
                if (timeUpdated == false)
                {
                    timeUpdated = true;
                    lastMessageTimeout = TimeSpan.Zero;
                    Logger.Info($"{streamType}: {waitReason} DataWait.");
                }

                try
                {
                    firstPendingRequest?.Wait(dataTimeout);
                }
                catch (Exception) { timeUpdated = false; }   // Dummy catcher

                lastMessageTimeout += dataTimeout;
            }
            // Otherwise, check if buffers are full & wait on time update.
            else if (BufferFull == true)
            {
                if (timeUpdated == false)
                {
                    timeUpdated = true;
                    lastMessageTimeout = TimeSpan.Zero;
                    Logger.Info($"{streamType}: {waitReason} SyncWait. {currentTime}");
                }

                try
                {
                    timeUpdatedEvent.WaitOne(clocktickTimeout);
                }
                catch (Exception){}    //Dummy catrcher

                lastMessageTimeout += clocktickTimeout;
            }
            
            // 5sec. timeout on repeated messages.
            if (lastMessageTimeout >= maxMessageTimeout)
            {
                lastMessageTimeout = TimeSpan.Zero;
                timeUpdated = false;
            }
           
        }

        private void DownloadThread()
        {
            // clear garbage before appending new data
            sharedBuffer?.ClearData();
            TimeBufferDepth = currentStreams.Parameters.Document.MinBufferTime ?? TimeSpan.FromSeconds(10);
            var initSegment = currentStreams.InitSegment;
            if (initSegment != null)
            {
                Logger.Info($"{streamType}: Requesting Init segment {initSegment.Url}");
                var request = DownloadRequest(initSegment, streamType, null,
                    ActionHolderInitSegmentDownloadOK, ActionHolderStaticSegmentDownloadFailed);
                if (request == null)
                {
                    Stop();
                    return;
                }
            }
            else
            {
                WaitForUpdate($"{streamType}: No Init segment to request");
            }

            if(currentSegmentId.HasValue == false)
                currentSegmentId = currentStreams.GetStartSegment(currentTime, TimeBufferDepth);

            while (playback)
            {
           
                // Check suspend request
                DoSuspend();

                // Process any pending downloads.
                ProcessRequests();

                // Nothing is waiting for transfer to player at this stage 
                // (second chunk may, but it will be processed at next iteration)

                // After Manifest update refresh value of current segment to be downloaded as with new
                // Manifest, its ID may be very different then last.
                SwapRepresentation();

                if (currentSegmentId.HasValue == false)
                {
                    WaitForUpdate($"CurrentSegmentId is NULL");
                    continue;
                }

                // If underlying buffer is full & there are no download slots, wait for time update.
                if (BufferFull == true && DownloadSlotsAvailable == false)
                {
                    WaitForUpdate($"Buffer Full ({bufferTime}-{currentTime}) {bufferTime - currentTime} > {TimeBufferDepth}.");
                    continue;
                }

                // Get new stream.
                // there should be NO case we get NULL stram at this point. This case is handled
                // when swapping representations... but check for such scenario anyway.
                var stream = currentStreams.MediaSegmentAtPos(currentSegmentId.Value);
                if (stream == null)
                {
                    if (currentRepresentation.Parameters.Document.IsDynamic == true)
                    {
                        WaitForUpdate($"Segment {currentSegmentId} Null Dynamic Manifest. Waiting for time/manifest update");
                        continue;
                    }

                    Logger.Warn($"{currentSegmentId} Null Content. Static Manifest. Stopping Player.");
                    Stop();
                    return;
                }

                // Download new segment. NULL indicates there are no download slots left.
                // this may occour if buffer is not full, but slots are (which is kind of odd)
                var downloadRequest = DownloadRequest(stream, streamType, currentSegmentId);
                if (downloadRequest == null)
                {
                    WaitForUpdate($"No download slots available. Max {maxSegmentDownloads}");
                    continue;
                }

                // Get timing information from last requested segment. 
                // Used for finding new items when MPD is updated.
                lastRequestedPeriod = stream.Period.Copy();

                ++currentSegmentId;

                if (CheckEndOfContent() == false)
                    continue;
        
                // Before giving up, for dynamic content, re-check if there is a pending manifest update
                Logger.Warn($"{streamType} End of content. BuffTime {bufferTime} StreamDuration {currentStreamDuration}");
                this.downloadTask.Wait(1000);
                Stop();

            }
        }

        private bool CheckEndOfContent()
        {
            var EndTime = (currentStreamDuration ?? TimeSpan.MaxValue);
            if (currentTime < EndTime)
                return false;

            if (currentRepresentation.Parameters.Document.IsDynamic == true)
            {
                Logger.Info($"{streamType}: End of content? Playback Time: {currentTime} Stream Duration {EndTime}");

                /*
                var waitTime = currentRepresentation.Parameters.Document.MinimumUpdatePeriod ?? new TimeSpan(0, 0, 5);
                var reloadedAgo = DateTime.UtcNow - currentRepresentation.Parameters.Document.ManifestParseCompleteTime;
                var delayForManifest = Math.Min(waitTime.TotalSeconds, reloadedAgo.TotalSeconds);
                
                Logger.Info($"{streamType}: Dynamic Manifest. Waiting 
                */

                if (SwapRepresentation() == true)
                {
                    Logger.Info($"{streamType}: No End of content.  Playback Time: {currentTime} Stream Duration {currentStreamDuration}");
                    return true;
                }
            }

            return false;
        }
        /// <summary>
        /// Downloads a segment by creating a download request and scheduling it for download
        /// </summary>
        /// <param name="aStream">Stream which is to be downloaded</param>
        /// <param name="aType">Optional. StreamType. Used for debug message purposes</param>
        /// <param name="aSegmentID">Optional Segment ID. Used for debug message purposes</param>
        /// <param name="downloadOK">Optional. Overrides DashClient's current Download Sucessfull handler</param>
        /// <param name="downloadFAIL">Optional. Overrides DashClient's current Download Failed handler</param>
        /// <returns></returns>
        private DownloadRequest DownloadRequest(MpdParser.Node.Dynamic.Segment aStream,
            StreamType? aType = null, uint? aSegmentID = null,
            Action<byte[], DownloadRequestData> downloadOK = null,
            Action<Exception, DownloadRequestData> downloadFAIL = null)
        {
            if (DownloadSlotsAvailable == false)
            {
                return null;
            }

            var request = new DownloadRequest(aStream, aType, aSegmentID);
            request.RanToCompletion = downloadOK ?? ActionHolderSegmentDownloadOK;
            request.Faulted = downloadFAIL ?? ActionSegmentDownloadFailed;
            request.RequestFailed = ActionHolderStaticSegmentDownloadFailed;
            request = downloadRequestPool.AddFirst(request).Value;

            request.Download();

            return request;

        }

        /// <summary>
        /// Wrapper on processing download requests. Processing (data transfer/error handling)
        /// is not done manually in main lopp. Intentional. Allows easer managment of in-order
        /// transfer of downloaded data.
        /// </summary>
        private void ProcessRequests()
        {
            // Process is "self cleaning".
            // i.e. Downloaded tasks shall self remove.
            // failed tasks shall re-schedule themselve or
            // remove on failure.
            downloadRequestPool.Last?.Value.Process();
        }

        /// <summary>
        /// Download handler for init segment.
        /// </summary>
        /// <param name="data">requested data returned by WebClient</param>
        /// <param name="requestParams">request parameters</param>
        private void InitSegmentDownloadOK(byte[] data, DownloadRequestData requestParams)
        {
            initStreamBytes = data;

            if (initStreamBytes != null)
                sharedBuffer.WriteData(initStreamBytes);

            downloadRequestPool.Last.Value.Dispose();
            downloadRequestPool.RemoveLast();

            Logger.Info($"{requestParams.StreamType}: Init segment downloaded.");
        }

        /// <summary>
        /// Segment donwload handler. Applicable to both, Static & dynamic segments.
        /// </summary>
        /// <param name="data">requested data returned by WebClient</param>
        /// <param name="requestParams">request parameters</param>
        private void SegmentDownloadOK(byte[] data, DownloadRequestData requestParams)
        {
            
            // If buffer is full, return without removing request from request pool.
            // will be serviced at next iteration.
            if (BufferFull == true)
                return;

            sharedBuffer.WriteData(data);
            bufferTime += requestParams.DownloadSegment.Period.Duration;
            var timeInfo = requestParams.DownloadSegment.Period.ToString();

            downloadRequestPool.Last.Value.Dispose();
            downloadRequestPool.RemoveLast();

            Logger.Info($"{streamType}: Recieved segment: {requestParams.SegmentID} {timeInfo}");
        }

        /// <summary>
        /// Static Segment download fail handler. Provides for retries with (if not exceeded)
        /// </summary>
        /// <param name="ex">Exception which caused failure</param>
        /// <param name="requestParams">request parameters</param>
        private void StaticSegmentDownloadFailed(Exception ex, DownloadRequestData requestParams)
        {
            WebException exw = ex as WebException;
            if (exw == null)
            {
                Logger.Error($"{streamType}: Segment {requestParams.SegmentID} Error: {ex.Message} {ex.TargetSite} {ex.StackTrace}");
            }
            else
            {
                Logger.Warn($"{streamType}: Segment: {requestParams.SegmentID} download failed. Retrying. {exw.Message}");
            }


            if (downloadRequestPool.Last.Value.DownloadErrorCount >= MaxRetryCount)
            {
                Logger.Error($"{streamType}: Segment {requestParams.SegmentID}. Max retry count reached. Stoping Player.");

                downloadRequestPool.Last.Value.Dispose();
                downloadRequestPool.RemoveLast();

                Stop();
                return;
            }

            downloadRequestPool.Last.Value.Download();
        }

        /// <summary>
        /// Dynamic Segment download fail handler.
        /// </summary>
        /// <param name="ex">Exception which caused failure</param>
        /// <param name="requestParams">request parameters</param>
        private void DynamicSegmentDownloadFailed(Exception ex, DownloadRequestData requestParams)
        {
            WebException exw = ex as WebException;

            downloadRequestPool.Last.Value.Dispose();
            downloadRequestPool.RemoveLast();


            if (exw == null)
            {
                Logger.Error($"{streamType}: Segment {requestParams.SegmentID} Error: {ex.Message} {ex.TargetSite} {ex.StackTrace}");
                return;
            }


            // Should we check for 404s only? 
            Logger.Info($"{streamType}: Segment: {requestParams.SegmentID} download failed. Skipping. {exw.Message}");
        }
    }
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

                if(Low > High )
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
    internal class DownloadRequestData
    {
        public MpdParser.Node.Dynamic.Segment DownloadSegment { get; internal set; }
        public uint? SegmentID { get; internal set; }
        public StreamType? StreamType { get; internal set; }

        public DownloadRequestData(MpdParser.Node.Dynamic.Segment aSegment, StreamType? aType = null, uint? aSegmentID = null)
        {
            DownloadSegment = aSegment;
            SegmentID = aSegmentID;
            StreamType = aType;
        }
    }

    /// <summary>
    /// Download request class for handling download requests.
    /// </summary>
    internal class DownloadRequest : IDisposable
    {
        private const string Tag = "JuvoPlayer";

        /// <summary>
        /// WebClient Task as returned by Async requests.
        /// </summary>
        public Task<byte[]> DownloadTask { get; internal set; } = null;

        /// <summary>
        /// Request Task - Task which downloads data in asynchronous way.
        /// </summary>
        public Task RequestTask { get; internal set; } = null;

        /// <summary>
        /// Download request data associated with this instance of request.
        /// </summary>
        public DownloadRequestData requestData { get; internal set; } = null;

        private ByteRange downloadRange = null;
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        /// <summary>
        /// DownloadTask state handlers. Will be called, Task State Dependant, when Process() is executed
        /// </summary>
        public Action<DownloadRequestData> Created { get; set; } = null;
        public Action<DownloadRequestData> WaitingForActivation { get; set; } = null;
        public Action<DownloadRequestData> WaitingToRun { get; set; } = null;
        public Action<DownloadRequestData> Running { get; set; } = null;
        public Action<DownloadRequestData> WaitingForChildrenToComplete { get; set; } = null;
        public Action<byte[], DownloadRequestData> RanToCompletion { get; set; } = null;
        public Action<DownloadRequestData> Canceled { get; set; } = null;
        public Action<Exception, DownloadRequestData> Faulted { get; set; } = null;
        public Action<Exception, DownloadRequestData> RequestFailed { get; set; } = null;

        /// <summary>
        /// Download error counter
        /// </summary>
        public int DownloadErrorCount { get; internal set; } = 0;

        /// <summary>
        /// Accessor to get the data from Download Task
        /// i.e. Download results.
        /// </summary>
        public byte[] Data
        {
            get
            {
                if (DownloadTask.IsCompleted == true)
                    return DownloadTask.Result;

                return null;
            }
        }

        /// <summary>
        /// Creates a Download Request object
        /// </summary>
        /// <param name="aStream">Stream to be downloaded</param>
        /// <param name="aType">Optional. Stream type. Used for debug messages</param>
        /// <param name="aSegmentID">Optional. StrweamID. Used for debug messages.</param>
        public DownloadRequest(MpdParser.Node.Dynamic.Segment aSegment,
            StreamType? aType = null, uint? aSegmentID = null)
        {

            requestData = new DownloadRequestData(aSegment, aType, aSegmentID);

            if (!String.IsNullOrEmpty(aSegment.ByteRange))
                downloadRange = new ByteRange(aSegment.ByteRange);

        }

        /// <summary>
        /// External API for issuing a download. Calls internal Task method to preforma actual
        /// work related with download startup.
        /// </summary>
        public void Download()
        {
            RequestTask = Task.Run(() => DownloadInternal());
        }
        private void DownloadInternal()
        {
            // I am opened to suggestions on WebClient reuse.
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

                try
                {
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

                        Task.Delay(sleepTime).Wait();

                        //Check for being disposed. If so, do not proceed.
                        if (disposedValue == true)
                        {
                            Logger.Warn($"{requestData.StreamType}: Downloader Disposed. Segment: {requestData.SegmentID} Requested. URL: {requestData.DownloadSegment.Url}.");
                            return;
                        }
                    }

                    // In case of reloads, get rid off previous DownloadTask.
                    DownloadTask?.Dispose();

                    DownloadTask = dataDownloader.DownloadDataTaskAsync(requestData.DownloadSegment.Url);

                    Logger.Info($"{requestData.StreamType}: Segment: {requestData.SegmentID} Requested. URL: {requestData.DownloadSegment.Url} Range: {downloadRange?.ToString()}");

                    DownloadTask.Wait();

                }
                catch(Exception)
                {
                    //Dummy catcher. Error handling is done during common Process of all awaiters
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
                RequestFailed?.Invoke(RequestTask.Exception, requestData);
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
                    Faulted?.Invoke(DownloadTask.Exception.InnerException, requestData);
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
