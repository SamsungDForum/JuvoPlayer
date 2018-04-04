using System;
using System.Collections.Generic;
using JuvoPlayer.Common;
using JuvoLogger;
using MpdParser;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashDataProvider : IDataProvider
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private readonly DashManifest manifest;
        private Document currentDocument = null;
        private Period currentPeriod = null;
        private DashMediaPipeline audioPipeline;
        private DashMediaPipeline videoPipeline;
        private TimeSpan currentTimeStamp = TimeSpan.Zero;
        private DateTime lastReloadTime = DateTime.MinValue;
        private Object locker = new Object();
        private TimeSpan minimumReloadPeriod = TimeSpan.Zero;

        private Task manifestLoader = null;
        private static readonly TimeSpan manifestreloadTimeout = new TimeSpan(0, 0, 10);

        public DashDataProvider(
            DashManifest manifest,
            DashMediaPipeline audioPipeline,
            DashMediaPipeline videoPipeline)
        {
            this.manifest = manifest ?? throw new ArgumentNullException(nameof(manifest), "manifest cannot be null");
            this.audioPipeline = audioPipeline ?? throw new ArgumentNullException(nameof(audioPipeline), "audioPipeline cannot be null");
            this.videoPipeline = videoPipeline ?? throw new ArgumentNullException(nameof(videoPipeline), "videoPipeline cannot be null");


            manifest.ManifestChanged += OnManifestChanged;
            audioPipeline.DRMInitDataFound += OnDRMInitDataFound;
            audioPipeline.SetDrmConfiguration += OnSetDrmConfiguration;
            audioPipeline.StreamConfigReady += OnStreamConfigReady;
            audioPipeline.PacketReady += OnPacketReady;
            videoPipeline.DRMInitDataFound += OnDRMInitDataFound;
            videoPipeline.SetDrmConfiguration += OnSetDrmConfiguration;
            videoPipeline.StreamConfigReady += OnStreamConfigReady;
            videoPipeline.PacketReady += OnPacketReady;
        }

        public event ClipDurationChanged ClipDurationChanged;
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event PacketReady PacketReady;
        public event StreamsFound StreamsFound;

        /// <summary>
        /// MPD Downloader task. Its role is to download MPD till pipeline is started
        /// and time data is recieved. There are possible cases where pipeline will not start
        /// i.e. no valid segments found, etc. In such scenarios, no timing information will be recieved.
        /// therefore this task provides a fail back mechanisms. Will be stopped when first timestamps
        /// from pipeline are recieved. After that, MPD reloads will be scheduled from time event.
        /// </summary>
        /// <returns></returns>
        private void ManifestLoader()
        {
            // Detach from caller and allow to continue in any context.
            await Task.Delay(1).ConfigureAwait(false);

            // Run this loader for as long as no reload has arrived.
            // Will be marked by lastRealoadTime.
            while (currentTimeStamp == TimeSpan.Zero)
            {
                manifest.ReloadManifest(DateTime.UtcNow);
                Task.Delay(manifestreloadTimeout).Wait();

                // Check if Document / Period has been obtanied. If so
                // and document type is static, exit loop.
                if (currentPeriod != null && currentDocument?.IsDynamic == false)
                    break;
            }

            Logger.Info($"Auto Manifest downloader disabled. TimeStamp={currentTimeStamp} Manifest={currentDocument != null} Period={currentPeriod != null}");

            // This is odd. No log messages are printed at task exit if there is not delay/action after
            // log message...
            Task.Delay(500).Wait();
        }
        /// <summary>
        /// Manifest Change Callback incoked by DashManifest class when new manifest is downloaded.
        /// </summary>
        /// <param name="newDocument">IDocument containing an updated document version</param>
        private void OnManifestChanged(IDocument newDocument)
        {
            Logger.Info($"OnManifestChanged. Processing MPD");

            // Mark time of "last" document update
            lastReloadTime = DateTime.UtcNow;

            // Temps are used at this point, we do not need to lock down this entire
            // section if current* instance variables would be used.
            var tmpDocument = newDocument as Document;
            var tmpPeriod = FindPeriod(tmpDocument, LiveClockTime(currentTimeStamp, tmpDocument));

            if (tmpPeriod == null)
            {
                manifest.ReloadManifest(DateTime.UtcNow.AddSeconds(5));
                return;
            }
            // No update period? Static content, set timeout to max
            minimumReloadPeriod = tmpDocument.MinimumUpdatePeriod ?? TimeSpan.MaxValue;

            //EarlyAvailable periods are not utilized now
            if (tmpDocument.IsDynamic == true && tmpPeriod.Type == Period.Types.EarlyAvailable)
            {
                Logger.Info($"EarlyAvailable MPD are not utilized. {tmpDocument.ToString()}. Scheduling reload in {minimumReloadPeriod}ms");
                return;
            }

            // Prevent other any other Manifest change events from being pushed down 
            // the pipeline while current event is propagated down the pipeline
            lock (locker)
            {
                // Update document/period and start player
                currentDocument = tmpDocument;
                currentPeriod = tmpPeriod;

                StartInternal();

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
        }

        public void OnPlayed()
        {
        }

        public void OnSeek(TimeSpan time)
        {
            if (!IsSeekingSupported())
                return;

            audioPipeline.Seek(time);
            videoPipeline.Seek(time);
        }

        public void OnStopped()
        {
            audioPipeline.Stop();
            videoPipeline.Stop();
        }

        public bool IsSeekingSupported()
        {
            bool isDynamic;

            lock (locker)
            {
                isDynamic = currentDocument?.IsDynamic ?? false;
            }

            // NOTE: Are we sure seeking is illegal on Dynamic MPDs?
            // Imho no. There is a "time window" in which content is available
            // but leave that for now...
            return (isDynamic == false);

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

        public void Start()
        {
            Logger.Info("Dash start.");
            manifestLoader = Task.Run(()=>ManifestLoader());

            // Synchronous start can be implemented by:
            // waiting for manifestLoader;
            
        }

        private void StartInternal()
        {
            
            if (currentDocument == null || currentPeriod == null)
            {
                Logger.Info("Dash start delayed. No Manifest/Period available.");
                return;
            }

            // TODO:? In case of updates - should we check if A/V is any different from
            // anything we passed down before? Should offload client...
            ManifestParameters manifestParams;
            manifestParams = new ManifestParameters(currentDocument, currentPeriod);
            manifestParams.PlayClock = LiveClockTime(currentTimeStamp);

            Logger.Info(currentPeriod.ToString());

            var audios = currentPeriod.Sets.Where(o => o.Type.Value == MediaType.Audio);
            var videos = currentPeriod.Sets.Where(o => o.Type.Value == MediaType.Video);

            if (audios.Count() > 0 && videos.Count() > 0)
            {

		BuildSubtitleInfos(currentPeriod);

                if (currentPeriod.Duration.HasValue)
                    ClipDurationChanged?.Invoke(currentPeriod.Duration.Value);

                foreach (var v in videos)
                {
                    v.Parameters = manifestParams;
                }

                foreach (var a in audios)
                {
                    a.Parameters = manifestParams;
                }

 

                videoPipeline.Start(videos);
                audioPipeline.Start(audios);
            }
        }

        /// <summary>
        /// Finds a period that matches current playback timestamp. May return "early access"
        /// Period. Based on ISO IEC 23009-1 2015 section 5.3.2.1 & DASH IF IOP v 3.3
        /// </summary>
        /// <param name="Document">Document for which period is to be found </param>
        /// <param name="timeIndex"></param>
        /// <returns></returns>
        private Period FindPeriod(Document aDoc, TimeSpan timeIndex)
        {

            // Periods are already "sorted" from lowest to highest, thus find a period
            // where start time (time elapsed when this period shouold start)
            // to do so, search periods backwards, starting from those that should play last.
            for (int p = aDoc.Periods.Length - 1; p >= 0; p--)
            {
                var period = aDoc.Periods[p];
                var availstart = aDoc.AvailabilityStartTime ?? DateTime.UtcNow;

                var start = DateTime.UtcNow.Subtract(availstart);
                start = start.Add(period.Start ?? TimeSpan.Zero);

                var end = start.Add(period.Duration ?? TimeSpan.Zero);

                start = new TimeSpan(start.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond);
                end = new TimeSpan(end.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond);
                var ti = new TimeSpan(timeIndex.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond);

                Logger.Info($"Searching: {start}-{end} {period.ToString()} for TimeIndex/Current: {ti}");

                if (ti >= start && ti <= end)
                {
                    Logger.Info($"Matching period found: {period.ToString()} for TimeIndex: {ti}");
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
        /// <param name="newDoc">IDocument. Optional argument containing a document which will
        /// be used to retrieve isLive information</param>
        /// <returns>TimeSpan - Current Live Clock for dynamic mpd. It is expressed as ammount
        /// of time has passed since Document.AvailabilityStartTime
        /// For static content, time passed as argument is returned.</returns>
        private TimeSpan LiveClockTime(TimeSpan time, IDocument newDoc = null)
        {
            bool isDynamic;

            if (newDoc == null)
            {
                lock (locker)
                {
                    isDynamic = currentDocument.IsDynamic;
                }

                if (isDynamic == true)
                {
                    time = DateTime.UtcNow.Subtract(currentDocument.AvailabilityStartTime ?? DateTime.MinValue);
                }
            }
            else
            {
                var aDoc = newDoc as Document;
                isDynamic = aDoc.IsDynamic;

                if (isDynamic == true)
                {
                    time = DateTime.UtcNow.Subtract(aDoc.AvailabilityStartTime ?? DateTime.MinValue);
                }
            }



            return time;
        }
        /// <summary>
        /// Schedules a manifest reload for dynamic documents only.
        /// Reload Time is scheduled as UtcNow + minimum reload period.
        /// Uses current/previous timestamps from "OnTimeUpdated" event. to compute difference.
        /// If difference between 
        /// </summary>
        private void ScheduleManifestReload()
        {
            bool isDynamic;

            // Only update if pipeline is running
            if (audioPipeline == null && videoPipeline == null)
                return;

            lock (locker)
            {
                isDynamic = currentDocument.IsDynamic;
            }

            if (isDynamic)
            {
                // should we check playback time here or actual time?
                if ((DateTime.UtcNow - lastReloadTime) >= minimumReloadPeriod)
                {
                    manifest.ReloadManifest(DateTime.UtcNow);
                }
            }

        }

        public string CurrentCueText { get; }

        public void OnTimeUpdated(TimeSpan time)
        {
            currentTimeStamp = time;

            audioPipeline.OnTimeUpdated(time);
            videoPipeline.OnTimeUpdated(time);

            // Check if MPD needs reload.
            ScheduleManifestReload();
        }

        public void Dispose()
        {
            OnStopped();

            audioPipeline?.Dispose();
            audioPipeline = null;
            videoPipeline?.Dispose();
            videoPipeline = null;
        }
    }
}
