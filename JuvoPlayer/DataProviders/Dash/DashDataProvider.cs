using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Subtitles;
using MpdParser;
using AlignmentEntry = System.Tuple<uint?, System.TimeSpan, uint?, System.TimeSpan, System.TimeSpan>;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class AlignmentEntryComparer : IComparer<AlignmentEntry>
    {
        public int Compare(AlignmentEntry x, AlignmentEntry y)
        {
            return (int)(x.Item5.Ticks - y.Item5.Ticks);
        }
    }

    internal class DashDataProvider : IDataProvider
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private readonly DashManifest manifest;
        private DashMediaPipeline audioPipeline;
        private DashMediaPipeline videoPipeline;
        private readonly List<SubtitleInfo> subtitleInfos = new List<SubtitleInfo>();
        private CuesMap cuesMap;
        private TimeSpan currentTime = TimeSpan.Zero;

        private static readonly int maxManifestDownloadRetries = 3;
        private static readonly TimeSpan manifestReloadDelay = TimeSpan.FromMilliseconds(1500);

        public event ClipDurationChanged ClipDurationChanged;
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event PacketReady PacketReady;
        public event StreamError StreamError;

        private bool disposed;
        private bool isDynamic;

        private DateTime? documentPublishTime;

        private Timer manifestReloadTimer;
        private int manifestDownloadRetries;

        private readonly SemaphoreSlim updateInProgressLock = new SemaphoreSlim(1);

        public DashDataProvider(
            DashManifest manifest,
            DashMediaPipeline audioPipeline,
            DashMediaPipeline videoPipeline)
        {
            this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest), "manifest cannot be null");
            this.audioPipeline = audioPipeline ?? throw new ArgumentNullException(nameof(audioPipeline), "audioPipeline cannot be null");
            this.videoPipeline = videoPipeline ?? throw new ArgumentNullException(nameof(videoPipeline), "videoPipeline cannot be null");

            audioPipeline.DRMInitDataFound += OnDRMInitDataFound;
            audioPipeline.SetDrmConfiguration += OnSetDrmConfiguration;
            audioPipeline.StreamConfigReady += OnStreamConfigReady;
            audioPipeline.PacketReady += OnPacketReady;
            audioPipeline.StreamError += OnStreamError;

            videoPipeline.DRMInitDataFound += OnDRMInitDataFound;
            videoPipeline.SetDrmConfiguration += OnSetDrmConfiguration;
            videoPipeline.StreamConfigReady += OnStreamConfigReady;
            videoPipeline.PacketReady += OnPacketReady;
            videoPipeline.StreamError += OnStreamError;
        }

        private async void OnManifestReload(Object state)
        {
            Logger.Info("");

            if (!updateInProgressLock.Wait(0))
                return;

            try
            {
                // Stale event check.
                if (manifestReloadTimer == null)
                    return;

                var updateResult = await UpdateManifest();

                // Failed update. Retry with default timeout
                if (updateResult == false)
                {
                    // Check if number of retries exceeds predefined limit.
                    // If it does, stop playback and raise error.
                    manifestDownloadRetries++;
                    if (manifestDownloadRetries > maxManifestDownloadRetries)
                    {
                        OnStopped();
                        OnStreamError("Manifest download failed");
                        return;
                    }

                    manifestReloadTimer?.Change(manifestReloadDelay, TimeSpan.FromMilliseconds(-1));
                    return;
                }

                // Got manifest. Reset retry counter
                manifestDownloadRetries = 0;

                Parallel.Invoke(() => audioPipeline.SwitchStreamIfNeeded(),
                                () => videoPipeline.SwitchStreamIfNeeded());

                if (manifest.CurrentDocument.IsDynamic)
                {
                    var reloadTime = manifest.CurrentDocument.MinimumUpdatePeriod ?? manifestReloadDelay;

                    // For zero minimum update periods (aka, after every chunk) use default reload.
                    if (reloadTime == TimeSpan.Zero)
                        reloadTime = manifestReloadDelay;

                    manifestReloadTimer?.Change(reloadTime, TimeSpan.FromMilliseconds(-1));
                }
                else
                {
                    // Static content. Reloads not needed
                    manifestReloadTimer?.Dispose();
                    manifestReloadTimer = null;
                }
            }
            catch (Exception e) { }
            finally
            {
                updateInProgressLock.Release();
            }
        }

        private void OnDRMInitDataFound(DRMInitData drmData)
        {
            DRMInitDataFound?.Invoke(drmData);
        }

        private void OnSetDrmConfiguration(DRMDescription description)
        {
            SetDrmConfiguration?.Invoke(description);
        }

        private void OnStreamConfigReady(StreamConfig config)
        {
            StreamConfigReady?.Invoke(config);
        }

        private void OnPacketReady(Packet packet)
        {
            PacketReady?.Invoke(packet);
        }

        public void OnChangeActiveStream(StreamDescription stream)
        {
            switch (stream.StreamType)
            {
                case StreamType.Audio:
                    audioPipeline.ChangeStream(stream);
                    break;
                case StreamType.Video:
                    videoPipeline.ChangeStream(stream);
                    break;
                case StreamType.Subtitle:
                    OnChangeActiveSubtitleStream(stream);
                    break;
                default:
                    break;
            }
        }

        public void OnDeactivateStream(StreamType streamType)
        {
            if (streamType == StreamType.Subtitle)
            {
                OnDeactivateSubtitleStream();
                return;
            }
            throw new NotImplementedException();
        }

        private void OnDeactivateSubtitleStream()
        {
            cuesMap = null;
        }

        private void OnChangeActiveSubtitleStream(StreamDescription description)
        {
            if (description.Id >= subtitleInfos.Count)
                throw new ArgumentException("Invalid subtitle description");

            var subtitleInfo = subtitleInfos[description.Id];
            cuesMap = new SubtitleFacade().LoadSubtitles(subtitleInfo);
        }

        public void OnPaused()
        {
            Logger.Info("");
        }

        public async void OnPlayed()
        {
            Parallel.Invoke(() => audioPipeline.SwitchStreamIfNeeded(),
                            () => videoPipeline.SwitchStreamIfNeeded());
        }

        public void OnSeek(TimeSpan time)
        {
            if (!IsSeekingSupported())
                return;
               
            Parallel.Invoke(() => videoPipeline.Pause(), () => audioPipeline.Pause());
            Parallel.Invoke(() => videoPipeline.Seek(time), () => audioPipeline.Seek(time));
            Parallel.Invoke(() => videoPipeline.Resume(), () => audioPipeline.Resume());
        }

        public void OnStopped()
        {
            //Before disabling manifest update timer, make sure it is not in the middle of an update
            Logger.Info("Waiting for manifest upates to complete");
            updateInProgressLock.Wait();
            Logger.Info("Manifest updates completed");

            manifestReloadTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            manifestReloadTimer?.Dispose();
            manifestReloadTimer = null;
            
            Parallel.Invoke(() => videoPipeline.Stop(), () => audioPipeline.Stop());
            updateInProgressLock.Release();
        }

        public bool IsSeekingSupported()
        {
            // NOTE: Are we sure seeking is illegal on Dynamic MPDs?
            // Imho no. There is a "time window" in which content is available
            // but leave that for now...
            return !isDynamic;
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            switch (streamType)
            {
                case StreamType.Audio:
                    return audioPipeline.GetStreamsDescription();
                case StreamType.Video:
                    return videoPipeline.GetStreamsDescription();
                case StreamType.Subtitle:
                    return subtitleInfos.Select(info => info.ToStreamDescription()).ToList();
                default:
                    return new List<StreamDescription>();
            }
        }

        public async void Start()
        {
            Logger.Info("Dash start.");

            // Clear manifest publish time & download retry counter
            documentPublishTime = null;
            manifestDownloadRetries = 0;

            manifestReloadTimer = new Timer(OnManifestReload, null, Timeout.Infinite, Timeout.Infinite);
            manifestReloadTimer.Change(0, Timeout.Infinite);
        }

        /// <summary>
        /// Function aligns Audio & Video Start Segments for dynamic content.
        /// Static content no alignment is done, just selection of start segments and trimm offsets
        /// </summary>
        /// <remarks>
        /// Video Segment is picked as start point. Segment Time Range (Start-Duration) is split
        /// into 4 separate time points between Segment.Start and Segment.Duration.
        /// For each of those points, audio segment is selected and its start time recorded.
        /// Such list is recorded for 
        /// 
        /// {Prev. Video Segment}.{Start Video Segment}.{Next Video Segment}
        /// 
        /// if prev video segment is unavailable, then video segment source list is as following:
        /// 
        /// {Start Video Segment}.{Next Video Segment}.{Next Video Segment}
        /// 
        /// Once a list of {VideoSegmentId}.{Video Start Time}.{AudioSegmentID}.{Audio Start Time}
        /// is created, it is scanned for smallest differences between Video Start Time & Audio Start Time.
        /// Audio segment ID and Video Segment ID selected for aligned start parameters will have 
        /// that difference smallest.
        /// 
        /// After selecting start IDs for audio & video, associated start times of each segment are chosen as
        /// aligned trimm Offsets.
        /// 
        /// Reason for scanning mutiple time points of video segment: A & V segments do not have to be aligned,
        /// as such there may be overlaps.
        /// 
        /// </remarks>
        /// <param name="audio">Audio representation</param>
        /// <param name="video">Video Representation</param>
        /// <param name="isDynamic">Static/Dynamic flag</param>
        /// <param name="logger">Logger object for debug output. Optional</param>
        private static void AlignStartParameters(Representation audio, Representation video, bool isDynamic, ILogger logger = null)
        {
            if (isDynamic)
                AlignDynamicStartParameters(audio, video, logger);
            else
                AlignStaticStartParameters(audio, video, logger);
        }

        private static void AlignStaticStartParameters(Representation audio, Representation video, ILogger logger = null)
        {
            var videoStartSegment = video.Segments.StartSegmentId();
            var audioStartSegment = audio.Segments.StartSegmentId();
            var videoTimeRange = video.Segments.SegmentTimeRange(videoStartSegment);
            var audioTimeRange = video.Segments.SegmentTimeRange(audioStartSegment);

            var trimmOffset = videoTimeRange.Start < audioTimeRange.Start ? videoTimeRange.Start : audioTimeRange.Start;

            //Set aligned start data
            audio.SetAlignedStartParameters(audioStartSegment, trimmOffset);
            video.SetAlignedStartParameters(videoStartSegment, trimmOffset);
            logger?.Info($"Segment Alignment: Video={videoStartSegment} Audio={audioStartSegment} TrimmOffset={trimmOffset}");
        }

        private static void AlignDynamicStartParameters(Representation audio, Representation video, ILogger logger = null)
        {
            var alignments = new List<AlignmentEntry>();

            var videoStartSegment = video.Segments.StartSegmentId();
            var videoSegmentId = video.Segments.PreviousSegmentId(videoStartSegment);

            if (!videoStartSegment.HasValue)
                videoSegmentId = videoStartSegment;

            // Scan 3 video segments starting from videoSegmentId.
            for (int s = 0; s < 3; s++)
            {
                var videoTimeRange = video.Segments.SegmentTimeRange(videoSegmentId);

                if (videoTimeRange == null)
                    continue;

                // Split duration into 3 elements to cover 4 time points in segment duration
                // Start, Start+1/3*Duration, Start+2/3*Duration, Start+3/3*Duration
                //
                var tickOffset = videoTimeRange.Duration.Ticks / 3;
                var tickStart = videoTimeRange.Start.Ticks;

                for (int t = 0; t < 4; t++)
                {
                    // Get audio SegmentId & Start time for a given time point
                    var timePoint = TimeSpan.FromTicks(tickStart + (t * tickOffset));
                    var audioSegmentId = audio.Segments.SegmentId(timePoint);
                    var audioTimeRange = audio.Segments.SegmentTimeRange(audioSegmentId);

                    if (audioTimeRange == null)
                        continue;

                    // Store valid data
                    var timeDiff = (videoTimeRange.Start - audioTimeRange.Start).Duration();
                    var entry = new AlignmentEntry(videoSegmentId, videoTimeRange.Start, audioSegmentId, audioTimeRange.Start, timeDiff);
                    alignments.Add(entry);
                }
                // Get Next video segment
                videoSegmentId = video.Segments.NextSegmentId(videoSegmentId);
            }

            var audioStartSegment = videoStartSegment = null;
            TimeSpan? trimmOffset = null;

            // handle the unexpected
            if (alignments.Count > 0)
            {
                // Sort all entries by timeDiff and pick smallest
                var comparer = new AlignmentEntryComparer();

                alignments.Sort(comparer);

                videoStartSegment = alignments[0].Item1;
                audioStartSegment = alignments[0].Item3;
                var videoStart = alignments[0].Item2;
                var audioStart = alignments[0].Item4;
                trimmOffset = videoStart < audioStart ? videoStart : audioStart;
            }

            //Set aligned start data
            audio.SetAlignedStartParameters(audioStartSegment, trimmOffset);
            video.SetAlignedStartParameters(videoStartSegment, trimmOffset);
            logger?.Info($"Segment Alignment: Video={videoStartSegment} Audio={audioStartSegment} TrimmOffset={trimmOffset}");
        }


        private bool UpdateMedia(Document document, Period period)
        {
            if (disposed)
                return false;

            // TODO:? In case of updates - should we check if A/V is any different from
            // anything we passed down before? Should offload client...
            var manifestParams = new ManifestParameters(document, period)
            {
                PlayClock = LiveClockTime(currentTime, document)
            };

            Logger.Info(period.ToString());

            List<Media> audios = null;
            List<Media> videos = null;
            Representation audioRepresentation = null;
            Representation videoRepresentation = null;
            bool videoPrepared = false;
            bool audioPrepared = false;

            Parallel.Invoke(
                () =>
                {
                    audios = period.Sets.Where(o => o.Type.Value == MediaType.Audio).ToList();
                    if (!audios.Any())
                        return;

                    foreach (var a in audios)
                    {
                        a.SetDocumentParameters(manifestParams);
                    }

                    audioPrepared = audioPipeline.UpdateMedia(audios);
                    audioRepresentation = audioPipeline.GetRepresentation();
                },
                () =>
                {
                    videos = period.Sets.Where(o => o.Type.Value == MediaType.Video).ToList();
                    if (!videos.Any())
                        return;

                    foreach (var v in videos)
                    {
                        v.SetDocumentParameters(manifestParams);
                    }

                    videoPrepared = videoPipeline.UpdateMedia(videos);
                    videoRepresentation = videoPipeline.GetRepresentation();
                });

            if (!audioPrepared || !videoPrepared)
            {
                Logger.Error($"Failed to prepare A/V streams. Video={videoPrepared} Audio={audioPrepared}");
                OnStopped();
                OnStreamError("Failed to prepare A/V streams");
                return false;
            }

            if (audios.Any() && videos.Any())
            {
                BuildSubtitleInfos(period);

                if (document.MediaPresentationDuration.HasValue && document.MediaPresentationDuration.Value > TimeSpan.Zero)
                    ClipDurationChanged?.Invoke(document.MediaPresentationDuration.Value);

                isDynamic = document.IsDynamic;

                AlignStartParameters(videoRepresentation, audioRepresentation, isDynamic, Logger);

                return true;
            }

            return false;

        }

        /// <summary>
        /// Finds a period that matches current playback timestamp. May return "early access"
        /// Period. Based on ISO IEC 23009-1 2015 section 5.3.2.1 & DASH IF IOP v 3.3
        /// </summary>
        /// <param name="document">Document for which period is to be found </param>
        /// <param name="timeIndex"></param>
        /// <returns></returns>
        private Period FindPeriod(Document document, TimeSpan timeIndex)
        {
            var timeIndexTotalSeconds = Math.Truncate(timeIndex.TotalSeconds);
            // Periods are already "sorted" from lowest to highest, thus find a period
            // where start time (time elapsed when this period should start)
            // to do so, search periods backwards, starting from those that should play last.
            for (var p = document.Periods.Length - 1; p >= 0; p--)
            {
                var period = document.Periods[p];

                var availstart = document.AvailabilityStartTime ?? DateTime.UtcNow;
                var start = document.IsDynamic ? DateTime.UtcNow.Subtract(availstart) : TimeSpan.Zero;

                start = start.Add(period.Start ?? TimeSpan.Zero);
                var end = start.Add(period.Duration ?? TimeSpan.Zero);

                Logger.Debug($"Searching: {start}-{end} {period} for TimeIndex/Current: {timeIndex}");

                if (timeIndexTotalSeconds >= Math.Truncate(start.TotalSeconds)
                    && timeIndexTotalSeconds <= Math.Truncate(end.TotalSeconds))
                {
                    Logger.Debug($"Matching period found: {period} for TimeIndex: {timeIndex}");
                    return period;
                }
            }

            Logger.Info($"No period found for TimeIndex: {timeIndex}");

            return null;
        }

        /// <summary>
        /// Gets LiveClock for provided Time Span. Returned clock will be "live" only
        /// for dynamic content. Otherwise provided time will not be changed.
        /// </summary>
        /// <param name="time">Current Time Stamp</param>
        /// <param name="document">Document. A document which will be used to retrieve isLive information</param>
        /// <returns>TimeSpan - Current Live Clock for dynamic mpd. It is expressed as ammount
        /// of time has passed since Document.AvailabilityStartTime
        /// For static content, time passed as argument is returned.</returns>
        private static TimeSpan LiveClockTime(TimeSpan time, Document document)
        {
            if (document.IsDynamic)
                time = DateTime.UtcNow.Subtract(document.AvailabilityStartTime ?? DateTime.MinValue);

            return time;
        }

        private void BuildSubtitleInfos(Period period)
        {
            subtitleInfos.Clear();

            var textAdaptationSets = period.Sets.Where(o => o.Type.Value == MediaType.Text).ToList();
            foreach (var textAdaptationSet in textAdaptationSets)
            {
                var lang = textAdaptationSet.Lang;
                var mimeType = textAdaptationSet.Type;
                foreach (var representation in textAdaptationSet.Representations)
                {
                    var mediaSegments = representation.Segments.MediaSegments().ToList();
                    if (!mediaSegments.Any()) continue;

                    var segment = mediaSegments.First();
                    var streamDescription = new SubtitleInfo
                    {
                        Id = subtitleInfos.Count,
                        Language = lang,
                        Path = segment.Url.ToString(),
                        MimeType = mimeType?.Key
                    };

                    subtitleInfos.Add(streamDescription);
                }
            }
        }

        public Cue CurrentCue => cuesMap?.Get(currentTime);

        public async void OnTimeUpdated(TimeSpan time)
        {
            currentTime = time;

            if (disposed)
                return;

            Parallel.Invoke(
                () =>
                {
                    audioPipeline.OnTimeUpdated(time);

                    if (audioPipeline.CanStreamSwitch())
                    {
                        audioPipeline.AdaptToNetConditions();
                        audioPipeline.SwitchStreamIfNeeded();
                    }
                },
                () =>
                {
                    videoPipeline.OnTimeUpdated(time);

                    if (videoPipeline.CanStreamSwitch())
                    {
                        videoPipeline.AdaptToNetConditions();
                        videoPipeline.SwitchStreamIfNeeded();
                    }
                });
        }

        public async Task<bool> UpdateManifest()
        {
            bool res = true;
                        
            Logger.Info("Updating manifest");

            try
            {
                if (!await manifest.ReloadManifestTask())
                    return false;
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Reloading manifest was cancelled");
                return false;
            }

            var tmpDocument = manifest.CurrentDocument;
            if (tmpDocument == null)
            {
                Logger.Info("No Manifest.");
                return false;
            }

            var tmpPeriod = FindPeriod(tmpDocument, LiveClockTime(currentTime, tmpDocument));
            if (tmpPeriod == null)
            {
                Logger.Info("No period in Manifest.");
                return false;
            }

            if (!documentPublishTime.HasValue)
            {
                documentPublishTime = tmpDocument.PublishTime;
                res = UpdateMedia(tmpDocument, tmpPeriod);
            }
            else
            {
                // Update document ONLY it has newer publish time then previously dowloaded manifest
                // or it is first download.
                //
                if (documentPublishTime < tmpDocument.PublishTime)
                {
                    documentPublishTime = tmpDocument.PublishTime;
                    res = UpdateMedia(tmpDocument, tmpPeriod);
                }
                else
                {
                    Logger.Info($"Manifest update skipped. Current: {tmpDocument.PublishTime} Publish time not newer then {documentPublishTime}");
                }
            }
            

            return res;
        }

        private void OnStreamError(string errorMessage)
        {
            // TODO: Review parallelization. Logging, A & V Stop, Stream Erro Invokation
            // can be safely done in parallel.
            Logger.Error($"Stream Error: {errorMessage}. Terminating pipelines.");

            // Bubble up stream error info up to PlayerController which will shut down
            // underlying player
            StreamError?.Invoke(errorMessage);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            OnStopped();

            manifest.Dispose();

            audioPipeline?.Dispose();
            audioPipeline = null;

            videoPipeline?.Dispose();
            videoPipeline = null;

            updateInProgressLock.Dispose();
        }
    }
}

