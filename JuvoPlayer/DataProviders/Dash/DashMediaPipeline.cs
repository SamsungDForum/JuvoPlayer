using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Utils;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Drms.Cenc;
using MpdParser;

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

            public bool IsCompatibleWith(DashStream other)
            {
                return Representation.Codecs == other.Representation.Codecs
                       && Representation.Height == other.Representation.Height
                       && Representation.Width == other.Representation.Width;
            }

            public override bool Equals(object obj)
            {
                return obj is DashStream stream && Equals(stream);
            }

            public bool Equals(DashStream other)
            {
                return other != null && (EqualityComparer<Media>.Default.Equals(Media, other.Media) &&
                                         EqualityComparer<Representation>.Default.Equals(Representation, other.Representation));
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

        /// <summary>
        /// Holds smaller of the two (PTS/DTS) from the initial packet.
        /// </summary>
        private TimeSpan? trimmOffset;

        private readonly IDashClient dashClient;
        private readonly IDemuxer demuxer;
        private readonly IThroughputHistory throughputHistory;
        private readonly StreamType streamType;

        private bool pipelineStarted;
        private bool disableAdaptiveStreaming;

        private DashStream currentStream;
        private DashStream pendingStream;
        private List<DashStream> availableStreams = new List<DashStream>();

        private static readonly TimeSpan SegmentEps = TimeSpan.FromSeconds(0.5);
        private TimeSpan? lastSeek = TimeSpan.Zero;

        private PacketTimeStamp demuxerClock;
        private PacketTimeStamp lastPushedClock;

        public DashMediaPipeline(IDashClient dashClient, IDemuxer demuxer, IThroughputHistory throughputHistory, StreamType streamType)
        {
            this.dashClient = dashClient ?? throw new ArgumentNullException(nameof(dashClient), "dashClient cannot be null");
            this.demuxer = demuxer ?? throw new ArgumentNullException(nameof(dashClient), "demuxer cannot be null");
            this.throughputHistory = throughputHistory ?? throw new ArgumentNullException(nameof(throughputHistory), "throughputHistory cannot be null");
            this.streamType = streamType;

            demuxer.DRMInitDataFound += OnDRMInitDataFound;
            demuxer.StreamConfigReady += OnStreamConfigReady;
            demuxer.PacketReady += OnPacketReady;
            demuxer.DemuxerError += OnStreamError;

            dashClient.Error += OnStreamError;
        }

        public Representation GetRepresentation()
        {
            return pendingStream?.Representation ?? currentStream?.Representation;
        }

        public bool UpdateMedia(IList<Media> media)
        {
            if (media == null)
                throw new ArgumentNullException(nameof(media), "media cannot be null");

            if (media.Any(o => o.Type.Value != ToMediaType(streamType)))
                throw new ArgumentException("Not compatible media found");

            DashStream tmpStream = null;
            bool res = false;

            if (currentStream != null)
            {
                var currentMedia = media.Count == 1 ? media.First() : media.FirstOrDefault(o => o.Id == currentStream.Media.Id);
                var currentRepresentation = currentMedia?.Representations.FirstOrDefault(o => o.Id == currentStream.Representation.Id);
                if (currentRepresentation != null)
                {
                    GetAvailableStreams(media, currentMedia);

                    // Prepeare set media before assigning it to "pending stram" to assure
                    // ready to use stream is set. Otherwise adopt to net condition may snach it and send it to DashClient
                    tmpStream = new DashStream(currentMedia, currentRepresentation);
                    res = tmpStream.Representation.Segments.PrepeareStream();
                    pendingStream = tmpStream;
                    return res;
                }
            }

            var defaultMedia = GetDefaultMedia(media);
            GetAvailableStreams(media, defaultMedia);
            // get first element of sorted array 
            var representation = defaultMedia.Representations.OrderByDescending(o => o.Bandwidth).First();

            // Prepeare set media before assigning it to "pending stram" to assure
            // ready to use stream is set. Otherwise adopt to net condition may snach it and send it to DashClient
            tmpStream = new DashStream(defaultMedia, representation);
            res = tmpStream.Representation.Segments.PrepeareStream();
            pendingStream = tmpStream;

            return res;
        }

        public void AdaptToNetConditions()
        {
            if (disableAdaptiveStreaming)
                return;

            if (currentStream == null && pendingStream == null)
                return;

            var streamToAdapt = pendingStream ?? currentStream;
            if (streamToAdapt.Representation.Bandwidth.HasValue == false)
                return;

            var currentThroughput = throughputHistory.GetAverageThroughput();
            if (Math.Abs(currentThroughput) < 0.1)
                return;

            Logger.Debug("Adaptation values:");
            Logger.Debug("  current throughput: " + currentThroughput);
            Logger.Debug("  current stream bandwith: " + streamToAdapt.Representation.Bandwidth.Value);

            // availableStreams is sorted array by descending bandwith
            var stream = availableStreams.FirstOrDefault(o => o.Representation.Bandwidth <= currentThroughput);
            if (stream != null && stream.Representation.Bandwidth != streamToAdapt.Representation.Bandwidth)
            {
                Logger.Info("Changing stream do bandwith: " + stream.Representation.Bandwidth);
                pendingStream = stream;
            }
        }

        public void SwitchStreamIfNeeded()
        {
            if (pendingStream == null)
                return;

            Logger.Info($"{streamType}");

            if (currentStream == null)
            {
                StartPipeline(pendingStream);
            }
            else if (currentStream.IsCompatibleWith(pendingStream))
            {
                UpdatePipeline(pendingStream);
            }
            else
            {
                ResetPipeline();
                StartPipeline(pendingStream);
            }

            pendingStream = null;
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

                pipelineStarted = true;
            }

            if (newStream != null)
            {
                currentStream = newStream;

                Logger.Info($"{streamType}: Dash pipeline start.");
                Logger.Info($"{streamType}: Media: {currentStream.Media}");
                Logger.Info($"{streamType}: {currentStream.Representation}");

                dashClient.SetRepresentation(currentStream.Representation);
                ParseDrms(currentStream.Media);
            }

            if (!trimmOffset.HasValue)
                trimmOffset = currentStream.Representation.AlignedTrimmOffset;

            demuxer.StartForExternalSource(newStream != null ? InitializationMode.Full : InitializationMode.Minimal);
            dashClient.Start();
        }

        /// <summary>
        /// Updates pipeline with new media based on Manifest Update.
        /// </summary>
        /// <param name="newStream">new Dash Stream object containing new media</param>
        private void UpdatePipeline(DashStream newStream)
        {
            currentStream = newStream;

            Logger.Info($"{streamType}: Manifest update. {newStream.Media} {newStream.Representation}");

            dashClient.UpdateRepresentation(newStream.Representation);
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

        public void Seek(TimeSpan time)
        {
            lastSeek = dashClient.Seek(time);
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
            var newStream = new DashStream(newMedia, newRepresentation);

            if (currentStream.Media.Type.Value != newMedia.Type.Value)
                throw new ArgumentException("wrong media type");

            if (!newStream.Representation.Segments.PrepeareStream())
            {
                Logger.Warn($"{streamType}: Failed to prepare stream. New stream IS NOT set. Continuing playing previous");
                return;
            }

            disableAdaptiveStreaming = true;

            ResetPipeline();
            StartPipeline(newStream);
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
                    StreamType = streamType,
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
                else if (string.Equals(schemeIdUri, "http://youtube.com/drm/2012/10/10", StringComparison.CurrentCultureIgnoreCase))
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
            drmData.StreamType = streamType;
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

            PacketReady?.Invoke(Packet.CreateEOS(streamType));
        }

        private void OnStreamError(string errorMessage)
        {
            // Transfer event to Data Provider
            StreamError?.Invoke(errorMessage);
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
                    Logger.Info($"{streamType}: Zero timestamped packet. Adjusting demuxerClock: {demuxerClock} trimmOffset: {trimmOffset.Value}");
                }
            }
            else
            {
                if (packet.Pts + SegmentEps < lastSeek)
                {
                    // Add last seek value to packet clock. Forcing last seek value looses
                    // PTS/DTS differences causing lip sync issues.
                    //
                    demuxerClock = (PacketTimeStamp)packet + lastSeek.Value;

                    Logger.Info($"{streamType}: Badly timestamped packet. Adjusting demuxerClock to: {demuxerClock}");
                }

                lastSeek = null;
            }
        }

        private void OnStreamConfigReady(StreamConfig config)
        {
            StreamConfigReady?.Invoke(config);
        }

        public bool CanStreamSwitch()
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
