using System;
using System.Linq;
using System.Xml;
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Drms.Cenc;
using MpdParser;
using System.Collections.Generic;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashMediaPipeline : IDisposable
    {
        private struct DashStream : IEquatable<DashStream>
        {
            public DashStream(Media media, Representation representation)
            {
                Media = media;
                Representation = representation;
            }
            public Media Media { get; private set; }
            public Representation Representation { get; private set; }

            public override bool Equals(object obj)
            {
                return obj is DashStream && Equals((DashStream)obj);
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
                hashCode = hashCode * -1521134295 + EqualityComparer<Representation>.Default.GetHashCode(Representation);
                return hashCode;
            }
        }

        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event PacketReady PacketReady;

        /// <summary>
        /// Stores information on which handler is being currently used, so it can be easly removed
        /// from demuxer.
        /// </summary>
        private event PacketReady PacketReadyHandlerUsed = null;

        /// <summary>
        /// Storage holders for initial packets PTS/DTS values.
        /// Used in Trimming Packet Handler to truncate down PTS/DTS values.
        /// First packet seen acts as flip switch. Fill initial values or not.
        /// </summary>
        private bool haveFirstPTSDTS = false;
        private TimeSpan firstPTS = TimeSpan.Zero;
        private TimeSpan firstDTS = TimeSpan.Zero;

        private readonly IDashClient dashClient;
        private readonly IDemuxer demuxer;
        private readonly StreamType streamType;

        private bool demuxerFullyInitialized = false;

        private DashStream currentStream;
        private List<DashStream> availableStreams = new List<DashStream>();

        private static readonly TimeSpan SegmentEps = TimeSpan.FromSeconds(0.5);
        private TimeSpan laskSeek = TimeSpan.Zero;
        private TimeSpan demuxerTimeStamp = TimeSpan.Zero;

        public DashMediaPipeline(IDashClient dashClient, IDemuxer demuxer, StreamType streamType)
        {
            this.dashClient = dashClient ?? throw new ArgumentNullException(nameof(dashClient), "dashClient cannot be null");
            this.demuxer = demuxer ?? throw new ArgumentNullException(nameof(dashClient), "demuxer cannot be null");
            this.streamType = streamType;

            demuxer.DRMInitDataFound += OnDRMInitDataFound;
            demuxer.StreamConfigReady += OnStreamConfigReady;
        }

        

        public void Start(IEnumerable<Media> media)
        {
            if (media == null)
                throw new ArgumentNullException(nameof(media), "media cannot be null");

            if (media.Any(o => o.Type.Value != ToMediaType(streamType)))
                throw new ArgumentException("Not compatible media found");

            var defaultMedia = GetDefaultMedia(media);
            // get first element of sorted array 
            var representation = defaultMedia.Representations.OrderByDescending(o => o.Bandwidth).First();
            var defaultStream = new DashStream(defaultMedia, representation);

            if (demuxerFullyInitialized)
            {
                UpdatePipeline(defaultStream);
            }
            else
            {
                StartPipeline(defaultStream);
            }

            GetAvailableStreams(media, defaultMedia);
        }

        private void GetAvailableStreams(IEnumerable<Media> media, Media defaultMedia)
        {
            // Not perfect algorithm.
            // check if default media has many representations. if yes, return as available streams
            // list of default media representation + representations from any media from the same group
            // if no, return all available medias
            // TODO: add support for: SupplementalProperty schemeIdUri="urn:mpeg:dash:adaptation-set-switching:2016" 
            //            if (media.Any(o => o.Representations.Count() > 1))
            if (defaultMedia.Representations.Count() > 1)
            {
                if (defaultMedia.Group.HasValue)
                {
                    availableStreams = media.Where(o => o.Group == defaultMedia.Group)
                                            .SelectMany(o => o.Representations, (parent, repr) => new DashStream(parent, repr)).ToList();
                }
                else
                {
                    availableStreams = defaultMedia.Representations.Select(o => new DashStream(defaultMedia, o)).ToList();

                }
            }
            else
            {
                availableStreams = media.Select(o => new DashStream(o, o.Representations.First())).ToList();
            }
        }

        private void StartPipeline(DashStream newStream)
        {
            currentStream = newStream;

            Logger.Info($"{streamType}: Dash pipeline start.");
            Logger.Info($"{streamType}: Media: {newStream.Media}");
            Logger.Info($"{streamType}: {newStream.Representation.ToString()}");

            dashClient.SetRepresentation(newStream.Representation);
            ParseDrms(newStream.Media);

            // Select packet handler. Live content usually
            // has very large PTS/DTS values which need trunction.
            //
            // TODO: @availabilityStartTime + Period Start Time + PeriodDuration
            // could be check for overflows (being longer then 32 bit)
            // and trimming handler could be applied only to such cases
            //
            if(newStream.Media.Parameters.Document.IsDynamic == true)
            {
                // Live content uses "trimming" packet handler
                // which takes first PTS/DTS and subtracts it from all others, effectivety
                // counting PTS/DTS from zero.
                PacketReadyHandlerUsed = OnPacketReadyTrimPtsDts;
            }
            else
            {
                PacketReadyHandlerUsed = OnPacketReady;
            }
            demuxer.PacketReady += PacketReadyHandlerUsed;

            dashClient.Start();
            demuxer.StartForExternalSource(InitializationMode.Full);
            demuxerFullyInitialized = true;
        }

        /// <summary>
        /// Updates pipeline with new media based on Manifest Update.
        /// </summary>
        /// <param name="newStream">new Dash Stream object containing new media</param>
        private void UpdatePipeline(DashStream newStream)
        {
            currentStream = newStream;

            Logger.Info($"{streamType}: Dash pipeline update.");
            Logger.Info($"{streamType}: Media: {newStream.Media}");
            Logger.Info($"{streamType}: {newStream.Representation.ToString()}");

            dashClient.UpdateRepresentation(newStream.Representation);

        }

        private static Media GetDefaultMedia(IEnumerable<Media> medias)
        {
            Media media = null;
            if (medias.Count() == 1)
                media = medias.First();
            if (media == null)
                media = medias.FirstOrDefault(o => o.HasRole(MediaRole.Main));
            if (media == null)
                media = medias.FirstOrDefault(o => o.Lang == "en");
            if (media == null)
                media = medias.FirstOrDefault();

            return media;
        }

        private MediaType ToMediaType(StreamType streamType)
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

        public void Stop()
        {
            dashClient.Stop();

            demuxer.PacketReady -= PacketReadyHandlerUsed;
            haveFirstPTSDTS = false;

            demuxer.Dispose();
            demuxerFullyInitialized = false;
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            dashClient.OnTimeUpdated(time);
        }

        public void Seek(TimeSpan time)
        {
            // Stop demuxer and dashclient
            // Stop demuxer first so old incoming data will ignored
            demuxer.Reset();
            dashClient.Stop();

            // Set new time
            laskSeek = dashClient.Seek(time);

            // Start downloading and parsing new data
            dashClient.Start();
            demuxer.StartForExternalSource(InitializationMode.Minimal);
        }

        public void ChangeStream(StreamDescription stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream), "stream cannot be null");

            if (availableStreams.Count() <= stream.Id)
                throw new ArgumentOutOfRangeException();

            var newMedia = availableStreams[stream.Id].Media;
            var newRepresentation = availableStreams[stream.Id].Representation;
            var newStream = new DashStream(newMedia, newRepresentation);

            if (currentStream.Media.Type.Value != newMedia.Type.Value)
                throw new ArgumentException("wrong media type");

            // Stop demuxer and dashclient
            // Stop demuxer first so old incoming data will ignored
            demuxer.Reset();
            dashClient.Stop();

            StartPipeline(newStream);
        }

        public List<StreamDescription> GetStreamsDescription()
        {
            return availableStreams.Select((o, i) =>
                new StreamDescription()
                {
                    Id = i,
                    Description = CreateStreamDescription(o),
                    StreamType = streamType,
                    Default = currentStream.Equals(o)
                }).ToList();
        }

        private string CreateStreamDescription(DashStream stream)
        {
            string description = "";
            if (!string.IsNullOrEmpty(stream.Media.Lang))
                description += stream.Media.Lang;
            if (stream.Representation.Height.HasValue && stream.Representation.Width.HasValue)
                description += string.Format(" ( {0}x{1} )", stream.Representation.Width, stream.Representation.Height);
            if (stream.Representation.NumChannels.HasValue)
                description += string.Format(" ( {0} ch )", stream.Representation.NumChannels);

            return description;
        }

        private void ParseDrms(Media newMedia)
        {
            // TODO(p.galiszewsk): make it extensible
            foreach (var descriptor in newMedia.ContentProtections)
            {
                var schemeIdUri = descriptor.SchemeIdUri;
                if (CencUtils.SupportsSchemeIdUri(schemeIdUri))
                {
                    XmlDocument doc = new XmlDocument();
                    try
                    {
                        doc.LoadXml(descriptor.Data);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    // read first node inner text (should be psshbox or pro header)
                    var initData = doc.FirstChild?.FirstChild?.InnerText;

                    var drmInitData = new DRMInitData()
                    {
                        InitData = Convert.FromBase64String(initData),
                        SystemId = CencUtils.SchemeIdUriToSystemId(schemeIdUri),
                        StreamType = streamType
                    };
                    DRMInitDataFound?.Invoke(drmInitData);
                }
                else if (string.Equals(schemeIdUri, "http://youtube.com/drm/2012/10/10", StringComparison.CurrentCultureIgnoreCase))
                {
                    XmlDocument doc = new XmlDocument();
                    try
                    {
                        doc.LoadXml(descriptor.Data);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (doc.FirstChild?.ChildNodes == null)
                        continue;

                    foreach (XmlNode node in doc.FirstChild?.ChildNodes)
                    {
                        var type = node.Attributes?.GetNamedItem("type")?.Value;
                        if (CencUtils.SupportsType(type))
                        {
                            var drmDescriptor = new DRMDescription()
                            {
                                LicenceUrl = node.InnerText,
                                Scheme = type
                            };
                            SetDrmConfiguration?.Invoke(drmDescriptor);
                        }
                    }
                }
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
                packet.Dts += demuxerTimeStamp;
                packet.Pts += demuxerTimeStamp;

                PacketReady?.Invoke(packet);
                return;
            }

            PacketReady?.Invoke(Packet.CreateEOS(streamType));
        }

        /// <summary>
        /// OnPacketReady trimming event handler. This is initial handler to be used
        /// it obtains start PTS/DTS, then replaces a handler with one that only trims.
        /// </summary>
        /// <param name="packet">Packet to process</param>
        private void OnPacketReadyTrimPtsDts(Packet packet)
        {
            if (packet != null)
            {

                // Sometimes we can receive invalid timestamp from demuxer
                // eg during encrypted content seek or live video.
                // Adjust timestamps to avoid playback problems
                packet.Dts += demuxerTimeStamp;
                packet.Pts += demuxerTimeStamp;

                //Get very first PTS/DTS
                if (haveFirstPTSDTS == false)
                {
                    firstPTS = packet.Dts;
                    firstDTS = packet.Pts;
                    haveFirstPTSDTS = true;
                }
                
                // Trim PTS/DTS
                packet.Pts -= firstPTS;
                packet.Dts -= firstDTS;

                PacketReady?.Invoke(packet);
                return;
            }

            PacketReady?.Invoke(Packet.CreateEOS(streamType));
        }

        

        private void AdjustDemuxerTimeStampIfNeeded(Packet packet)
        {
            if (laskSeek == TimeSpan.Zero)
                return;

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
            demuxer?.Dispose();
        }
    }
}
