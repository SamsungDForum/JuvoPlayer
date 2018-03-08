using System;
using System.Linq;
using System.Xml;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Logging;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Drms.Cenc;
using MpdParser;

namespace JuvoPlayer.DataProviders.Dash
{
    class DashMediaPipeline
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event PacketReady PacketReady;

        private readonly IDashClient dashClient;
        private readonly IDemuxer demuxer;
        private readonly StreamType streamType;

        public DashMediaPipeline(IDashClient dashClient, IDemuxer demuxer, StreamType streamType)
        {
            this.dashClient = dashClient ?? throw new ArgumentNullException(nameof(dashClient), "dashClient cannot be null");
            this.demuxer = demuxer ?? throw new ArgumentNullException(nameof(dashClient), "demuxer cannot be null");
            this.streamType = streamType;

            demuxer.DRMInitDataFound += OnDRMInitDataFound;
            demuxer.StreamConfigReady += OnStreamConfigReady;
            demuxer.PacketReady += OnPacketReady;
        }

        public void Start(Media newMedia)
        {
            if (newMedia == null)
                throw new ArgumentNullException(nameof(newMedia), "newMedia cannot be null");

            Logger.Info("Dash start.");

            Logger.Info(string.Format("{0} Media: {1}", streamType, newMedia));

            // get first element of sorted array 
            var representation = newMedia.Representations.OrderByDescending(o => o.Bandwidth).First();
            Logger.Info(representation.ToString());

            dashClient.SetRepresentation(representation);
            ParseDrms(newMedia);

            dashClient.Start();
            demuxer.StartForExternalSource(InitializationMode.Full);
        }
        public void Stop()
        {
            dashClient.Stop();
            demuxer.Dispose();
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

            // Set new times 
            dashClient.Seek(time);

            // Start downloading and parsing new data
            dashClient.Start();
            demuxer.StartForExternalSource(InitializationMode.Minimal);
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
                    catch (Exception e)
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
                    catch (Exception e)
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
                PacketReady?.Invoke(packet);
                return;
            }

            PacketReady?.Invoke(Packet.CreateEOS(streamType));
        }

        private void OnStreamConfigReady(StreamConfig config)
        {
            StreamConfigReady?.Invoke(config);
        }
    }
}
