using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Xml;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Utils;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Drms.Cenc;
using MpdParser;
using System.Threading;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashMediaPipeline : IDisposable
    {
        private class DashStream : IEquatable<DashStream>
        {
            public DashStream(Media media, Representation representation)
            {
                Media = media;
                Representation = representation;
            }

            public Media Media { get; }
            public Representation Representation { get; }

            public override bool Equals(object obj)
            {
                return obj is DashStream stream && Equals(stream);
            }

            public bool Equals(DashStream other)
            {
                return other != null && (EqualityComparer<Media>.Default.Equals(Media, other.Media) &&
                                         EqualityComparer<Representation>.Default.Equals(Representation,
                                             other.Representation));
            }

            public override int GetHashCode()
            {
                var hashCode = 1768762187;
                hashCode = hashCode * -1521134295 + base.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<Media>.Default.GetHashCode(Media);
                hashCode = hashCode * -1521134295 +
                           EqualityComparer<Representation>.Default.GetHashCode(Representation);
                return hashCode;
            }
        }

        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event PacketReady PacketReady;
        public event StreamError StreamError;
        public event BufferingStarted BufferingStarted;
        public event BufferingCompleted BufferingCompleted;

        /// <summary>
        /// Holds smaller of the two (PTS/DTS) from the initial packet.
        /// </summary>
        private TimeSpan? trimmOffset;

        private readonly IDashClient dashClient;
        private readonly IDemuxer demuxer;
        private readonly IThroughputHistory throughputHistory;
        private readonly StreamType streamType;
        public StreamType StreamType => streamType;

        private bool pipelineStarted;
        private bool disableAdaptiveStreaming;

        private DashStream currentStream;
        private DashStream _pendingStreamStorage;

        private DashStream PendingStream
        {
            get => _pendingStreamStorage;
            set => _pendingStreamStorage = InitializePendingStream(value);
        }

        private readonly Object switchStreamLock = new Object();
        private List<DashStream> availableStreams = new List<DashStream>();

        private static readonly TimeSpan SegmentEps = TimeSpan.FromSeconds(0.5);
        private TimeSpan? lastSeek = TimeSpan.Zero;

        private PacketTimeStamp demuxerClock;
        private PacketTimeStamp lastPushedClock;

        public DashMediaPipeline(IDashClient dashClient, IDemuxer demuxer, IThroughputHistory throughputHistory,
            StreamType streamType)
        {
            this.dashClient = dashClient ??
                              throw new ArgumentNullException(nameof(dashClient), "dashClient cannot be null");
            this.demuxer = demuxer ?? throw new ArgumentNullException(nameof(dashClient), "demuxer cannot be null");
            this.throughputHistory = throughputHistory ??
                                     throw new ArgumentNullException(nameof(throughputHistory),
                                         "throughputHistory cannot be null");
            this.streamType = streamType;

            demuxer.DRMInitDataFound += OnDRMInitDataFound;
            demuxer.StreamConfigReady += OnStreamConfigReady;
            demuxer.PacketReady += OnPacketReady;
            demuxer.DemuxerError += OnStreamError;

            dashClient.Error += OnStreamError;
            dashClient.BufferingCompleted += OnBufferingCompleted;
            dashClient.BufferingStarted += OnBufferingStarted;
        }

        private DashStream InitializePendingStream(DashStream newStream)
        {
            if (newStream == null)
                return null;

            Logger.Info($"{StreamType}: Preparing stream for playback.");

            // TODO: Make PrepareStream async or async with delayed status notification
            //
            if (newStream.Representation.Segments.PrepeareStream())
                return newStream;

            Logger.Warn($"{StreamType}: Stream preparation failed! Content will not be played.");

            return null;
        }

        public Representation GetRepresentation()
        {
            return PendingStream?.Representation ?? currentStream?.Representation;
        }

        public void SynchronizeWith(DashMediaPipeline synchronizeWith)
        {
            var myRepresentation = GetRepresentation();
            var syncRepresentation = synchronizeWith.GetRepresentation();

            var myGood = myRepresentation != null;
            var syncGood = syncRepresentation != null;

            if (!myGood || !syncGood)
                throw new ArgumentNullException(
                    $"{StreamType}: Null or Failed Init. Representation. {myGood}/{syncGood}");

            myRepresentation.AlignStartSegmentsWith(syncRepresentation);

            Logger.Info(
                $"Segment Alignment: {streamType}={myRepresentation.AlignedStartSegmentID} {synchronizeWith.StreamType}={syncRepresentation.AlignedStartSegmentID} TrimmOffset={myRepresentation.AlignedTrimmOffset}");
        }

        public void UpdateMedia(Period period)
        {
            var media = period.GetMedia(ToMediaType(StreamType));
            if (!media.Any())
                throw new ArgumentOutOfRangeException($"{StreamType}: No media in period {period}");

            if (currentStream != null)
            {
                var currentMedia = media.Count == 1
                    ? media.First()
                    : media.FirstOrDefault(o => o.Id == currentStream.Media.Id);
                var currentRepresentation =
                    currentMedia?.Representations.FirstOrDefault(o => o.Id == currentStream.Representation.Id);
                if (currentRepresentation != null)
                {
                    GetAvailableStreams(media, currentMedia);

                    // Media Preparation (Call to Initialize) is done upon assignment to pendingStream.
                    PendingStream = new DashStream(currentMedia, currentRepresentation);
                    return;
                }
            }

            var defaultMedia = GetDefaultMedia(media);
            GetAvailableStreams(media, defaultMedia);
            // get first element of sorted array
            var representation = defaultMedia.Representations.OrderByDescending(o => o.Bandwidth).Last();

            PendingStream = new DashStream(defaultMedia, representation);
        }

        public void AdaptToNetConditions()
        {
            if (disableAdaptiveStreaming)
                return;

            if (currentStream == null && PendingStream == null)
                return;

            var streamToAdapt = PendingStream ?? currentStream;
            if (streamToAdapt.Representation.Bandwidth.HasValue == false)
                return;

            var currentThroughput = throughputHistory.GetAverageThroughput();
            if (Math.Abs(currentThroughput) < 0.1)
                return;

            Logger.Debug("Adaptation values:");
            Logger.Debug("  current throughput: " + currentThroughput);
            Logger.Debug("  current stream bandwidth: " + streamToAdapt.Representation.Bandwidth.Value);

            // availableStreams is sorted array by descending bandwidth
            var stream = availableStreams.FirstOrDefault(o =>
                             o.Representation.Bandwidth <= currentThroughput) ?? availableStreams.Last();

            if (stream.Representation.Bandwidth == streamToAdapt.Representation.Bandwidth) return;

            Logger.Info("Changing stream to bandwidth: " + stream.Representation.Bandwidth);
            PendingStream = stream;
        }

        public void SwitchStreamIfNeeded()
        {
            // Access serialization is needed.
            // SwitchStreamIfNeeded can be called from DashDataProvider or
            // timer based manifest reload (different threads) which can cause
            // null object reference if pendingStream is nulled AFTER another thread
            // passed pendingStream null check.
            //
            // Stream switching does not need to be serialized. If stream switch is already
            // in progress, next stream switch can safly be ignored, thus use of monitor
            // rather then lock.
            //

            if (!Monitor.TryEnter(switchStreamLock))
                return;

            try
            {
                if (PendingStream == null)
                    return;

                Logger.Info($"{StreamType}");

                if (currentStream == null)
                {
                    StartPipeline(PendingStream);
                    return;
                }

                if (!CanSwitchStream())
                    return;

                ResetPipeline();
                StartPipeline(PendingStream);

                PendingStream = null;
            }
            finally
            {
                Monitor.Exit(switchStreamLock);
            }
        }

        private void GetAvailableStreams(IEnumerable<Media> media, Media defaultMedia)
        {
            // Not perfect algorithm.
            // check if default media has many representations. if yes, return as available streams
            // list of default media representation + representations from any media from the same group
            // if no, return all available medias
            // TODO: add support for: SupplementalProperty schemeIdUri="urn:mpeg:dash:adaptation-set-switching:2016"
            if (defaultMedia.Representations.Length > 1)
            {
                if (defaultMedia.Group.HasValue)
                {
                    availableStreams = media.Where(o => o.Group == defaultMedia.Group)
                        .SelectMany(o => o.Representations, (parent, repr) => new DashStream(parent, repr))
                        .OrderByDescending(o => o.Representation.Bandwidth)
                        .ToList();
                }
                else
                {
                    availableStreams = defaultMedia.Representations.Select(o => new DashStream(defaultMedia, o))
                        .OrderByDescending(o => o.Representation.Bandwidth)
                        .ToList();
                }
            }
            else
            {
                availableStreams = media.Select(o => new DashStream(o, o.Representations.First()))
                    .OrderByDescending(o => o.Representation.Bandwidth)
                    .ToList();
            }
        }

        private void StartPipeline(DashStream newStream = null)
        {
            // There may be mutliple calls here.
            // AdaptiveStreaming, OnStreamChange and Resume.
            // We must assure there are no multiple calls to Start Client. Blocking at client
            // won't do the trick.
            //
            lock (dashClient)
            {
                if (pipelineStarted)
                    return;

                if (newStream != null)
                {
                    currentStream = newStream;

                    Logger.Info($"{StreamType}: Dash pipeline start.");
                    Logger.Info($"{StreamType}: Media: {currentStream.Media}");
                    Logger.Info($"{StreamType}: {currentStream.Representation}");

                    dashClient.SetRepresentation(currentStream.Representation);
                    ParseDrms(currentStream.Media);
                }

                if (!trimmOffset.HasValue)
                    trimmOffset = currentStream.Representation.AlignedTrimmOffset;

                var fullInitRequired = (newStream != null);

                demuxer.StartForExternalSource(fullInitRequired ? InitializationMode.Full : InitializationMode.Minimal);
                dashClient.Start(fullInitRequired);

                pipelineStarted = true;
            }
        }

        private static Media GetDefaultMedia(ICollection<Media> medias)
        {
            Media media = null;
            if (medias.Count == 1)
                media = medias.First();
            if (media == null)
                media = medias.FirstOrDefault(o => o.HasRole(MediaRole.Main));
            if (media == null)
                media = medias.FirstOrDefault(o => o.Lang == "en");

            return media ?? medias.FirstOrDefault();
        }

        private static MediaType ToMediaType(StreamType streamType)
        {
            switch (streamType)
            {
                case StreamType.Audio:
                    return MediaType.Audio;
                case StreamType.Video:
                    return MediaType.Video;
                case StreamType.Subtitle:
                    return MediaType.Text;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Resume()
        {
            StartPipeline();
        }

        public void Pause()
        {
            ResetPipeline();
        }

        public void Stop()
        {
            if (!pipelineStarted)
                return;

            demuxer.Stop();
            dashClient.Stop();

            trimmOffset = null;

            pipelineStarted = false;
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            dashClient.OnTimeUpdated(time);
        }

        public void Seek(TimeSpan time, uint seekId)
        {
            lastSeek = dashClient.Seek(time);
            PacketReady?.Invoke(SeekPacket.CreatePacket(StreamType, seekId));
        }

        public void ChangeStream(StreamDescription stream)
        {
            Logger.Info("");

            if (stream == null)
                throw new ArgumentNullException(nameof(stream), "stream cannot be null");

            if (availableStreams.Count <= stream.Id)
                throw new ArgumentOutOfRangeException();

            var newMedia = availableStreams[stream.Id].Media;
            var newRepresentation = availableStreams[stream.Id].Representation;

            // Share lock with switchStreamIfNeeded. Change stream may happen on a separate thread.
            // As such, we do not want 2 starts happening
            // - One as a manual stream selection
            // - One as a adaptive stream switching.
            //
            Monitor.Enter(switchStreamLock);
            try
            {
                // Stream needs manual preparation as it is NOT passed through pendingStream
                //
                var newStream = InitializePendingStream(new DashStream(newMedia, newRepresentation));

                if (newStream == null)
                {
                    Logger.Warn(
                        $"{StreamType}: Failed to prepare stream. New stream IS NOT set. Continuing playing previous");
                    return;
                }

                if (currentStream.Media.Type.Value != newMedia.Type.Value)
                    throw new ArgumentException("wrong media type");

                disableAdaptiveStreaming = true;

                ResetPipeline();
                StartPipeline(newStream);
            }
            finally
            {
                Monitor.Exit(switchStreamLock);
            }
        }

        private void ResetPipeline()
        {
            if (!pipelineStarted)
                return;

            // Stop demuxer and dashclient
            // Stop demuxer first so old incoming data will ignored
            demuxer.Stop();
            dashClient.Reset();

            pipelineStarted = false;
        }

        public List<StreamDescription> GetStreamsDescription()
        {
            return availableStreams.Select((o, i) =>
                new StreamDescription
                {
                    Id = i,
                    Description = CreateStreamDescription(o),
                    StreamType = StreamType,
                    Default = currentStream.Equals(o)
                }).ToList();
        }

        private static string CreateStreamDescription(DashStream stream)
        {
            var description = "";
            if (!string.IsNullOrEmpty(stream.Media.Lang))
                description += stream.Media.Lang;
            if (stream.Representation.Height.HasValue && stream.Representation.Width.HasValue)
                description += $" ( {stream.Representation.Width}x{stream.Representation.Height} )";
            if (stream.Representation.NumChannels.HasValue)
                description += $" ( {stream.Representation.NumChannels} ch )";

            return description;
        }

        private void ParseDrms(Media newMedia)
        {
            // TODO(p.galiszewsk): make it extensible
            foreach (var descriptor in newMedia.ContentProtections)
            {
                var schemeIdUri = descriptor.SchemeIdUri;
                if (CencUtils.SupportsSchemeIdUri(schemeIdUri))
                    ParseCencScheme(descriptor, schemeIdUri);
                else if (string.Equals(schemeIdUri, "http://youtube.com/drm/2012/10/10",
                    StringComparison.CurrentCultureIgnoreCase))
                    ParseYoutubeScheme(descriptor);
            }
        }

        private void ParseCencScheme(ContentProtection descriptor, string schemeIdUri)
        {
            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(descriptor.Data);
            }
            catch (Exception)
            {
                return;
            }

            // read first node inner text (should be psshbox or pro header)
            var initData = doc.FirstChild?.FirstChild?.InnerText;

            var drmInitData = new DRMInitData
            {
                InitData = Convert.FromBase64String(initData),
                SystemId = CencUtils.SchemeIdUriToSystemId(schemeIdUri),
                // Stream Type will be appended during OnDRMInitDataFound()
            };

            OnDRMInitDataFound(drmInitData);
        }

        private void ParseYoutubeScheme(ContentProtection descriptor)
        {
            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(descriptor.Data);
            }
            catch (Exception)
            {
                return;
            }

            if (doc.FirstChild?.ChildNodes == null)
                return;

            foreach (XmlNode node in doc.FirstChild?.ChildNodes)
            {
                var type = node.Attributes?.GetNamedItem("type")?.Value;
                if (!CencUtils.SupportsType(type))
                    continue;

                var drmDescriptor = new DRMDescription
                {
                    LicenceUrl = node.InnerText,
                    Scheme = type
                };
                SetDrmConfiguration?.Invoke(drmDescriptor);
            }
        }

        private void OnDRMInitDataFound(DRMInitData drmData)
        {
            drmData.StreamType = StreamType;
            DRMInitDataFound?.Invoke(drmData);
        }

        private void OnPacketReady(Packet packet)
        {
            if (packet != null)
            {
                AdjustDemuxerTimeStampIfNeeded(packet);

                // Sometimes we can receive invalid timestamp from demuxer
                // eg during encrypted content seek or live video.
                // Adjust timestamps to avoid playback problems
                packet += demuxerClock;
                packet -= trimmOffset.Value;

                if (packet.Pts < TimeSpan.Zero || packet.Dts < TimeSpan.Zero)
                {
                    packet.Pts = TimeSpan.Zero;
                    packet.Dts = TimeSpan.Zero;
                }

                // Don't convert packet here, use assignment (less costly)
                lastPushedClock.SetClock(packet);
                PacketReady?.Invoke(packet);
                return;
            }

            PacketReady?.Invoke(Packet.CreateEOS(StreamType));
        }

        private void OnStreamError(string errorMessage)
        {
            // Transfer event to Data Provider
            StreamError?.Invoke(errorMessage);
        }

        private void OnBufferingStarted()
        {
            BufferingStarted?.Invoke();
        }

        private void OnBufferingCompleted()
        {
            BufferingCompleted?.Invoke();
        }

        private void AdjustDemuxerTimeStampIfNeeded(Packet packet)
        {
            if (!lastSeek.HasValue)
            {
                if (packet.IsZeroClock())
                {
                    // This IS NOT ideal solution to work around reset of PTS/DTS after
                    demuxerClock = lastPushedClock;
                    trimmOffset = TimeSpan.Zero;
                    Logger.Info(
                        $"{StreamType}: Zero timestamped packet. Adjusting demuxerClock: {demuxerClock} trimmOffset: {trimmOffset.Value}");
                }
            }
            else
            {
                if (packet.Pts + SegmentEps < lastSeek)
                {
                    // Add last seek value to packet clock. Forcing last seek value looses
                    // PTS/DTS differences causing lip sync issues.
                    //
                    demuxerClock = (PacketTimeStamp) packet + lastSeek.Value;

                    Logger.Info($"{StreamType}: Badly timestamped packet. Adjusting demuxerClock to: {demuxerClock}");
                }

                lastSeek = null;
            }
        }

        private void OnStreamConfigReady(StreamConfig config)
        {
            StreamConfigReady?.Invoke(config);
        }

        public bool CanSwitchStream()
        {
            // Allow adaptive stream switching if Client is in correct state and
            // Adaptive Streaming enabled.
            //
            return dashClient.CanStreamSwitch() && !disableAdaptiveStreaming;
        }

        public void Dispose()
        {
            demuxer.Dispose();
            dashClient.Dispose();
        }
    }
}
