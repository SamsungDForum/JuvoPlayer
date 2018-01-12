using JuvoPlayer.Common;
using JuvoPlayer.DRM.Cenc;
using MpdParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace JuvoPlayer.Dash
{
    class DashMediaPipeline
    {
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event StreamPacketReady StreamPacketReady;

        private IDashClient dashClient;
        private IDemuxer demuxer;
        private StreamType streamType;

        public DashMediaPipeline(IDashClient dashClient, IDemuxer demuxer, StreamType streamType)
        {
            this.dashClient = dashClient ?? throw new ArgumentNullException(nameof(dashClient), "dashClient cannot be null");
            this.demuxer = demuxer ?? throw new ArgumentNullException(nameof(dashClient), "demuxer cannot be null");
            this.streamType = streamType;

            demuxer.DRMInitDataFound += OnDRMInitDataFound;
            demuxer.StreamConfigReady += OnStreamConfigReady;
            demuxer.StreamPacketReady += OnStreamPacketReady;
        }

        public void Start(Media newMedia)
        {
            if (newMedia == null)
                throw new ArgumentNullException(nameof(newMedia), "newMedia cannot be null");

            Tizen.Log.Info("JuvoPlayer", "Dash start.");

            dashClient.UpdateMedia(newMedia);
            ParseDrms(newMedia);

            dashClient.Start();
            demuxer.StartForExternalSource();
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            dashClient.OnTimeUpdated(time);
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

                    var drmDescriptor = new DRMDescription()
                    {
                        InitData = initData,
                        Scheme = CencUtils.SchemeIdUriToType(schemeIdUri)
                    };
                    SetDrmConfiguration?.Invoke(drmDescriptor);
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

        private void OnStreamPacketReady(StreamPacket packet)
        {
            if (packet != null)
            {
                StreamPacketReady?.Invoke(packet);
                return;
            }

            StreamPacketReady?.Invoke(StreamPacket.CreateEOS(streamType));
        }

        private void OnStreamConfigReady(StreamConfig config)
        {
            StreamConfigReady?.Invoke(config);
        }
    }
}
