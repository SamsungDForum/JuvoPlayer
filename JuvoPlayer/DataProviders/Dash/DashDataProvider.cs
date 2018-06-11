using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Subtitles;
using MpdParser;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashDataProvider : IDataProvider
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private readonly DashManifest manifest;
        private Document currentDocument;
        private Period currentPeriod;
        private DashMediaPipeline audioPipeline;
        private DashMediaPipeline videoPipeline;
        private readonly List<SubtitleInfo> subtitleInfos = new List<SubtitleInfo>();
        private CuesMap cuesMap;
        private TimeSpan currentTime = TimeSpan.Zero;

        public event ClipDurationChanged ClipDurationChanged;
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event PacketReady PacketReady;
        public event StreamError StreamError;

        private ManualResetEventSlim waitForManifest = new ManualResetEventSlim(false);

        private bool disposed;
        private bool errorProcessed;

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

        public async void OnPlayed()
        {
            // try to update manifest. Needed in case of live content
            await UpdateManifest();
        }

        public void OnSeek(TimeSpan time)
        {
            if (!IsSeekingSupported())
                return;

            audioPipeline.Pause();
            videoPipeline.Pause();

            audioPipeline.Seek(time);
            videoPipeline.Seek(time);

            audioPipeline.Resume();
            videoPipeline.Resume();
        }

        public void OnStopped()
        {
            audioPipeline.Stop();
            videoPipeline.Stop();
        }

        public bool IsSeekingSupported()
        {
            // NOTE: Are we sure seeking is illegal on Dynamic MPDs?
            // Imho no. There is a "time window" in which content is available
            // but leave that for now...
            return (currentDocument.IsDynamic == false);

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

            await UpdateManifest();
        }

        private void StartInternal()
        {
            if (disposed)
                return;

            if (currentDocument == null || currentPeriod == null)
            {
                Logger.Info("Dash start delayed. No Manifest/Period available.");
                return;
            }

            // TODO:? In case of updates - should we check if A/V is any different from
            // anything we passed down before? Should offload client...
            var manifestParams = new ManifestParameters(currentDocument, currentPeriod);
            manifestParams.PlayClock = LiveClockTime(currentTime);

            Logger.Info(currentPeriod.ToString());

            var audios = currentPeriod.Sets.Where(o => o.Type.Value == MediaType.Audio).ToList();
            var videos = currentPeriod.Sets.Where(o => o.Type.Value == MediaType.Video).ToList();

            if (audios.Any() && videos.Any())
            {
                BuildSubtitleInfos(currentPeriod);

                if (currentPeriod.Duration.HasValue)
                    ClipDurationChanged?.Invoke(currentPeriod.Duration.Value);

                foreach (var v in videos)
                {
                    v.SetDocumentParameters(manifestParams);
                }

                foreach (var a in audios)
                {
                    a.SetDocumentParameters(manifestParams);
                }

                videoPipeline.Start(videos);
                audioPipeline.Start(audios);
            }
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
        /// <param name="newDoc">IDocument. Optional argument containing a document which will
        /// be used to retrieve isLive information</param>
        /// <returns>TimeSpan - Current Live Clock for dynamic mpd. It is expressed as ammount
        /// of time has passed since Document.AvailabilityStartTime
        /// For static content, time passed as argument is returned.</returns>
        private TimeSpan LiveClockTime(TimeSpan time, Document newDoc = null)
        {
            var document = newDoc ?? currentDocument;

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

            audioPipeline.OnTimeUpdated(time);
            videoPipeline.OnTimeUpdated(time);

            await UpdateManifest();
        }

        public async Task UpdateManifest()
        {
            if (manifest.NeedsReload())
            {
                Logger.Info("Updating manifest");

                await manifest.ReloadManifestTask();

                var tmpDocument = manifest.CurrentDocument;
                if (tmpDocument == null)
                {
                    Logger.Info("No Manifest.");
                    return;
                }

                var tmpPeriod = FindPeriod(tmpDocument, LiveClockTime(currentTime, tmpDocument));
                if (tmpPeriod == null)
                {
                    Logger.Info("No period in Manifest.");
                    return;
                }

                currentDocument = manifest.CurrentDocument;
                currentPeriod = tmpPeriod;

                StartInternal();
            }
        }

        private void OnStreamError(string errorMessage)
        {
            // Process error only once - there is no point in gobbling up
            // CPU timne processing multiple requests as the very first will cause pipeline
            // termination for Audio & Video
            if (errorProcessed)
                return;

            errorProcessed = true;

            // TODO: Review parallelization. Logging, A & V Stop, Stream Erro Invokation
            // can be safely done in parallel.
            Logger.Error($"Stream Error: {errorMessage}. Terminating pipelines.");

            // This will generate "already stopped" message from failed pipeline.
            // It is possible to forgo calling stop here, simply raise StreamError event
            // and wait for termination as part of player window closure. Imho, better to call
            // quits as early as possible.
            audioPipeline.Stop();
            videoPipeline.Stop();

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

            audioPipeline?.Dispose();
            audioPipeline = null;
            videoPipeline?.Dispose();
            videoPipeline = null;
        }
    }
}

