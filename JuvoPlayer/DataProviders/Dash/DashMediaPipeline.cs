using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using JuvoLogger;
using JuvoPlayer.Common;
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
                return EqualityComparer<Media>.Default.Equals(Media, other.Media) &&
                       EqualityComparer<Representation>.Default.Equals(Representation, other.Representation);
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
        /// Storage holders for initial packets PTS/DTS values.
        /// Used in Trimming Packet Handler to truncate down PTS/DTS values.
        /// First packet seen acts as flip switch. Fill initial values or not.
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
        private TimeSpan laskSeek = TimeSpan.Zero;
        private TimeSpan demuxerTimeStamp = TimeSpan.Zero;

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

        public void UpdateMedia(IList<Media> media)
        {
            if (media == null)
                throw new ArgumentNullException(nameof(media), "media cannot be null");

            if (media.Any(o => o.Type.Value != ToMediaType(streamType)))
                throw new ArgumentException("Not compatible media found");

            if (pipelineStarted && currentStream != null)
            {
                var currentMedia = media.Count == 1 ? media.First() : media.FirstOrDefault(o => o.Id == currentStream.Media.Id);
                var currentRepresentation = currentMedia?.Representations.FirstOrDefault(o => o.Id == currentStream.Representation.Id);
                if (currentRepresentation != null)
                {
                    GetAvailableStreams(media, currentMedia);
                    pendingStream = new DashStream(currentMedia, currentRepresentation);
                    return;
                }
            }

            var defaultMedia = GetDefaultMedia(media);
            GetAvailableStreams(media, defaultMedia);
            // get first element of sorted array 
            var representation = defaultMedia.Representations.OrderByDescending(o => o.Bandwidth).First();
            pendingStream = new DashStream(defaultMedia, representation);
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
                StopPipeline();
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
            if (newStream != null)
            {
                currentStream = newStream;

                Logger.Info($"{streamType}: Dash pipeline start.");
                Logger.Info($"{streamType}: Media: {currentStream.Media}");
                Logger.Info($"{streamType}: {currentStream.Representation}");

                dashClient.SetRepresentation(currentStream.Representation);
                ParseDrms(currentStream.Media);
            }

            demuxer.StartForExternalSource(newStream != null ? InitializationMode.Full : InitializationMode.Minimal);
            dashClient.Start();
            pipelineStarted = true;
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
            StopPipeline();
            pipelineStarted = false;
        }
        public void Stop()
        {
            StopPipeline();

            trimmOffset = null;
            pipelineStarted = false;
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            dashClient.OnTimeUpdated(time);
        }

        public void Seek(TimeSpan time)
        {
            laskSeek = dashClient.Seek(time);
        }

        public void ChangeStream(StreamDescription stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream), "stream cannot be null");

            if (availableStreams.Count <= stream.Id)
                throw new ArgumentOutOfRangeException();

            var newMedia = availableStreams[stream.Id].Media;
            var newRepresentation = availableStreams[stream.Id].Representation;
            var newStream = new DashStream(newMedia, newRepresentation);

            if (currentStream.Media.Type.Value != newMedia.Type.Value)
                throw new ArgumentException("wrong media type");

            disableAdaptiveStreaming = true;

            StopPipeline();
            StartPipeline(newStream);
        }

        private void StopPipeline()
        {
            // Stop demuxer and dashclient
            // Stop demuxer first so old incoming data will ignored
            demuxer.Stop();
            dashClient.Stop();
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
                StreamType = streamType
            };
            DRMInitDataFound?.Invoke(drmInitData);
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
                packet.Dts += demuxerTimeStamp - trimmOffset.Value;
                packet.Pts += demuxerTimeStamp - trimmOffset.Value;

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
            //Get very first PTS/DTS
            if (trimmOffset.HasValue == false)
                trimmOffset = TimeSpan.FromTicks(Math.Min(packet.Pts.Ticks, packet.Dts.Ticks));

            if (packet.Pts + SegmentEps < laskSeek)
            {
                Logger.Debug("Got badly timestamped packet. Adding timestamp to packets");
                demuxerTimeStamp = laskSeek;
            }

            laskSeek = TimeSpan.Zero;
        }

        private void OnStreamConfigReady(StreamConfig config)
        {
            StreamConfigReady?.Invoke(config);
        }

        public void Dispose()
        {
            demuxer.Dispose();
            dashClient.Dispose();
        }
    }
}
