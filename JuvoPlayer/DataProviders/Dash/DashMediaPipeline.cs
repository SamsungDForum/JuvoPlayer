/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Xml;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Drms;
using MpdParser;
using System.Threading;
using System.Threading.Tasks;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashMediaPipeline : IDisposable
    {
        private class DashStream : IEquatable<DashStream>
        {
            public DashStream(AdaptationSet media, Representation representation)
            {
                Media = media;
                Representation = representation;
            }

            public AdaptationSet Media { get; }
            public Representation Representation { get; }

            public override bool Equals(object obj)
            {
                return obj is DashStream stream && Equals(stream);
            }

            public bool Equals(DashStream other)
            {
                return other != null && (EqualityComparer<AdaptationSet>.Default.Equals(Media, other.Media) &&
                                         EqualityComparer<Representation>.Default.Equals(Representation,
                                             other.Representation));
            }

            public override int GetHashCode()
            {
                var hashCode = 1768762187;
                hashCode = hashCode * -1521134295 + base.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<AdaptationSet>.Default.GetHashCode(Media);
                hashCode = hashCode * -1521134295 +
                           EqualityComparer<Representation>.Default.GetHashCode(Representation);
                return hashCode;
            }
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly IDashClient dashClient;
        private readonly IDemuxerController demuxerController;
        private readonly IThroughputHistory throughputHistory;
        public StreamType StreamType { get; }

        private volatile bool pipelineStarted;
        public bool DisableAdaptiveStreaming { get; set; }

        private DashStream currentStream;

        private DashStream pendingStream;

        private readonly Object switchStreamLock = new Object();
        private List<DashStream> availableStreams = new List<DashStream>();

        private readonly Subject<DrmInitData> drmInitDataSubject = new Subject<DrmInitData>();
        private readonly Subject<DrmDescription> setDrmConfigurationSubject = new Subject<DrmDescription>();
        private readonly Subject<Packet> packetReadySubject = new Subject<Packet>();
        private readonly Subject<StreamConfig> demuxerStreamConfigReadySubject = new Subject<StreamConfig>();
        private readonly Subject<StreamConfig> metaDataStreamConfigSubject = new Subject<StreamConfig>();

        private IDisposable demuxerPacketReadySub;
        private IDisposable demuxerStreamConfigReadySub;
        private IDisposable clientReadySub;

        public Func<Packet, bool> PacketPredicate { get; set; }
        private static readonly TimeSpan timeBufferDepthDefault = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan maxBufferTime = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan minBufferTime = TimeSpan.FromSeconds(5);

        public DashMediaPipeline(IDashClient dashClient, IDemuxerController demuxerController,
            IThroughputHistory throughputHistory,
            StreamType streamType)
        {
            this.dashClient = dashClient ??
                              throw new ArgumentNullException(nameof(dashClient), "dashClient cannot be null");
            this.demuxerController = demuxerController ??
                                     throw new ArgumentNullException(nameof(demuxerController), "cannot be null");
            this.throughputHistory = throughputHistory ??
                                     throw new ArgumentNullException(nameof(throughputHistory),
                                         "throughputHistory cannot be null");
            StreamType = streamType;

            clientReadySub = dashClient.Ready()
                .Subscribe(async unit => await OnClientReady(), SynchronizationContext.Current);
            SubscribeDemuxerEvents();

        }

        public bool IsDataAvailable() =>
            (!pipelineStarted || dashClient.IsDataAvailable());

        public void SetDataRequest(TimeSpan request) =>
            dashClient.SetDataRequest(request);

        private async Task OnClientReady()
        {
            try
            {
                AdaptToNetConditions();
                await SwitchStreamIfNeeded();

                dashClient.ScheduleNextSegDownload();
            }
            catch (TaskCanceledException ex)
            {
                Logger.Warn(ex, $"{StreamType}: Doesn't schedule next segment to download");
            }
            catch (OperationCanceledException ex)
            {
                Logger.Warn(ex, $"{StreamType}: Doesn't schedule next segment to download");
            }
        }

        public Representation GetRepresentation()
        {
            return pendingStream?.Representation ?? currentStream?.Representation;
        }

        public void UpdateMedia(Period period)
        {
            var media = period.GetAdaptationSets(ToMediaType(StreamType));
            if (!media.Any())
                throw new ArgumentOutOfRangeException($"{StreamType}: No media in period {period}");

            lock (switchStreamLock)
            {
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
                        currentStream = new DashStream(currentMedia, currentRepresentation);
                        dashClient.UpdateRepresentation(currentStream.Representation);

                        PushMetaDataConfiguration();

                        return;
                    }
                }

                var defaultMedia = GetDefaultMedia(media);
                GetAvailableStreams(media, defaultMedia);
                // get first element of sorted array
                var representation = defaultMedia.Representations.OrderByDescending(o => o.Bandwidth).Last();

                pendingStream = new DashStream(defaultMedia, representation);
            }
        }

        public void AdaptToNetConditions()
        {
            // Treat adaptive streaming as "non critical". If cannot be processed now due to lock, attempt
            // will be done at next iteration.
            if (!Monitor.TryEnter(switchStreamLock))
                return;

            try
            {
                if (DisableAdaptiveStreaming)
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
                Logger.Debug("  current stream bandwidth: " + streamToAdapt.Representation.Bandwidth.Value);

                // availableStreams is sorted array by descending bandwidth
                var stream = availableStreams.FirstOrDefault(o =>
                                 o.Representation.Bandwidth <= currentThroughput) ?? availableStreams.Last();

                if (stream.Representation.Bandwidth == streamToAdapt.Representation.Bandwidth) return;

                Logger.Info("Changing stream to bandwidth: " + stream.Representation.Bandwidth);
                pendingStream = stream;
            }
            finally
            {
                Monitor.Exit(switchStreamLock);
            }
        }

        public async Task SwitchStreamIfNeeded()
        {
            // Access serialization is needed.
            // SwitchStreamIfNeeded can be called from DashDataProvider or
            // timer based manifest reload (different threads) which can cause
            // null object reference if pendingStream is nulled AFTER another thread
            // passed pendingStream null check.
            //
            // Stream switching does not need to be serialized. If stream switch is already
            // in progress, next stream switch can safely be ignored, thus use of monitor
            // rather then lock.

            if (!Monitor.TryEnter(switchStreamLock))
                return;

            try
            {
                if (pendingStream == null)
                    return;

                if (pipelineStarted)
                {
                    await FlushPipeline();
                }

                StartPipeline(pendingStream);
                pendingStream = null;
            }
            finally
            {
                Monitor.Exit(switchStreamLock);
            }
        }

        private void SetStream(DashStream newStream)
        {
            Logger.Info("");
            Monitor.Enter(switchStreamLock);
            try
            {
                DisableAdaptiveStreaming = true;
                currentStream = newStream;
                pendingStream = null;
                dashClient.UpdateRepresentation(currentStream.Representation);
            }
            finally
            {
                Monitor.Exit(switchStreamLock);
            }
        }

        private void PushMetaDataConfiguration()
        {
            var metaConfig = GetStreamMetaDataConfig();
            if (metaConfig == null)
                return;

            metaDataStreamConfigSubject.OnNext(metaConfig);
        }


        private void GetAvailableStreams(IEnumerable<AdaptationSet> media, AdaptationSet defaultMedia)
        {
            // Not perfect algorithm.
            // check if default media has many representations. if yes, return as available streams
            // list of default media representation + representations from any media from the same group
            // if no, return all available medias
            // TODO(p.galiszewski): add support for: SupplementalProperty schemeIdUri="urn:mpeg:dash:adaptation-set-switching:2016"
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
            if (pipelineStarted)
                return;

            if (newStream != null)
            {
                currentStream = newStream;

                Logger.Info($"{StreamType}: Dash pipeline start.");
                Logger.Info($"{StreamType}: Media: {currentStream.Media}");
                Logger.Info($"{StreamType}: {currentStream.Representation}");

                dashClient.UpdateRepresentation(currentStream.Representation);
                ParseDrms(currentStream.Media);
                PushMetaDataConfiguration();
            }

            var fullInitRequired = (newStream != null) || DisableAdaptiveStreaming;

            demuxerController.StartForEs();
            dashClient.Start(fullInitRequired);

            pipelineStarted = true;
        }

        private static AdaptationSet GetDefaultMedia(ICollection<AdaptationSet> medias)
        {
            AdaptationSet media = null;
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

            dashClient.Stop();

            ResetPipeline();

            currentStream = null;
            pendingStream = null;
        }

        public void OnTimeUpdated(TimeSpan time)
        {
            dashClient.OnTimeUpdated(time);
        }

        public TimeSpan Seek(TimeSpan time)
        {
            try
            {
                return dashClient.Seek(time);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new SeekException("Invalid content", ex);
            }
        }

        public bool ChangeStream(StreamDescription stream)
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

            if (newStream.Equals(currentStream))
            {
                Logger.Info($"Selected stream {stream.Id} {stream.Description} already playing. Not changing.");
                return false;
            }


            SetStream(newStream);
            Logger.Info($"Stream {stream.Id} {stream.Description} set.");
            return true;
        }

        private void ResetPipeline()
        {
            if (!pipelineStarted)
                return;

            Logger.Info($"{StreamType}:");

            // Stop demuxer and dashclient
            dashClient.Reset();

            demuxerController.Reset();
            DisposeDemuxerSubscriptions();
            SubscribeDemuxerEvents();

            pipelineStarted = false;
        }

        private async Task FlushPipeline()
        {
            if (!pipelineStarted)
                return;

            Logger.Info($"{StreamType}");

            // Stop demuxer and dashclient
            dashClient.Reset();
            await demuxerController.Flush();

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

        private void ParseDrms(AdaptationSet newMedia)
        {
            foreach (var descriptor in newMedia.ContentProtections)
            {
                var schemeIdUri = descriptor.SchemeIdUri;
                if (EmeUtils.SupportsSchemeIdUri(schemeIdUri))
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
            if (initData == null)
                return;

            var drmInitData = new DrmInitData
            {
                KeyIDs = EmeUtils.GetKeyIDs(descriptor.CencDefaultKID),
                DataType = EmeUtils.GetInitDataType(doc.FirstChild?.FirstChild?.Name),
                InitData = Convert.FromBase64String(initData),
                SystemId = EmeUtils.SchemeIdUriToSystemId(schemeIdUri),
                // Stream Type will be appended during OnDRMInitDataFound()
            };

            drmInitDataSubject.OnNext(drmInitData);
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
                if (!EmeUtils.SupportsType(type))
                    continue;

                var drmDescriptor = new DrmDescription
                {
                    LicenceUrl = node.InnerText,
                    Scheme = type
                };
                setDrmConfigurationSubject.OnNext(drmDescriptor);
            }
        }

        public IObservable<DrmInitData> OnDRMInitDataFound()
        {
            return demuxerController.DrmInitDataFound()
                .Merge(drmInitDataSubject.AsObservable())
                .Select(data =>
                {
                    data.StreamType = StreamType;
                    return data;
                });
        }

        public IObservable<Packet> PacketReady()
        {
            return packetReadySubject.AsObservable()
                .Select(packet => packet == null ? EOSPacket.Create(StreamType) : packet)
                .Where(packet => PacketPredicate == null || PacketPredicate.Invoke(packet));
        }

        public IObservable<DrmDescription> SetDrmConfiguration()
        {
            return setDrmConfigurationSubject.AsObservable();
        }

        public IObservable<string> StreamError()
        {
            return dashClient.ErrorOccurred().Merge(demuxerController.DemuxerError());
        }

        public IObservable<StreamConfig> StreamConfigReady()
        {
            return demuxerStreamConfigReadySubject
                .Merge(metaDataStreamConfigSubject).AsObservable();

        }

        private void DisposeDemuxerSubscriptions()
        {
            demuxerPacketReadySub?.Dispose();
            demuxerStreamConfigReadySub?.Dispose();
        }

        private void SubscribeDemuxerEvents()
        {
            demuxerPacketReadySub = demuxerController.PacketReady()
                .Subscribe(packet => packetReadySubject.OnNext(packet),
                    () => packetReadySubject.OnCompleted(),
                    SynchronizationContext.Current);

            demuxerStreamConfigReadySub = demuxerController.StreamConfigReady()
                .Subscribe(config => demuxerStreamConfigReadySubject.OnNext(config), SynchronizationContext.Current);
        }

        BufferStreamConfig GetStreamMetaDataConfig()
        {
            var representation = GetRepresentation();
            if (representation == null)
                return null;

            var mpdDoc = representation.Segments.GetDocumentParameters().Document;

            return new BufferStreamConfig
            {
                Stream = StreamType,
                Bandwidth = representation.Bandwidth,
                MinBufferTime = mpdDoc.MinBufferTime,
                BufferDuration = GetTimeBufferDepth(mpdDoc, representation)
            };
        }

        private static TimeSpan TimeBufferDepthDynamic(DocumentParameters docParams)
        {
            // For dynamic content, use TimeShiftBuffer depth as it defines "available" content.
            // 1/4 of the buffer is "used up" when selecting start segment.
            // Start Segment = First Available + 1/4 of Time Shift Buffer.
            // Use max 1/2 of TimeShiftBufferDepth.
            //var docParams = GetRepresentation().Segments.GetDocumentParameters();
            if (docParams == null)
                throw new ArgumentNullException("currentStreams.GetDocumentParameters() returns null");
            var tsBuffer = (int)(docParams.TimeShiftBufferDepth?.TotalSeconds ??
                                  0);
            tsBuffer = tsBuffer / 2;

            // If value is out of range, truncate to max 15 seconds.
            return (tsBuffer == 0) ? timeBufferDepthDefault : TimeSpan.FromSeconds(tsBuffer);
        }

        private static TimeSpan TimeBufferDepthStatic(DocumentParameters docParams, Representation repStream)
        {
            var duration = repStream.Segments.Duration;
            if (!duration.HasValue)
                return timeBufferDepthDefault;

            var segments = repStream.Segments.Count;
            if (0 == segments)
                return timeBufferDepthDefault;

            if (docParams == null)
                throw new ArgumentNullException("currentStreams.GetDocumentParameters() returns null");

            var manifestMinBufferDepth =
                docParams.MinBufferTime ?? TimeSpan.FromSeconds(10);

            //Get average segment duration = Total Duration / number of segments.
            var avgSegmentDuration = TimeSpan.FromSeconds(
                duration.Value.TotalSeconds / segments);
            var portionOfSegmentDuration = TimeSpan.FromSeconds(avgSegmentDuration.TotalSeconds * 0.15);
            var safeTimeBufferDepth = TimeSpan.FromTicks(avgSegmentDuration.Ticks * 3) + portionOfSegmentDuration;

            TimeSpan timeBufferDepth;

            if (safeTimeBufferDepth >= manifestMinBufferDepth)
            {
                timeBufferDepth = safeTimeBufferDepth;
            }
            else
            {
                timeBufferDepth = manifestMinBufferDepth;
                var bufferLeft = manifestMinBufferDepth - portionOfSegmentDuration;
                if (bufferLeft < portionOfSegmentDuration)
                    timeBufferDepth += portionOfSegmentDuration;
            }

            Logger.Info(
                $"Average Segment Duration: {avgSegmentDuration} Manifest Min. Buffer Time: {manifestMinBufferDepth}");

            return timeBufferDepth;
        }

        private static TimeSpan GetTimeBufferDepth(DocumentParameters docParams, Representation repStream)
        {
            TimeSpan bufferSize;
            if (docParams.IsDynamic)
                bufferSize = TimeBufferDepthDynamic(docParams);
            else
                bufferSize = TimeBufferDepthStatic(docParams, repStream);
            if (bufferSize > maxBufferTime)
                bufferSize = maxBufferTime;
            else if (bufferSize < minBufferTime)
                bufferSize = minBufferTime;
            Logger.Info($"TimeBufferDepth: {bufferSize}");

            return bufferSize;
        }

        public void Dispose()
        {
            demuxerController.Dispose();
            dashClient.Dispose();

            DisposeAllSubjects();

            DisposeDemuxerSubscriptions();
            clientReadySub.Dispose();
        }

        private void DisposeAllSubjects()
        {
            drmInitDataSubject.Dispose();
            setDrmConfigurationSubject.Dispose();
            packetReadySubject.Dispose();
            demuxerStreamConfigReadySubject.Dispose();
            metaDataStreamConfigSubject.Dispose();
        }
    }
}
