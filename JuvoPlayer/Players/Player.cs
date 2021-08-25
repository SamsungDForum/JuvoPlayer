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
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;

namespace JuvoPlayer.Players
{
    public class Player : IPlayer
    {
        private readonly Configuration _configuration;

        private readonly Func<IPlatformPlayer> _platformPlayerFactory;
        private readonly IWindow _window;
        private readonly Clock _clock;
        private CancellationTokenSource _cancellationTokenSource;
        private Period _currentPeriod;
        private IPlatformPlayer _platformPlayer;
        private IDisposable _platformPlayerEosSubscription;
        private Segment _segment;
        private Dictionary<ContentType, StreamHolder> _streamHolders;
        private IStreamProvider _streamProvider;
        private readonly CdmContext _cdmContext;
        private int _numberOfStarvingStreams;
        private readonly Subject<IEvent> _eventSubject;

        public Player(
            Func<IPlatformPlayer> platformPlayerFactory,
            CdmContext cdmContext,
            Clock clock,
            IWindow window,
            Configuration configuration)
        {
            _platformPlayerFactory = platformPlayerFactory;
            _cdmContext = cdmContext;
            _clock = clock;
            _window = window;
            _configuration = configuration;
            _eventSubject = new Subject<IEvent>();
        }

        public TimeSpan? Duration => _streamProvider?.GetDuration();

        public TimeSpan? Position
        {
            get
            {
                try
                {
                    return _platformPlayer?.GetPosition();
                }
                catch (Exception)
                {
                    return _segment.Start;
                }
            }
        }

        public PlayerState State
        {
            get
            {
                try
                {
                    return _platformPlayer?.GetState() ?? PlayerState.None;
                }
                catch (Exception)
                {
                    return PlayerState.None;
                }
            }
        }

        public async Task Prepare()
        {
            Log.Info();
            _cancellationTokenSource = new CancellationTokenSource();
            _platformPlayer = _platformPlayerFactory.Invoke();
            _currentPeriod = await PrepareStreamProvider();
            var streamGroups = SelectDefaultStreamsGroups(_currentPeriod);
            CreateStreamHolders(
                streamGroups,
                new IStreamSelector[streamGroups.Length]);
            await PrepareStreams();
            var startTime = _currentPeriod.StartTime ?? TimeSpan.Zero;
            if (_configuration.StartTime != null)
            {
                var requestedStartTime = _configuration.StartTime.Value;
                if (requestedStartTime > startTime)
                    startTime = requestedStartTime;
            }

            await UpdateSegment(startTime);
            LoadChunks();

            var streamConfigs =
                await Task.WhenAll(GetStreamConfigs());
            await PreparePlayer(streamConfigs);
        }

        public async Task Seek(TimeSpan position)
        {
            Log.Info($"Seek to {position}");
            if (_platformPlayer == null)
                throw new InvalidOperationException("Prepare not called");

            var stateBeforeSeek = _platformPlayer.GetState();
            if (stateBeforeSeek == PlayerState.Playing)
                PausePlayer();
            await StopStreaming();

            await UpdateSegment(position);
            position = _segment.Start;
            LoadChunks();

            await SeekPlayer(position);

            if (stateBeforeSeek == PlayerState.Playing)
                Play();
        }

        public StreamGroup[] GetStreamGroups()
        {
            return _streamProvider.GetStreamGroups(_currentPeriod);
        }

        public (StreamGroup[], IStreamSelector[]) GetSelectedStreamGroups()
        {
            return _streamProvider.GetSelectedStreamGroups();
        }

        public async Task SetStreamGroups(StreamGroup[] streamGroups, IStreamSelector[] selectors)
        {
            Log.Info();
            if (_platformPlayer == null)
                throw new InvalidOperationException("Prepare not called");
            VerifyStreamGroups(streamGroups, selectors);

            var previousState = _platformPlayer.GetState();
            if (previousState == PlayerState.Playing)
                PausePlayer();
            var position = _platformPlayer.GetState() == PlayerState.Ready
                ? _segment.Start
                : _platformPlayer.GetPosition();
            await StopStreaming();

            // Create new streams
            var streamHolders = new Dictionary<ContentType, StreamHolder>();
            var shallRecreatePlayer = false;
            for (var index = 0; index < streamGroups.Length; ++index)
            {
                var streamGroup = streamGroups[index];
                var selector = selectors[index];
                var contentType = streamGroup.ContentType;

                // Add new stream
                if (!_streamHolders.ContainsKey(contentType))
                {
                    shallRecreatePlayer = true;
                    streamHolders[contentType] = CreateStreamHolder(
                        streamGroup,
                        selector);
                    continue;
                }

                var oldStreamHolder = _streamHolders[contentType];
                var oldStreamGroup = oldStreamHolder.StreamGroup;
                // Replace streams
                if (streamGroup != oldStreamGroup)
                {
                    // TODO: Consider to check if stream groups are 'compatible'
                    shallRecreatePlayer = true;
                    DisposeStreamHolder(oldStreamHolder);
                    streamHolders[contentType] = CreateStreamHolder(
                        streamGroup,
                        selector);
                    continue;
                }

                var platformCapabilities = Platform.Current.Capabilities;
                var supportsSeamlessAudioChange =
                    platformCapabilities.SupportsSeamlessAudioChange;
                if (!supportsSeamlessAudioChange
                    && contentType == ContentType.Audio)
                {
                    var oldStreamSelector = oldStreamHolder.StreamSelector;
                    if (!Equals(oldStreamSelector, selector))
                    {
                        shallRecreatePlayer = true;
                        DisposeStreamHolder(oldStreamHolder);
                        streamHolders[contentType] = CreateStreamHolder(
                            streamGroup,
                            selector);
                        continue;
                    }
                }

                // Reuse previously selected stream and update IStreamSelector only
                _streamHolders.Remove(contentType);
                streamHolders[contentType] = oldStreamHolder;
                var stream = oldStreamHolder.Stream;
                _streamProvider.UpdateStream(stream, selector);
            }

            // If we still have old streamHolders,
            // that means a client selected less streams than previously.
            // We have to dispose them and recreate the player.
            if (_streamHolders.Count > 0)
            {
                shallRecreatePlayer = true;
                foreach (var streamHolder in _streamHolders.Values)
                    DisposeStreamHolder(streamHolder);
            }

            _streamHolders = streamHolders;
            await PrepareStreams();

            await UpdateSegment(position);

            LoadChunks();

            if (shallRecreatePlayer)
            {
                _platformPlayerEosSubscription.Dispose();
                _platformPlayer.Dispose();
                _platformPlayer = _platformPlayerFactory.Invoke();
                var streamConfigs =
                    await Task.WhenAll(GetStreamConfigs());
                await PreparePlayer(streamConfigs);
            }
            else
            {
                await SeekPlayer(_segment.Start);
            }

            // Restore previous state
            if (previousState == PlayerState.Playing)
                Play();
        }

        public void Play()
        {
            if (_numberOfStarvingStreams == 0)
                StartPlayer();

            foreach (var streamHolder in _streamHolders.Values)
            {
                streamHolder.StartPushingPackets(
                    _segment,
                    _platformPlayer);
                var bufferingObserver =
                    streamHolder.BufferingObserver;
                bufferingObserver.Start();
            }
        }

        private void StartPlayer()
        {
            var state = _platformPlayer.GetState();
            switch (state)
            {
                case PlayerState.Ready:
                    _platformPlayer.Start();
                    break;
                case PlayerState.Paused:
                    _platformPlayer.Resume();
                    break;
                default:
                    return;
            }

            _clock.Start();
        }

        public async Task Pause()
        {
            foreach (var streamHolder in _streamHolders.Values)
            {
                var bufferingObserver =
                    streamHolder.BufferingObserver;
                bufferingObserver.Stop();
            }

            PausePlayer();
            foreach (var streamHolder in _streamHolders.Values)
                await streamHolder.StopPushingPackets();
        }

        private void PausePlayer()
        {
            _platformPlayer.Pause();
            _clock.Stop();
        }

        public IObservable<IEvent> OnEvent()
        {
            var eventObservable = _eventSubject.AsObservable();
            if (_cdmContext != null)
                eventObservable = eventObservable.Merge(_cdmContext.OnException());
            return eventObservable;
        }

        public async Task DisposeAsync()
        {
            Log.Info();
            await StopStreaming();
            if (_streamHolders != null)
            {
                foreach (var streamHolder in _streamHolders.Values)
                    DisposeStreamHolder(streamHolder);
            }

            _streamProvider.Dispose();

            _cancellationTokenSource?.Dispose();
            _platformPlayerEosSubscription?.Dispose();
            _platformPlayer?.Dispose();
            _cdmContext?.Dispose();
            _eventSubject.OnCompleted();
            _eventSubject.Dispose();
        }

        private Task PrepareStreams()
        {
            return Task.WhenAll(
                _streamHolders.Values.Select(holder =>
                    holder.Stream.Prepare()));
        }

        private async Task UpdateSegment(TimeSpan startTime)
        {
            if (_streamHolders.ContainsKey(ContentType.Video))
            {
                var videoStream = _streamHolders[ContentType.Video]
                    .Stream;
                if (videoStream != null)
                {
                    var adjustedStartTime = await videoStream.GetAdjustedSeekPosition(startTime);
                    if (adjustedStartTime > startTime)
                        Log.Warn("Seek to a previous key frame is not supported");
                    startTime = adjustedStartTime;
                }
            }

            Log.Info($"{startTime}");

            _segment = new Segment
            {
                Base = _clock.Elapsed,
                Start = startTime,
                Stop = TimeSpan.MinValue
            };

            foreach (var streamHolder in _streamHolders.Values)
            {
                var bufferingObserver
                    = streamHolder.BufferingObserver;
                bufferingObserver.Reset(_segment);
            }

            _numberOfStarvingStreams = 0;
        }

        private StreamGroup[] SelectDefaultStreamsGroups(Period period)
        {
            var allStreamGroups =
                _streamProvider.GetStreamGroups(period);
            var selectedStreamGroups = new List<StreamGroup>();
            var audioStreamGroup = SelectDefaultAudioStreamGroup(
                allStreamGroups);
            if (audioStreamGroup != null)
                selectedStreamGroups.Add(audioStreamGroup);
            var videoStreamGroup = SelectDefaultStreamGroup(
                allStreamGroups,
                ContentType.Video);
            if (videoStreamGroup != null)
                selectedStreamGroups.Add(videoStreamGroup);
            return selectedStreamGroups.ToArray();
        }

        private StreamGroup SelectDefaultStreamGroup(
            StreamGroup[] streamGroups,
            ContentType contentType)
        {
            var filteredStreamGroups = streamGroups
                .Where(streamGroup => streamGroup.ContentType == contentType)
                .ToArray();
            return filteredStreamGroups
                       .FirstOrDefault(streamGroup => streamGroup.Streams.Any(stream =>
                           stream.Format.SelectionFlags == SelectionFlags.Default ||
                           stream.Format.RoleFlags == RoleFlags.Main))
                   ?? filteredStreamGroups.FirstOrDefault();
        }

        private StreamGroup SelectDefaultAudioStreamGroup(StreamGroup[] streamGroups)
        {
            var filteredStreamGroups = streamGroups
                .Where(streamGroup => streamGroup.ContentType == ContentType.Audio);
            if (_configuration.PreferredAudioLanguage != null)
            {
                filteredStreamGroups = filteredStreamGroups.Where(streamGroup =>
                    streamGroup.Streams.Any(stream =>
                        stream.Format.Language == _configuration.PreferredAudioLanguage));
            }

            return filteredStreamGroups.FirstOrDefault()
                   ?? SelectDefaultStreamGroup(streamGroups, ContentType.Audio);
        }

        private async Task<Period> PrepareStreamProvider()
        {
            var timeline = await _streamProvider.Prepare();
            return timeline.Periods[0];
        }

        private async Task PreparePlayer(StreamConfig[] streamConfigs)
        {
            Log.Info();
            _platformPlayer.Open(_window, streamConfigs);
            _platformPlayerEosSubscription = _platformPlayer
                .OnEos()
                .Subscribe(_ => { _eventSubject.OnNext(new EosEvent()); });
            var synchronizationContext = SynchronizationContext.Current;
            var cancellationToken = _cancellationTokenSource.Token;
            await _platformPlayer.PrepareAsync(contentType =>
            {
                Log.Info($"{contentType}");
                var holder = _streamHolders[contentType];
                synchronizationContext.Post(_ =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        holder.StartPushingPackets(
                            _segment,
                            _platformPlayer);
                    }
                }, cancellationToken);
            }, cancellationToken);
        }

        private void LoadChunks()
        {
            Log.Info();
            foreach (var holder in _streamHolders.Values)
            {
                holder.StartLoadingChunks(
                    _segment,
                    _cancellationTokenSource.Token);
            }
        }

        private IEnumerable<Task<StreamConfig>> GetStreamConfigs()
        {
            return _streamHolders.Values.Select(holder =>
            {
                var stream = holder.Stream;
                return stream.GetStreamConfig(_cancellationTokenSource.Token);
            });
        }

        private void CreateStreamHolders(
            IReadOnlyList<StreamGroup> streamGroups,
            IReadOnlyList<IStreamSelector> streamSelectors)
        {
            _streamHolders = new Dictionary<ContentType, StreamHolder>();
            for (var i = 0; i < streamGroups.Count; i++)
            {
                var streamGroup = streamGroups[i];
                var streamSelector = streamSelectors[i];
                var contentType = streamGroup.ContentType;
                _streamHolders[contentType] = CreateStreamHolder(
                    streamGroup,
                    streamSelector);
            }
        }

        private StreamHolder CreateStreamHolder(
            StreamGroup streamGroup,
            IStreamSelector streamSelector)
        {
            var stream = _streamProvider.CreateStream(
                _currentPeriod,
                streamGroup,
                streamSelector);
            var packetSynchronizer = new PacketSynchronizer
            {
                Clock = _clock,
                Offset = TimeSpan.FromSeconds(1)
            };
            var bufferingObserver = new BufferingObserver(
                _clock,
                TimeSpan.FromMilliseconds(200),
                TimeSpan.FromMilliseconds(800));
            var streamRenderer = new StreamRenderer(
                packetSynchronizer,
                _cdmContext,
                bufferingObserver);
            return new StreamHolder(
                stream,
                streamRenderer,
                streamGroup,
                bufferingObserver,
                OnStreamRendererBuffering,
                OnStreamException);
        }

        private void OnStreamRendererBuffering(bool isBuffering)
        {
            var wasBuffering = _numberOfStarvingStreams > 0;
            _numberOfStarvingStreams +=
                isBuffering ? 1 : -1;
            Log.Info($"Is Buffering = {_numberOfStarvingStreams > 0}");
            Debug.Assert(_numberOfStarvingStreams >= 0,
                $"{nameof(_numberOfStarvingStreams)} shouldn't be negative");
            if (_numberOfStarvingStreams == 1 && !wasBuffering)
            {
                PausePlayer();
                _eventSubject.OnNext(new BufferingEvent(true));
            }
            else if (_numberOfStarvingStreams == 0)
            {
                StartPlayer();
                _eventSubject.OnNext(new BufferingEvent(false));
            }
        }

        private async void OnStreamException(Exception ex)
        {
            await StopStreaming();
            _eventSubject.OnNext(new ExceptionEvent(ex));
        }

        private void DisposeStreamHolder(StreamHolder streamHolder)
        {
            var stream = streamHolder.Stream;
            _streamProvider.ReleaseStream(stream);
            var contentType = streamHolder
                .StreamGroup
                .ContentType;
            _streamHolders.Remove(contentType);
            streamHolder.Dispose();
        }

        private async Task StopStreaming()
        {
            Log.Info();
            _clock.Stop();
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            if (_streamHolders == null)
                return;
            await Task.WhenAll(
                _streamHolders.Values.Select(
                    holder => holder.StopLoadingChunks()));
            foreach (var streamHolder in _streamHolders.Values)
            {
                await streamHolder.StopPushingPackets();
                streamHolder.Flush();
            }
        }

        private async Task SeekPlayer(TimeSpan position)
        {
            var synchronizationContext = SynchronizationContext.Current;
            var cancellationToken = _cancellationTokenSource.Token;
            await _platformPlayer.SeekAsync(position, contentType =>
            {
                Log.Info($"{contentType}");
                var holder = _streamHolders[contentType];
                synchronizationContext.Post(_ =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        holder.StartPushingPackets(
                            _segment,
                            _platformPlayer);
                    }
                }, cancellationToken);
            }, cancellationToken);
        }

        private static void VerifyStreamGroups(StreamGroup[] streamGroups, IStreamSelector[] streamSelectors)
        {
            if (streamGroups == null)
                throw new ArgumentNullException(nameof(streamGroups));
            if (streamGroups.Length == 0)
                throw new ArgumentException($"{nameof(streamGroups)} argument is empty");
            var audioStreamGroupsCount = 0;
            var videoStreamGroupsCount = 0;
            var capabilities = Platform.Current.Capabilities;
            var supportsSeamlessAudioChange =
                capabilities.SupportsSeamlessAudioChange;

            for (var index = 0; index < streamGroups.Length; index++)
            {
                var streamGroup = streamGroups[index];
                var selector = streamSelectors[index];
                var contentType = streamGroup.ContentType;

                if (contentType == ContentType.Audio)
                {
                    ++audioStreamGroupsCount;

                    if (supportsSeamlessAudioChange)
                        continue;
                    if (selector != null && selector.GetType() == typeof(ThroughputHistoryStreamSelector))
                    {
                        throw new ArgumentException(
                            "Cannot select ThroughputHistoryStreamSelector for audio StreamGroup. " +
                            "Platform doesn't support it");
                    }
                }
                else if (contentType == ContentType.Video)
                {
                    ++videoStreamGroupsCount;
                }
                else
                {
                    throw new ArgumentException($"{contentType} is not supported");
                }
            }

            if (audioStreamGroupsCount > 1)
            {
                throw new ArgumentException(
                    $"{nameof(streamGroups)} contains more than 1 audio stream group. Allowed 0 or 1");
            }

            if (videoStreamGroupsCount > 1)
            {
                throw new ArgumentException(
                    $"{nameof(streamGroups)} contains more than 1 video stream group. Allowed 0 or 1");
            }
        }

        internal void SetStreamProvider(IStreamProvider streamProvider)
        {
            _streamProvider = streamProvider;
        }

        private class StreamHolder : IDisposable
        {
            private Task _loadChunksTask;
            private Task _pushPacketsTask;
            private readonly IDisposable _bufferingObserverSubscription;
            private readonly IDisposable _streamExceptionSubscription;

            public StreamHolder(
                IStream stream,
                StreamRenderer streamRenderer,
                StreamGroup streamGroup,
                BufferingObserver bufferingObserver,
                Action<bool> onStreamRendererBuffering,
                Action<Exception> onException)
            {
                Stream = stream;
                StreamRenderer = streamRenderer;
                StreamGroup = streamGroup;
                BufferingObserver = bufferingObserver;
                _bufferingObserverSubscription =
                    BufferingObserver
                        .OnBuffering()
                        .Subscribe(onStreamRendererBuffering);
                _streamExceptionSubscription =
                    Stream
                        .OnException()
                        .Subscribe(onException);
            }

            public IStream Stream { get; }
            public StreamRenderer StreamRenderer { get; }
            public StreamGroup StreamGroup { get; }
            public BufferingObserver BufferingObserver { get; }
            public IStreamSelector StreamSelector => Stream.StreamSelector;

            public void StartLoadingChunks(
                Segment segment,
                CancellationToken cancellationToken)
            {
                _loadChunksTask = Stream.LoadChunks(segment,
                    StreamRenderer,
                    cancellationToken);
            }

            public async Task StopLoadingChunks()
            {
                if (_loadChunksTask == null)
                    return;
                try
                {
                    await _loadChunksTask;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            public void StartPushingPackets(Segment segment,
                IPlatformPlayer platformPlayer)
            {
                Log.Info();
                if (!StreamRenderer.IsPushingPackets)
                {
                    _pushPacketsTask = StreamRenderer.StartPushingPackets(
                        segment,
                        platformPlayer);
                }
            }

            public async Task StopPushingPackets()
            {
                Log.Info();
                StreamRenderer.StopPushingPackets();

                if (_pushPacketsTask == null)
                    return;

                try
                {
                    await _pushPacketsTask;
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            public void Flush()
            {
                Log.Info();
                StreamRenderer.Flush();
            }

            public void Dispose()
            {
                _bufferingObserverSubscription?.Dispose();
                _streamExceptionSubscription?.Dispose();
                Stream?.Dispose();
                BufferingObserver?.Dispose();
            }
        }
    }
}