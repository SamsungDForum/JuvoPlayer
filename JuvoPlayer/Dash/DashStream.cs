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
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Dash.MPD;
using JuvoPlayer.Demuxers;
using Log = JuvoLogger.Log;

namespace JuvoPlayer.Dash
{
    public class DashStream : IStream
    {
        private readonly IClock _clock;
        private readonly IDemuxer _demuxer;
        private readonly IDownloader _downloader;
        private ILogger _logger = Log.Logger;
        private readonly IThroughputHistory _throughputHistory;
        private readonly Subject<IEvent> _eventSubject;
        private AdaptationSet _adaptationSet;
        private TimeSpan? _periodDuration;
        private TimeSpan _bufferPosition;
        private ClipConfiguration _clipConfiguration;
        private RepresentationWrapper _currentRepresentation;
        private TaskCompletionSource<StreamConfig> _getStreamConfigTaskCompletionSource;
        private TimeSpan _maxBufferTime;
        private long? _previousSegmentNum;
        private IStreamRenderer _renderer;
        private RepresentationWrapper[] _representations;
        private Segment _segment;
        private DashDemuxerDataSource _demuxerDataSource;
        private DemuxerController _demuxerController;
        private CancellationToken _cancellationToken;

        public DashStream(
            IThroughputHistory throughputHistory,
            IDownloader downloader,
            IClock clock,
            IDemuxer demuxer,
            IStreamSelector streamSelector)
        {
            _throughputHistory = throughputHistory;
            _downloader = downloader;
            _clock = clock;
            _demuxer = demuxer;
            _eventSubject = new Subject<IEvent>();
            StreamSelector = streamSelector;
            _maxBufferTime = TimeSpan.FromSeconds(8);
        }

        public StreamGroup StreamGroup { get; private set; }

        public IObservable<IEvent> OnEvent() => _eventSubject.AsObservable();
        public IStreamSelector StreamSelector { get; private set; }

        public async Task Prepare()
        {
            if (_representations == null
                || _representations.Length == 0)
                return;

            if (_representations.Any(
                repr =>
                    repr.SegmentIndex != null))
                return;

            var representation = _representations.First();
            var indexChunk = CreateIndexChunk(
                representation,
                CancellationToken.None);
            await indexChunk.Load();
        }

        public async Task LoadChunks(
            Segment segment,
            IStreamRenderer renderer,
            CancellationToken token)
        {
            try
            {
                _logger.Info();
                _segment = segment;
                _renderer = renderer;
                _bufferPosition = segment.Start;
                _previousSegmentNum = null;
                _currentRepresentation = null;
                _cancellationToken = token;

                _demuxerController = new DemuxerController(
                    _demuxer,
                    StreamGroup.ContentType);

                _demuxerController.DemuxerInitialized += DemuxerControllerOnDemuxerInitialized;
                _demuxerController.PacketReady += DemuxerControllerOnPacketReady;
                _demuxerController.Run();
                await RunLoadChunksLoop(token);
            }
            finally
            {
                if (_demuxerDataSource != null &&
                    _demuxerDataSource.Completed == false)
                {
                    _demuxerDataSource.CompleteAdding();
                }

                await _demuxerController.CompleteAsync();
                _demuxerController.DemuxerInitialized -= DemuxerControllerOnDemuxerInitialized;
                _demuxerController.PacketReady -= DemuxerControllerOnPacketReady;
            }
        }

        private async Task DemuxerControllerOnDemuxerInitialized(object sender, DemuxerInitializedEventArgs e)
        {
            _logger.Info();

            _clipConfiguration = e.ClipConfiguration;
            var streamConfigs = _clipConfiguration.StreamConfigs;
            var streamConfig = streamConfigs.Single();
            var cancellationToken = _cancellationToken;
            await _demuxerController.EnableStreams(new List<StreamConfig> {streamConfig},
                cancellationToken,
                callImmediately: true);
            cancellationToken.ThrowIfCancellationRequested();

            _getStreamConfigTaskCompletionSource?.TrySetResult(streamConfig);
            var drmInitData = _clipConfiguration.DrmInitData;
            if (drmInitData != null)
                _renderer.HandleDrmInitData(drmInitData);
        }

        private Task DemuxerControllerOnPacketReady(object sender, PacketReadyEventArgs e)
        {
            var packet = e.Packet;
            if (packet.Pts > _periodDuration)
                return Task.CompletedTask;
            _logger.Info($"Got {packet.StreamType} {packet.Pts}");
            _renderer.HandlePacket(packet);
            return Task.CompletedTask;
        }

        public async Task<StreamConfig> GetStreamConfig(CancellationToken cancellationToken)
        {
            _logger.Info();
            var streamConfigs = _clipConfiguration.StreamConfigs;
            if (streamConfigs != null)
            {
                var streamConfig = streamConfigs.Single();
                return streamConfig;
            }

            _getStreamConfigTaskCompletionSource?.SetCanceled();
            _getStreamConfigTaskCompletionSource =
                new TaskCompletionSource<StreamConfig>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => { _getStreamConfigTaskCompletionSource.TrySetCanceled(); }))
            {
                var getStreamConfigTask =
                    _getStreamConfigTaskCompletionSource.Task;
                var result = await getStreamConfigTask;
                return result;
            }
        }

        public Task<TimeSpan> GetAdjustedSeekPosition(TimeSpan position)
        {
            foreach (var representation in _representations)
            {
                if (representation.SegmentIndex == null)
                    continue;
                var segmentIndex = representation.SegmentIndex;
                var periodDuration = representation.PeriodDuration;
                var segmentNum = segmentIndex.GetSegmentNum(
                    position,
                    periodDuration);
                return Task.FromResult(segmentIndex.GetStartTime(segmentNum));
            }

            return Task.FromResult(position);
        }

        public void Dispose()
        {
            _eventSubject.Dispose();
        }

        internal void SetAdaptationSet(
            StreamGroup streamGroup,
            AdaptationSet adaptationSet,
            TimeSpan? periodDuration)
        {
            StreamGroup = streamGroup;
            var contentTypeString = StreamGroup.ContentType.ToString();
            _logger = _logger.CopyWithPrefix(contentTypeString);
            _adaptationSet = adaptationSet;
            _periodDuration = periodDuration;
            var representations =
                _adaptationSet.Representations;
            _representations = representations.Select(
                    repr =>
                        new RepresentationWrapper
                        {
                            Representation = repr,
                            SegmentIndex = repr.GetIndex(),
                            PeriodDuration = periodDuration
                        })
                .ToArray();
        }

        internal void SetStreamSelector(IStreamSelector selector)
        {
            StreamSelector = selector;
        }

        private async Task RunLoadChunksLoop(CancellationToken cancellationToken)
        {
            _logger.Info();
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var bufferPosition = _bufferPosition;
                    var playbackPosition = _segment.ToPlaybackTime(_clock.Elapsed);
                    var bufferedDuration = bufferPosition - playbackPosition;
                    _logger.Info($"{bufferedDuration} = {bufferPosition} - {playbackPosition}");
                    if (bufferedDuration <= _maxBufferTime)
                    {
                        if (!await LoadNextChunk(cancellationToken))
                            break;
                        continue;
                    }

                    var halfBufferTime = TimeSpan.FromTicks(_maxBufferTime.Ticks / 2);
                    var delayTime = bufferedDuration - halfBufferTime;
                    await Task.Delay(delayTime, cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                var exceptionEvent = new ExceptionEvent(ex);
                _eventSubject.OnNext(exceptionEvent);
            }

            _logger.Info("Load loop finished");
        }

        private async Task<bool> LoadNextChunk(CancellationToken cancellationToken)
        {
            _logger.Info();
            var previousRepresentation = _currentRepresentation;
            var selectedIndex = SelectRepresentationIndex();
            var representation = _representations[selectedIndex];
            var changedRepresentation = false;
            if (previousRepresentation != representation)
            {
                _logger.Info("Change of representation");
                changedRepresentation = true;
                ReinitDemuxer(
                    representation.InitData,
                    cancellationToken);
                _currentRepresentation = representation;
            }

            var chunk = GetNextChunk(
                representation,
                cancellationToken,
                _bufferPosition,
                _previousSegmentNum);
            var sendEos = chunk == null;
            try
            {
                if (chunk != null)
                {
                    _logger.Info("Loading chunk");
                    if (changedRepresentation)
                        SendStreamInfoChangedEvent(selectedIndex);

                    await chunk.Load();
                    cancellationToken.ThrowIfCancellationRequested();
                    OnChunkLoaded(chunk,
                        representation);
                    return true;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex);
                // Send EOS if we got exception for the last segment
                if (chunk is DataChunk dataChunk)
                {
                    var lastSegmentNum =
                        GetLastSegmentNum(representation);
                    if (dataChunk.SegmentNum == lastSegmentNum)
                        sendEos = true;
                }

                if (!sendEos)
                {
                    _demuxerDataSource.CompleteAdding();
                    throw;
                }
            }
            finally
            {
                if (sendEos)
                {
                    _logger.Info("Sending EOS packet");
                    var contentType = _adaptationSet.ContentType;
                    var streamType = contentType.ToStreamType();
                    _demuxerDataSource.CompleteAdding();
                    _demuxerController.NotifyEos(
                        new List<StreamType> {streamType},
                        cancellationToken);
                }
            }

            return false;
        }

        private void SendStreamInfoChangedEvent(int selectedIndex)
        {
            var infoChangedEvent = new StreamInfoChangedEvent(
                StreamGroup,
                selectedIndex,
                _bufferPosition);
            _eventSubject.OnNext(infoChangedEvent);
        }

        private int SelectRepresentationIndex()
        {
            return StreamSelector.Select(StreamGroup);
        }

        private void ReinitDemuxer(
            IList<byte[]> initData,
            CancellationToken cancellationToken)
        {
            _logger.Info();

            _demuxerDataSource?.CompleteAdding();

            _demuxerDataSource = new DashDemuxerDataSource();
            _demuxerController.Init(
                _demuxerDataSource,
                cancellationToken);
            var minPts =
                _adaptationSet.ContentType == ContentType.Audio ? (TimeSpan?) _segment.Start : null;
            _demuxerController.GetPackets(
                minPts,
                cancellationToken);
            if (initData == null)
                return;

            var initDataLength = initData.Sum(bytes => bytes.Length);
            var seg = new SegmentBuffer();
            _demuxerDataSource.Offset = 0;
            _demuxerDataSource.AddSegmentBuffer(seg);
            foreach (var bytes in initData)
                seg.Add(bytes);

            _demuxerDataSource.Offset = initDataLength;
            seg.CompleteAdding();
        }

        private IChunk GetNextChunk(
            RepresentationWrapper representationWrapper,
            CancellationToken cancellationToken,
            TimeSpan bufferPosition,
            long? previousSegmentNum)
        {
            if (representationWrapper.InitData == null)
            {
                return CreateInitializationChunk(
                    representationWrapper,
                    cancellationToken);
            }

            if (representationWrapper.SegmentIndex == null)
            {
                return CreateIndexChunk(
                    representationWrapper,
                    cancellationToken);
            }

            return CreateDataChunk(
                representationWrapper,
                bufferPosition,
                previousSegmentNum,
                cancellationToken);
        }

        private IChunk CreateInitializationChunk(
            RepresentationWrapper representationWrapper,
            CancellationToken cancellationToken)
        {
            var representation = representationWrapper.Representation;
            var baseUrl = representation.BaseUrl;
            var initializationUri = representation.GetInitializationUri();
            var finalUrl = initializationUri.ResolveUri(baseUrl);
            return new InitializationChunk(
                finalUrl,
                initializationUri.Start,
                initializationUri.Length,
                representationWrapper,
                _downloader,
                _throughputHistory,
                _demuxerDataSource,
                cancellationToken);
        }

        private IChunk CreateIndexChunk(RepresentationWrapper representationWrapper,
            CancellationToken cancellationToken)
        {
            var representation = representationWrapper.Representation;
            var baseUrl = representation.BaseUrl;
            var indexUri = representation.GetIndexUri();
            var finalUrl = indexUri.ResolveUri(baseUrl);
            return new IndexChunk(
                finalUrl,
                indexUri.Start,
                indexUri.Length,
                representationWrapper,
                _downloader,
                _throughputHistory,
                cancellationToken);
        }

        private IChunk CreateDataChunk(RepresentationWrapper representationWrapper,
            TimeSpan bufferPosition,
            long? previousSegmentNum, CancellationToken cancellationToken)
        {
            var segmentNum = GetNextSegmentNum(
                representationWrapper,
                bufferPosition,
                previousSegmentNum);
            var lastSegmentNum = GetLastSegmentNum(representationWrapper);
            if (segmentNum > lastSegmentNum)
                return null;
            var segmentIndex = representationWrapper.SegmentIndex;
            var segmentUrl =
                segmentIndex.GetSegmentUrl(segmentNum);
            var representation = representationWrapper.Representation;
            var baseUrl = representation.BaseUrl;
            var finalUri = segmentUrl.ResolveUri(baseUrl);
            return new DataChunk(
                finalUri,
                segmentUrl.Start,
                segmentUrl.Length,
                segmentNum,
                _downloader,
                _throughputHistory,
                _demuxerDataSource,
                cancellationToken);
        }

        private long GetNextSegmentNum(
            RepresentationWrapper representationWrapper,
            TimeSpan bufferPosition,
            long? previousSegmentNum)
        {
            var segmentIndex = representationWrapper.SegmentIndex;
            if (previousSegmentNum.HasValue)
                return previousSegmentNum.Value + 1;
            return segmentIndex.GetSegmentNum(
                bufferPosition,
                representationWrapper.PeriodDuration);
        }

        private long GetLastSegmentNum(RepresentationWrapper representationWrapper)
        {
            var segmentIndex = representationWrapper.SegmentIndex;
            var periodDuration = representationWrapper.PeriodDuration;
            var firstSegmentNum = segmentIndex.GetFirstSegmentNum();
            var segmentCount = segmentIndex.GetSegmentCount(periodDuration);
            if (!segmentCount.HasValue)
                throw new NotImplementedException("Dynamic manifests are not supported yet");
            return firstSegmentNum + segmentCount.Value - 1;
        }

        private void OnChunkLoaded(
            IChunk chunk,
            RepresentationWrapper representationWrapper)
        {
            switch (chunk)
            {
                case DataChunk dataChunk:
                {
                    OnDataChunkLoaded(representationWrapper, dataChunk);
                    break;
                }

                default:
                    return;
            }
        }

        private void OnDataChunkLoaded(
            RepresentationWrapper representationWrapper,
            DataChunk dataChunk)
        {
            var segmentNum = dataChunk.SegmentNum;
            var segmentIndex = representationWrapper.SegmentIndex;
            var periodDuration = representationWrapper.PeriodDuration;
            var segmentStartTime = segmentIndex.GetStartTime(segmentNum);
            var segmentDuration =
                segmentIndex.GetDuration(segmentNum, periodDuration);
            _previousSegmentNum = segmentNum;
            _bufferPosition = segmentStartTime + segmentDuration.Value;
        }
    }
}