/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
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
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using ESPlayer = Tizen.TV.Multimedia;
using System.Threading.Tasks;
using Configuration;
using ElmSharp;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Utils;
using Nito.AsyncEx.Synchronous;
using System.Runtime.InteropServices;
using AsyncLock = Nito.AsyncEx.AsyncLock;
using PlayerState = JuvoPlayer.Common.PlayerState;

namespace JuvoPlayer.Player.EsPlayer
{
    /// <summary>
    /// Controls transfer stream operation
    /// </summary>
    internal sealed class EsStreamController : IDisposable
    {
        private class StateSnapshot
        {
            public TimeSpan Clock;
            public TimeSpan BufferDepth;
            public PlayerState State;
            public TaskCompletionSource<object> StateRestoredTcs;
        }

        private StateSnapshot _restorePoint;

        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        // Reference to all data streams representing transfer of individual
        // stream data and data storage
        private readonly EsStream[] esStreams;
        private readonly EsPlayerPacketStorage packetStorage;
        private readonly Synchronizer dataSynchronizer;

        // Reference to ESPlayer & associated window
        private ESPlayer.ESPlayer player;
        private readonly Window displayWindow;
        private readonly bool usesExternalWindow = true;

        // event callbacks
        private readonly Subject<string> playbackErrorSubject = new Subject<string>();
        private readonly Subject<int> bufferingProgressSubject = new Subject<int>();
        // Run state event through replayable subject. Upper layers may sub/unsubscribe
        // at will resulting in events being missed.
        private readonly ReplaySubject<PlayerState> stateChangedSubject =
            new ReplaySubject<PlayerState>(1);

        // Returns configuration status of all underlying streams.
        // True - all initialized streams are configures
        // False - at least one underlying stream is not configured
        private bool AllStreamsHaveConfiguration => esStreams.All(streamEntry =>
            streamEntry?.HaveConfiguration ?? true);

        public IPlayerClient Client { get; set; }

        // Termination & serialization objects for async operations.
        private CancellationTokenSource activeTaskCts = new CancellationTokenSource();
        private readonly AsyncLock asyncOpSerializer = new AsyncLock();
        private Task<bool> _asyncOpTask = Task.FromResult(false);

        private readonly IDisposable[] playbackErrorSubs;
        private IDisposable bufferingSub;
        private bool isDisposed;

        private readonly IScheduler _clockScheduler = new EventLoopScheduler();
        private readonly PlayerClockProvider _playerClock;
        private readonly DataClockProvider _dataClock;

        private readonly SynchronizationContext _syncCtx;

        private TaskCompletionSource<object> _playStateNotifier;
        private TaskCompletionSource<object> _configurationsCollected;


        #region Public API

        public void Initialize(StreamType stream)
        {
            logger.Info(stream.ToString());

            if (esStreams[(int)stream] != null)
            {
                throw new ArgumentException($"Stream {stream} already initialized");
            }

            dataSynchronizer.Initialize(stream);

            var esStream = new EsStream(stream, packetStorage, dataSynchronizer, _playerClock);
            esStream.SetPlayer(player);

            playbackErrorSubs[(int)stream] = esStream.PlaybackError()
                .Subscribe(OnEsStreamError, _syncCtx);

            esStreams[(int)stream] = esStream;

            _dataClock.Start();
        }

        public EsStreamController(EsPlayerPacketStorage storage)
            : this(storage,
                WindowUtils.CreateElmSharpWindow())
        {
            usesExternalWindow = false;
        }

        public EsStreamController(EsPlayerPacketStorage storage, Window window)
        {
            if (SynchronizationContext.Current == null)
                throw new ArgumentNullException(nameof(SynchronizationContext.Current));

            _syncCtx = SynchronizationContext.Current;

            // Create placeholder to data streams & chunk states
            esStreams = new EsStream[(int)StreamType.Count];
            playbackErrorSubs = new IDisposable[(int)StreamType.Count];

            _playerClock = new PlayerClockProvider(_clockScheduler);
            dataSynchronizer = new Synchronizer(_playerClock);
            _dataClock = new DataClockProvider(_clockScheduler, _playerClock);
            _dataClock.SetSynchronizerSource(dataSynchronizer.Pts());

            packetStorage = storage;
            displayWindow = window;

            player = new ESPlayer.ESPlayer();
            OpenPlayer();
        }

        private PlayerClockFn CreatePlayerClockFunction(ESPlayer.ESPlayer playerInstance)
        {
            TimeSpan Del()
            {
                try
                {
                    playerInstance.GetPlayingTime(out var currentClock);
                    return currentClock;
                }
                catch (Exception e)
                {
                    if (e is ObjectDisposedException || e is InvalidOperationException)
                        return PlayerClockProviderConfig.InvalidClock;

                    logger.Error(e);
                    throw;
                }
            }

            return Del;
        }

        private void OpenPlayer()
        {
            logger.Info("");
            player.Open();

            //The Tizen TV emulator is based on the x86 architecture. Using trust zone (DRM'ed content playback) is not supported by the emulator.
            if (RuntimeInformation.ProcessArchitecture != Architecture.X86) player.SetTrustZoneUse(true);

            player.SetDisplay(displayWindow);

            foreach (var stream in esStreams)
                stream?.SetPlayer(player);

            _playerClock.SetPlayerClockSource(CreatePlayerClockFunction(player));

            AttachEventHandlers();
        }

        private void ClosePlayer()
        {
            logger.Info("");

            DetachEventHandlers();

            _playerClock.SetPlayerClockSource(null);

            player.Stop();
            player.Close();
            player.Dispose();
            player = new ESPlayer.ESPlayer();

            foreach (var stream in esStreams)
                stream?.SetPlayer(player);
        }

        private void AttachEventHandlers()
        {
            player.EOSEmitted += OnEos;
            player.ErrorOccurred += OnESPlayerError;
            player.BufferStatusChanged += OnBufferStatusChanged;
            player.ResourceConflicted += OnResourceConflicted;
            logger.Info("Event handlers attached");
        }

        private void DetachEventHandlers()
        {

            player.EOSEmitted -= OnEos;
            player.ErrorOccurred -= OnESPlayerError;
            player.BufferStatusChanged -= OnBufferStatusChanged;
            player.ResourceConflicted -= OnResourceConflicted;
            logger.Info("Event handlers detached");
        }

        /// <summary>
        /// Sets provided configuration to appropriate stream.
        /// </summary>
        /// <param name="config">StreamConfig</param>
        public Task SetStreamConfiguration(StreamConfig config)
        {
            var streamType = config.StreamType();

            logger.Info($"{streamType}: {config.GetType()}");

            if (config is BufferStreamConfig metaData)
            {
                // Use video for buffer depth control.
                if (streamType == StreamType.Video)
                    _dataClock.BufferLimit = metaData.BufferDuration;

                return Task.CompletedTask;
            }

            if (esStreams[(int)streamType] == null)
            {
                logger.Warn($"Uninitialized stream {streamType}");
                return Task.CompletedTask;
            }

            if (esStreams[(int)streamType].HaveConfiguration)
            {
                if (!esStreams[(int)streamType].Configuration.IsCompatible(config))
                {
                    esStreams[(int)streamType].Configuration = config;
                    return Task.CompletedTask;
                }
                logger.Info($"{streamType}: Queuing configuration");
                return AppendPacket(BufferConfigurationPacket.Create(config));
            }

            if (_configurationsCollected == null)
            {
                esStreams[(int)streamType].SetStreamConfiguration(config);
            }
            else
            {
                esStreams[(int)streamType].Configuration = config;
                if (AllStreamsHaveConfiguration)
                    _configurationsCollected.TrySetResult(null);

                return Task.CompletedTask;
            }

            // Check if all initialized streams have configuration &
            // can be started
            if (!AllStreamsHaveConfiguration)
            {
                logger.Info($"Needed config: Video {esStreams[(int)StreamType.Video].Configuration == null} Audio {esStreams[(int)StreamType.Audio].Configuration == null}");
                return Task.CompletedTask;
            }

            var token = activeTaskCts.Token;
            return PreparePlayback(token);
        }

        /// <summary>
        /// Starts playback on all initialized streams. Streams do have to be
        /// configured in order for the call to start playback.
        /// </summary>
        public void Play()
        {
            if (!AllStreamsHaveConfiguration)
            {
                logger.Info($"Needed config: Video {esStreams[(int)StreamType.Video].Configuration == null} Audio {esStreams[(int)StreamType.Audio].Configuration == null}");
                return;
            }

            try
            {
                var token = activeTaskCts.Token;
                token.ThrowIfCancellationRequested();

                var state = player.GetState();
                logger.Info($"Player State: {state}");

                switch (state)
                {
                    case ESPlayer.ESPlayerState.Playing:
                        return;

                    case ESPlayer.ESPlayerState.Ready:
                        player.Start();
                        break;

                    case ESPlayer.ESPlayerState.Paused:
                        player.Resume();
                        _dataClock.Clock = _playerClock.LastClock;
                        ResumeTransfer(token);
                        break;

                    default:
                        throw new InvalidOperationException($"Play called in invalid state: {state}");
                }
                StartClockGenerator();
                SubscribeBufferingEvent();
                SetState(PlayerState.Playing, token);

            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation cancelled");
            }
        }

        /// <summary>
        /// Pauses playback on all initialized streams. Playback had to be played.
        /// </summary>
        public void Pause()
        {
            var currentState = player.GetState();
            logger.Info($"Player State: {currentState}");

            if (currentState == ESPlayer.ESPlayerState.Playing)
                PausePlayback();

            // Don't pass buffering events in paused state.
            UnsubscribeBufferingEvent();
        }

        /// <summary>
        /// Stops playback on all initialized streams.
        /// </summary>
        public void Stop()
        {
            var currentState = player.GetState();
            logger.Info($"Player State: {currentState}");

            if (currentState != ESPlayer.ESPlayerState.Paused && currentState != ESPlayer.ESPlayerState.Playing)
                return;

            try
            {
                StopTransfer();
                StopClockGenerator();
                player.Stop();
                SetState(PlayerState.Idle, CancellationToken.None);
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe);
            }
        }

        private Task GetPlayingStateCompletionTask(CancellationToken token)
        {
            var currentState = player.GetState();
            if (currentState == ESPlayer.ESPlayerState.Playing)
                return Task.CompletedTask;

            logger.Info($"Player state {currentState}. Creating Playing state notifier");

            // Wait for playback resume. 
            _playStateNotifier = new TaskCompletionSource<object>();
            return _playStateNotifier.Task.WithCancellation(token);
        }

        public async Task Seek(TimeSpan time)
        {
            logger.Info($"Seek to {time}");
            var token = activeTaskCts.Token;

            using (await asyncOpSerializer.LockAsync(token))
            {
                logger.Info($"Seeking to {time}");
                try
                {
                    token.ThrowIfCancellationRequested();
                    await GetPlayingStateCompletionTask(token);

                    // Don't cancel FlushStreams() or its internal operations. In case of cancellation
                    // stream controller will be in less then defined state.
                    await FlushStreams();

                    token.ThrowIfCancellationRequested();

                    var seekToTime = await Client.Seek(time, token);

                    EnableInput();
                    _playerClock.PendingClock = seekToTime;
                    _dataClock.Clock = time;
                    _dataClock.Start();

                    await ExecuteSeek(seekToTime, token);
                }
                catch (SeekException e)
                {
                    var msg = $"Seeking to {time} Failed, reason \"{e.Message}\"";
                    logger.Error(msg);
                    playbackErrorSubject.OnNext(msg);
                    throw;
                }
                catch (OperationCanceledException)
                {
                    logger.Info($"Seeking to {time} Cancelled");
                    throw;
                }
                catch (Exception e)
                {
                    logger.Error(e);
                    playbackErrorSubject.OnNext($"Seeking to {time} Failed");
                    throw;
                }
            }
        }

        private async Task ExecuteSeek(TimeSpan time, CancellationToken token)
        {
            dataSynchronizer.Prepare();

            logger.Info($"Player.SeekAsync({time})");

            var (needData, asyncHandler) = PrepareStreamStart(ESPlayer.StreamType.Audio, ESPlayer.StreamType.Video);
            var seekTask = player.SeekAsync(time, (s, _) => asyncHandler(s));

            logger.Info($"Player.SeekAsync({time}) Waiting for ready to seek");
            await needData.WithCancellation(token);

            logger.Info($"Player.SeekAsync({time}) Starting transfer");
            _asyncOpTask = StartTransfer(token);
            var startOk = await _asyncOpTask.WithCancellation(token);
            if (!startOk)
            {
                logger.Info($"Player.SeekAsync({time}) EOS");
                return;
            }

            logger.Info($"Player.SeekAsync({time}) Waiting for seek completion");
            await seekTask.WithCancellation(token);

            logger.Info($"Player.SeekAsync({time}) Done");
            StartClockGenerator();
            SubscribeBufferingEvent();
        }

        private async Task FlushStreams()
        {
            logger.Info("");

            // Stop data streams. They will be restarted from SeekAsync handler.
            StopClockGenerator();
            DisableInput();
            StopTransfer();
            UnsubscribeBufferingEvent();

            // Make sure data transfer is stopped!
            // SeekAsync behaves unpredictably when data transfer to player is occuring while SeekAsync gets called
            // Ignore token. Emptying streams cannot be done while streams are running.
            await Task.Run(action: WaitForAsyncOperationsCompletion);
            EmptyStreams();
        }

        public async Task ChangeRepresentation(object representation)
        {
            logger.Info("");
            var token = activeTaskCts.Token;
            using (await asyncOpSerializer.LockAsync(token))
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    // Wait for player to be in play state
                    await GetPlayingStateCompletionTask(token);

                    var clock = _playerClock.PendingOrLastClock;
                    if (clock == PlayerClockProviderConfig.InvalidClock)
                        clock = TimeSpan.Zero;

                    await FlushStreams();

                    var currentAudioConfig = esStreams[(int)StreamType.Audio].Configuration;
                    var currentVideoConfig = esStreams[(int)StreamType.Video].Configuration;
                    esStreams[(int)StreamType.Audio].Configuration = null;
                    esStreams[(int)StreamType.Video].Configuration = null;

                    _configurationsCollected = new TaskCompletionSource<object>();

                    var repositionedClock = await Client.ChangeRepresentation(clock, representation, token);

                    EnableInput();
                    _playerClock.PendingClock = repositionedClock;
                    _dataClock.Clock = clock;
                    _dataClock.Start();

                    logger.Info($"Representation changed. Current time {clock} New time {repositionedClock}");

                    await _configurationsCollected.Task.WithCancellation(token);
                    _configurationsCollected = null;

                    var changeTask = currentAudioConfig.IsCompatible(esStreams[(int)StreamType.Audio].Configuration) &&
                                     currentVideoConfig.IsCompatible(esStreams[(int)StreamType.Video].Configuration)
                        ? ExecuteSeek(clock, token)
                        : ChangeConfiguration(token);

                    await changeTask;
                }
                finally
                {
                    _configurationsCollected = null;
                    _playStateNotifier = null;
                }
            }
        }

        private Task ChangeConfiguration(CancellationToken token)
        {
            logger.Info("");

            var stateSnapshot = GetStateSnapshot();

            // TODO: Access to stream controller should be "blocked" in an async way while
            // TODO: player is restarted. Hell will break loose otherwise.
            SetState(PlayerState.Idle, CancellationToken.None);

            ClosePlayer();
            OpenPlayer();

            return RestoreStateSnapshot(stateSnapshot, token);
        }

        public async Task AppendPacket(Packet packet)
        {
            try
            {
                await packetStorage.AddPacket(packet);
            }
            catch (Exception e)
            when (e is ObjectDisposedException || e is InvalidOperationException)
            {
                logger.Warn($"{packet.StreamType}: Packet storage stopped/disposed {packet.Dts}");
                packet.Dispose();
            }
            catch (Exception e)
            {
                logger.Error($"{packet.StreamType}: {e}");
                throw;
            }
        }

        public void Suspend()
        {
            logger.Info("");

            if (activeTaskCts.IsCancellationRequested)
                return;

            activeTaskCts.Cancel();
            _restorePoint = GetStateSnapshot();

            StopTransfer();
            StopClockGenerator();
            player.Stop();
            ClosePlayer();

            SetState(PlayerState.Idle, CancellationToken.None);

            logger.Info($"Suspended State/Clock: {_restorePoint.State}/{_restorePoint.Clock}");
        }

        public async Task Resume()
        {
            if (!activeTaskCts.IsCancellationRequested)
                return;

            if (_restorePoint == null)
            {
                logger.Info("Restore point not found");
                return;
            }

            activeTaskCts = new CancellationTokenSource();
            var token = activeTaskCts.Token;

            logger.Info($"Resuming State/Clock {_restorePoint.State}/{_restorePoint.Clock}");

            var prepareTask = PreparePlayback(token);
            await Task.WhenAll(prepareTask, _restorePoint.StateRestoredTcs.Task).WithCancellation(token);

            logger.Info($"Resuming State/Clock {_restorePoint.State}/{_restorePoint.Clock} done");
            if (player.GetState() == ESPlayer.ESPlayerState.Playing)
                SubscribeBufferingEvent();
        }
        #endregion

        #region Private Methods

        #region Internal EsPlayer event handlers

        #endregion

        #region ESPlayer event handlers

        private void OnBufferStatusChanged(object sender, ESPlayer.BufferStatusEventArgs buffArgs)
        {
            logger.Info($"{buffArgs.StreamType.JuvoStreamType()} {buffArgs.BufferStatus}");
        }

        /// <summary>
        /// ESPlayer event handler. Notifies that ALL played streams have
        /// completed playback (EOS was sent on all of them)
        /// Methods
        /// </summary>
        /// <param name="sender">Object</param>
        /// <param name="eosArgs">ESPlayer.EosArgs</param>
        private void OnEos(object sender, ESPlayer.EOSEventArgs eosArgs)
        {
            logger.Info(eosArgs.ToString());

            stateChangedSubject.OnCompleted();
        }

        /// <summary>
        /// ESPlayer event handler. Notifies of an error condition during
        /// playback.
        /// Stops and disables all initialized streams and notifies of an error condition
        /// through PlaybackError event.
        /// </summary>
        /// <param name="sender">Object</param>
        /// <param name="errorArgs">ESPlayer.ErrorArgs</param>
        private void OnESPlayerError(object sender, ESPlayer.ErrorEventArgs errorArgs)
        {
            var error = errorArgs.ErrorType.ToString();

            logger.Error(error);

            playbackErrorSubject.OnNext(error);

        }

        private void OnResourceConflicted(object sender, ESPlayer.ResourceConflictEventArgs e)
        {
            logger.Info("");
        }

        private void OnEsStreamError(string error)
        {
            logger.Error(error);

            // Stop and disable all initialized data streams.
            StopTransfer();
            DisableInput();

            // Perform error notification
            playbackErrorSubject.OnNext(error);
        }

        public IObservable<string> ErrorOccured()
        {
            return playbackErrorSubject.AsObservable();
        }

        #endregion

        private void SubscribeBufferingEvent()
        {
            // It is expected, upon subscription, handler will be provided with current
            // buffering state.
            bufferingSub = esStreams[(int)StreamType.Video].StreamBuffering()
                .CombineLatest(
                    esStreams[(int)StreamType.Audio].StreamBuffering(), (v, a) => v | a)
                .Subscribe(OnStreamBuffering, _syncCtx);
        }

        private void UnsubscribeBufferingEvent()
        {
            bufferingSub?.Dispose();
            bufferingSub = null;

            bufferingProgressSubject.OnNext(100);
            logger.Info("");
        }

        private void OnStreamBuffering(bool isBuffering)
        {
            var currentState = player.GetState();

            switch (currentState)
            {
                case ESPlayer.ESPlayerState.Playing when isBuffering:
                    logger.Info($"State: {currentState} => Pausing");
                    player.Pause();
                    break;
                case ESPlayer.ESPlayerState.Paused when !isBuffering:
                    logger.Info($"State: {currentState} => Resuming");
                    player.Resume();
                    break;
                default:
                    return;
            }

            bufferingProgressSubject.OnNext(isBuffering ? 0 : 100);
        }

        private void SetState(PlayerState newState, CancellationToken token)
        {
            logger.Info(newState.ToString());

            if (_restorePoint != null)
            {
                ProcessStateSnapshot(newState, token);
                return;
            }
            if (token.IsCancellationRequested)
            {
                logger.Info($"Cancelled. Event {newState} not dispatched");
                throw new OperationCanceledException();
            }

            stateChangedSubject.OnNext(newState);
            OnStateChanged(newState);
        }

        private void ProcessStateSnapshot(PlayerState newState, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                logger.Info($"Restore to {_restorePoint.State} state cancelled");
                _restorePoint.StateRestoredTcs.TrySetCanceled();
                _restorePoint = null;
                return;
            }

            if (newState != _restorePoint.State)
            {
                logger.Info($"State {newState} not set. Restore to {_restorePoint.State} state");
                switch (newState)
                {
                    case PlayerState.Prepared:
                        Play();
                        break;

                    case PlayerState.Playing when _restorePoint.State == PlayerState.Paused:
                        Pause();
                        break;
                }

                return;
            }

            _restorePoint.StateRestoredTcs.TrySetResult(null);
            _restorePoint = null;
            logger.Info($"Restore state {newState} reached");
            SetState(newState, token);
        }

        private void OnStateChanged(PlayerState currentState)
        {
            if (currentState == PlayerState.Playing)
            {
                if (_playStateNotifier == null)
                    return;

                logger.Info($"Notifying {_playStateNotifier?.Task.Status}");
                _playStateNotifier?.TrySetResult(null);
            }
        }
        /// <summary>
        /// Method executes PrepareAsync on ESPlayer.
        /// </summary>
        private async Task PreparePlayback(CancellationToken token)
        {
            logger.Info("");

            try
            {
                using (await asyncOpSerializer.LockAsync(token))
                {
                    token.ThrowIfCancellationRequested();
                    await ExecutePreparePlayback(token);
                }

            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe);
                playbackErrorSubject.OnNext(ioe.Message);
            }
            catch (Exception e)
                when (e is TaskCanceledException || e is OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
                StopTransfer();
            }
            catch (Exception e)
            {
                logger.Error(e);
                playbackErrorSubject.OnNext("Start Failed");
                throw;
            }
        }

        private async Task ExecutePreparePlayback(CancellationToken token)
        {
            dataSynchronizer.Prepare();
            _dataClock.Start();

            logger.Info("Player.PrepareAsync()");

            var (needData, asyncHandler) = PrepareStreamStart(ESPlayer.StreamType.Audio, ESPlayer.StreamType.Video);
            var prepareTask = player.PrepareAsync(asyncHandler);

            logger.Info("Player.PrepareAsync() Waiting for ready to prepare");
            await needData.WithCancellation(token);

            logger.Info("Player.PrepareAsync() Starting transfer");
            _asyncOpTask = StartTransfer(token);
            var startOk = await _asyncOpTask.WithCancellation(token);
            if (!startOk)
            {
                logger.Info("Player.PrepareAsync() EOS");
                return;
            }

            logger.Info("Player.PrepareAsync() Waiting for completion");
            await prepareTask.WithCancellation(token);

            logger.Info("Player.PrepareAsync() Done");
            SetState(PlayerState.Prepared, token);
        }


        private (Task needData, Action<ESPlayer.StreamType> asyncHandler) PrepareStreamStart(params ESPlayer.StreamType[] streams)
        {
            var needDataTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            var readyState = new bool[streams.Length];
            var handler = new Action<ESPlayer.StreamType>(stream =>
            {
                var streamIdx = Array.IndexOf(streams, stream);
                if (streamIdx == -1)
                    return;

                readyState[streamIdx] = true;
                logger.Info($"{stream}: Ready for data");
                if (Array.TrueForAll(readyState, streamReady => streamReady))
                    needDataTcs.TrySetResult(null);
            });

            return (needDataTcs.Task, handler);
        }

        private void PausePlayback()
        {
            StopTransfer();
            StopClockGenerator();
            player.Pause();

            SetState(PlayerState.Paused, activeTaskCts.Token);
            logger.Info("Playback Paused");
        }

        private void SetPlayerConfiguration()
        {
            logger.Info("");

            foreach (var esStream in esStreams)
            {
                if (esStream?.Configuration != null)
                    esStream.SetStreamConfiguration();
            }
        }

        /// <summary>
        /// Stops all initialized data streams preventing transfer of data from associated
        /// data queue to underlying player. When stopped, stream can still accept new data
        /// </summary>
        private void StopTransfer()
        {
            logger.Info("Stopping all data streams");

            foreach (var esStream in esStreams)
                esStream?.Stop();
        }

        private void ResumeTransfer(CancellationToken token)
        {
            logger.Info("Resuming all data streams");

            foreach (var esStream in esStreams)
                esStream?.Start(token);
        }

        private void EmptyStreams()
        {
            foreach (var stream in esStreams)
                stream?.EmptyStorage();
        }

        private void EnableInput()
        {
            foreach (var stream in esStreams)
                stream?.EnableInput();
        }

        /// <summary>
        /// Disables all initialized data streams preventing
        /// any further new input collection
        /// </summary>
        private void DisableInput()
        {
            foreach (var esStream in esStreams)
                esStream?.DisableInput();
        }


        private async Task<bool> StartTransfer(CancellationToken token)
        {
            var firstPacketTask = esStreams[(int)StreamType.Video].PacketProcessed()
                .FirstAsync(pt => pt != typeof(BufferConfigurationPacket))
                .ToTask(token);

            esStreams[(int)StreamType.Video].Start(token);
            logger.Info($"{StreamType.Audio}: Waiting for first video packet");

            try
            {
                var firstPacket = await firstPacketTask;

                logger.Info($"{StreamType.Audio}: First video packet {firstPacket}");
                if (firstPacket == typeof(EOSPacket))
                {
                    player.SubmitEosPacket(ESPlayer.StreamType.Audio);
                    return false;
                }

                esStreams[(int)StreamType.Audio].Start(token);
                return true;
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation cancelled");
                player.SubmitEosPacket(ESPlayer.StreamType.Audio);
                player.SubmitEosPacket(ESPlayer.StreamType.Video);
                throw;
            }
            catch (Exception e)
            {
                logger.Error(e, "Operation failed");
                throw;
            }
        }

        /// <summary>
        /// Starts clock generation task
        /// </summary>
        private void StartClockGenerator()
        {
            logger.Info("");
            _dataClock.Start();
            _playerClock.Start();
        }

        /// <summary>
        /// Terminates clock generation task
        /// </summary>
        private void StopClockGenerator()
        {
            logger.Info("");
            _dataClock.Stop();
            _playerClock.Stop();
        }

        private StateSnapshot GetStateSnapshot()
        {
            PlayerState ps;
            switch (player.GetState())
            {
                case ESPlayer.ESPlayerState.Ready:
                    ps = PlayerState.Prepared;
                    break;
                case ESPlayer.ESPlayerState.Paused:
                    ps = PlayerState.Paused;
                    break;
                case ESPlayer.ESPlayerState.Playing:
                    ps = PlayerState.Playing;
                    break;
                default:
                    ps = PlayerState.Idle;
                    break;
            }

            var res = new StateSnapshot
            {
                Clock = _playerClock.PendingOrLastClock,
                BufferDepth = _dataClock.BufferLimit,
                State = ps,
                StateRestoredTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously)
            };

            logger.Info($"State snapshot. State {res.State} Clock {res.Clock} Buffer Depth {res.BufferDepth}");

            return res;
        }

        private async Task RestoreStateSnapshot(StateSnapshot stateSnapshot, CancellationToken token)
        {
            _restorePoint = stateSnapshot;

            logger.Info($"Restoring snapshot. State {_restorePoint.State} Clock {_restorePoint.Clock} Buffer Depth {_restorePoint.BufferDepth}");

            SetPlayerConfiguration();

            _dataClock.Clock = _restorePoint.Clock == PlayerClockProviderConfig.InvalidClock
                ? TimeSpan.Zero : _restorePoint.Clock;
            _dataClock.BufferLimit = _restorePoint.BufferDepth;

            // don't wait for restore point completion if prepare async fails.
            var restoreTask = _restorePoint.StateRestoredTcs.Task;
            await ExecutePreparePlayback(token);
            await restoreTask.WithCancellation(token);
        }
        #endregion

        #region Dispose support

        private void TerminateAsyncOperations()
        {
            // Stop clock & async operations
            logger.Info("");

            StopClockGenerator();
            StopTransfer();
            DisableInput();

            activeTaskCts.Cancel();

            _restorePoint?.StateRestoredTcs.TrySetCanceled();
            _playStateNotifier?.TrySetCanceled();

            logger.Info("Clock/AsyncOps shutdown");
        }

        private void WaitForAsyncOperationsCompletion()
        {
            var terminations = esStreams.Where(esStream => esStream != null)
                .Select(esStream => esStream.GetActiveTask())
                .Append(_asyncOpTask)
                .ToArray();

            logger.Info($"Waiting for {terminations.Length} operations to complete");
            Task.WhenAll(terminations).WaitWithoutException();
            logger.Info("Done");
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            logger.Info("");

            TerminateAsyncOperations();
            WaitForAsyncOperationsCompletion();

            logger.Info("Stopping playback");
            try
            {
                player.Stop();
            }
            catch (Exception e)
            {
                if (!(e is InvalidOperationException))
                    logger.Error(e);
                // Ignore. Will be raised if not playing :)
            }

            DisposeAllSubscriptions();
            DetachEventHandlers();
            dataSynchronizer.Dispose();
            DisposeStreams();
            DisposeAllSubjects();

            logger.Info("Disposing clock sources");
            // data clock uses player clock. Dispose data clock first.
            _dataClock.Dispose();
            _playerClock.Dispose();

            // Shut down player
            logger.Info("Disposing ESPlayer");
            // Don't call Close. Dispose does that. Otherwise exceptions will fly
            player.Dispose();
            if (usesExternalWindow == false)
                WindowUtils.DestroyElmSharpWindow(displayWindow);

            logger.Info("Disposing Tokens");
            // Clean up internal object
            activeTaskCts.Dispose();

            isDisposed = true;
        }

        private void DisposeStreams()
        {
            // Dispose of individual streams.
            logger.Info("Disposing Data Streams");
            foreach (var esStream in esStreams)
                esStream?.Dispose();
        }

        private void DisposeAllSubjects()
        {
            playbackErrorSubject.Dispose();
            stateChangedSubject.OnCompleted();
            stateChangedSubject.Dispose();
            bufferingProgressSubject.OnCompleted();
            bufferingProgressSubject.Dispose();
        }

        private void DisposeAllSubscriptions()
        {
            foreach (var playbackErrorSub in playbackErrorSubs)
                playbackErrorSub?.Dispose();

            bufferingSub?.Dispose();
        }

        #endregion

        public IObservable<PlayerState> StateChanged()
        {
            return stateChangedSubject.AsObservable();
        }

        public IObservable<int> BufferingProgress()
        {
            return bufferingProgressSubject.AsObservable();
        }

        public IObservable<TimeSpan> DataNeededStateChanged()
        {
            return _dataClock.DataClock();
        }

        public IObservable<TimeSpan> PlayerClock()
        {
            return _playerClock.PlayerClockObservable();
        }
    }
}
