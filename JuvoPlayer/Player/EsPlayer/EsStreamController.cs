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
            public PlayerState State;
            public TimeSpan Position;
        }

        private StateSnapshot _suspendState;

        private static readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

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

        private readonly IDisposable[] playbackErrorSubs;
        private IDisposable bufferingSub;
        private bool isDisposed;

        private readonly IScheduler _clockScheduler = new EventLoopScheduler();
        private readonly PlayerClockProvider _playerClock;
        private readonly DataClockProvider _dataClock;

        private readonly SynchronizationContext _syncCtx;

        private TaskCompletionSource<object> _configurationsCollected;
        private TimeSpan? _pendingPosition;

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
            _dataClock.SynchronizerClock = dataSynchronizer.Pts();

            packetStorage = storage;
            displayWindow = window;

            try
            {
                player = new ESPlayer.ESPlayer();
                OpenPlayer();
            }
            catch (Exception e)
            {
                logger.Error(e);
            }
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

            return PreparePlayback(activeTaskCts.Token);
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
                logger.Info("Operation canceled");
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

        private async ValueTask WaitForPlayingState(CancellationToken token)
        {
            var currentState = player.GetState();
            logger.Info($"Player state: {currentState}");
            if (currentState == ESPlayer.ESPlayerState.Playing)
            {
                token.ThrowIfCancellationRequested();
                return;
            }

            logger.Info($"Waiting for {ESPlayer.ESPlayerState.Playing}");
            var currentPlayer = player;
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
            } while (currentPlayer.GetState() != ESPlayer.ESPlayerState.Playing);

            logger.Info($"{ESPlayer.ESPlayerState.Playing} observed");
        }

        public async Task Seek(TimeSpan time)
        {
            logger.Info($"Seek to {time}");
            var token = activeTaskCts.Token;

            using (await asyncOpSerializer.LockAsync(token))
            {
                _pendingPosition = time;
                logger.Info($"Seeking to {_pendingPosition}");

                try
                {
                    // Abandon at this stage = clock/buffers remain unchanged.
                    await WaitForPlayingState(token);

                    // Don't cancel FlushStreams() or its internal operations. In case of cancellation
                    // stream controller will be in less then defined state.
                    await FlushStreams();

                    // DashDataProvider - does not care about cancellation token.
                    // HLSDataProvider - when cancelled, effectively terminates demuxer operation.
                    // - Playback termination - no issue.
                    // - Suspend - no way to resume demuxer other then restarting entire stream playback.
                    var seekToTime = await Client.Seek(time, CancellationToken.None);
                    _dataClock.Clock = seekToTime;

                    EnableInput();

                    var isCompleted = await ExecuteSeek(seekToTime, token);

                    _pendingPosition = null;
                    if (isCompleted)
                        SubscribeBufferingEvent();

                    return;
                }
                catch (SeekException e)
                {
                    var msg = $"Seeking to {time} Failed, reason \"{e.Message}\"";
                    logger.Error(msg);
                    playbackErrorSubject.OnNext(msg);

                    // Propagate subject content.
                    await Task.Yield();
                }
                catch (Exception e)
                when (e is TaskCanceledException || e is OperationCanceledException)
                {
                    logger.Info($"Seeking to {time} Cancelled");
                }
                catch (Exception e)
                {
                    logger.Error(e);
                    playbackErrorSubject.OnNext($"Seeking to {time} Failed");
                    throw;
                }

                // SeekException or Cancellation occurred.
                player.SubmitEosPacket(ESPlayer.StreamType.Audio);
                player.SubmitEosPacket(ESPlayer.StreamType.Video);
                logger.Info("EOS Sent");
            }
        }

        private async Task<bool> ExecuteSeek(TimeSpan time, CancellationToken token)
        {
            dataSynchronizer.Prepare();
            _dataClock.Start();

            var (needDataTcs, asyncHandler) = PrepareStreamStart(ESPlayer.StreamType.Audio, ESPlayer.StreamType.Video);

            using (token.Register(TerminateStreamStart, needDataTcs))
            {

                logger.Info($"Player.SeekAsync({time})");
                var seekTask = player.SeekAsync(time, (s, _) => asyncHandler(s));

                logger.Info($"Player.SeekAsync({time}) Waiting for ready to seek");
                await needDataTcs.Task;

                logger.Info($"Player.SeekAsync({time}) Starting transfer");
                if (false == await StartTransfer(token))
                {
                    _dataClock.Stop();
                    return false;
                }

                logger.Info($"Player.SeekAsync({time}) Waiting for seek completion");
                await seekTask.WithCancellation(token);

                logger.Info($"Player.SeekAsync({time}) Done");
                StartClockGenerator();
            }

            return true;
        }

        private async Task FlushStreams()
        {
            logger.Info("");

            StopClockGenerator();
            // Stop data streams. They will be restarted from SeekAsync handler.
            DisableInput();
            StopTransfer();
            UnsubscribeBufferingEvent();

            // Make sure data transfer is stopped!
            // SeekAsync behaves unpredictably when data transfer to player is occurring while SeekAsync gets called
            // Ignore token. Emptying streams cannot be done while streams are running.
            await AsyncOperationCompletions();
            EmptyStreams();
        }

        public async Task ChangeRepresentation(object representation)
        {
            logger.Info("");
            var token = activeTaskCts.Token;
            using (await asyncOpSerializer.LockAsync(token))
            {
                logger.Info("Changing representation");
                TimeSpan clock = TimeSpan.Zero;
                try
                {
                    // Wait for player to be in play state
                    await WaitForPlayingState(token);

                    clock = _pendingPosition ?? _playerClock.Clock;
                    _pendingPosition = null;

                    await FlushStreams();

                    var currentAudioConfig = esStreams[(int)StreamType.Audio].Configuration;
                    var currentVideoConfig = esStreams[(int)StreamType.Video].Configuration;
                    esStreams[(int)StreamType.Audio].Configuration = null;
                    esStreams[(int)StreamType.Video].Configuration = null;

                    _configurationsCollected = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

                    var repositionedClock = await Client.ChangeRepresentation(clock, representation, token);

                    EnableInput();

                    _dataClock.Clock = clock;
                    _dataClock.Start();

                    logger.Info($"Representation changed. Current time {clock} New time {repositionedClock}");

                    await _configurationsCollected.Task.WithCancellation(token);
                    _configurationsCollected = null;

                    if (currentAudioConfig.IsCompatible(esStreams[(int)StreamType.Audio].Configuration) &&
                       currentVideoConfig.IsCompatible(esStreams[(int)StreamType.Video].Configuration))
                    {
                        if (false == await ExecuteSeek(clock, token))
                            return;
                    }
                    else
                    {
                        await ChangeConfiguration(token);
                    }

                    StartClockGenerator();
                    SubscribeBufferingEvent();
                    _configurationsCollected = null;
                    logger.Info("Completed");
                    return;
                }
                catch (SeekException e)
                {
                    var msg = $"ChangeRepresentation seek to {clock} Failed, reason \"{e.Message}\"";
                    logger.Error(msg);
                    playbackErrorSubject.OnNext(msg);

                    // Propagate subject content.
                    await Task.Yield();
                }
                catch (Exception ce)
                when (ce is OperationCanceledException || ce is TaskCanceledException)
                {
                    logger.Info($"ChangeRepresentation cancelled");
                }
                catch (Exception e)
                {
                    logger.Error(e);
                    playbackErrorSubject.OnNext($"ChangeRepresentation failed");
                    throw;
                }

                player.SubmitEosPacket(ESPlayer.StreamType.Audio);
                player.SubmitEosPacket(ESPlayer.StreamType.Video);
            }
        }

        private Task ChangeConfiguration(CancellationToken token)
        {
            logger.Info("");

            var stateSnapshot = GetSuspendState();

            // TODO: Access to stream controller should be "blocked" in an async way while
            // TODO: player is restarted. Hell will break loose otherwise.
            SetState(PlayerState.Idle, CancellationToken.None);

            ClosePlayer();
            player.Dispose();
            player = new ESPlayer.ESPlayer();
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
            logger.Info(string.Empty);

            if (activeTaskCts.IsCancellationRequested)
                return;

            // Detach event handlers. If Suspend is in *Async() operation, will prevent
            // PlayerState observable termination.
            DetachEventHandlers();
            UnsubscribeBufferingEvent();

            logger.Info("Cancelling async operations");
            activeTaskCts.Cancel();

            StopTransfer();
            _suspendState = GetSuspendState();
            StopClockGenerator();

            // Not waiting for async completions during suspend.
            // Are awaited in resume to cut down suspend time.
            SetState(PlayerState.Idle, CancellationToken.None);
            logger.Info($"Suspended in {_suspendState}");
        }

        public async Task Resume()
        {
            try
            {
                if (_suspendState == null)
                {
                    logger.Info("Restore state not found");
                    return;
                }

                var restoreState = _suspendState;
                _suspendState = null;

                activeTaskCts.Dispose();
                activeTaskCts = new CancellationTokenSource();

                logger.Info("Waiting async Op completion");
                await AsyncOperationCompletions().WithoutException(logger);
                using (await asyncOpSerializer.LockAsync(activeTaskCts.Token))
                {
                    /*Acquire lock prior to proceeding. Async ops may still be terminating.
                    * Not applicable to real life scenarios. Applicable to integration tests*/
                }
                logger.Info("Async Ops completed");

                ClosePlayer();
                player.Dispose();
                player = new ESPlayer.ESPlayer();

                OpenPlayer();

                await RestoreStateSnapshot(restoreState, activeTaskCts.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            when (!(e is OperationCanceledException))
            {
                logger.Error(e);
            }
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

            logger.Info("");
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
                    logger.Info($"State: {currentState} => Player pause");
                    player.Pause();
                    break;
                case ESPlayer.ESPlayerState.Paused when !isBuffering:
                    logger.Info($"State: {currentState} => Player resume");
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

            if (token.IsCancellationRequested)
            {
                logger.Info($"Cancelled. Event {newState} not dispatched");
                throw new OperationCanceledException();
            }

            stateChangedSubject.OnNext(newState);
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
                    if (false == await ExecutePreparePlayback(token))
                        return;

                    SetState(PlayerState.Prepared, token);
                    return;
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

        private async Task<bool> ExecutePreparePlayback(CancellationToken token)
        {
            dataSynchronizer.Prepare();
            _dataClock.Start();
            var (needDataTcs, asyncHandler) = PrepareStreamStart(ESPlayer.StreamType.Audio, ESPlayer.StreamType.Video);

            using (token.Register(TerminateStreamStart, needDataTcs))
            {
                logger.Info("Player.PrepareAsync()");
                var prepareTask = player.PrepareAsync(asyncHandler);

                logger.Info("Player.PrepareAsync() Waiting for ready to prepare");
                await needDataTcs.Task;

                logger.Info("Player.PrepareAsync() Starting transfer");
                if (false == await StartTransfer(token))
                {
                    _dataClock.Stop();
                    return false;
                }

                logger.Info("Player.PrepareAsync() Waiting for completion");
                await prepareTask.WithCancellation(token);

                logger.Info("Player.PrepareAsync() Done");
            }

            return true;
        }


        private (TaskCompletionSource<object> needDataTcs, Action<ESPlayer.StreamType> asyncHandler) PrepareStreamStart(params ESPlayer.StreamType[] streams)
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

            return (needDataTcs, handler);
        }

        private static void TerminateStreamStart(object tcs)
        {
            var isCancelled = ((TaskCompletionSource<object>)tcs).TrySetCanceled();
            logger.Info($"Need data task cancelled: {isCancelled}");
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
            var packetTask = esStreams[(int)StreamType.Video].PacketProcessed()
                .FirstAsync(pt => pt != typeof(BufferConfigurationPacket))
                .ToTask(token);

            esStreams[(int)StreamType.Video].Start(token);
            logger.Info($"{StreamType.Audio}: Waiting for first video packet");

            try
            {
                // firstPacket - expected to get cancelled by token passed to ToTask()
                var firstPacket = await packetTask;

                logger.Info($"First video packet {firstPacket}");
                if (firstPacket == typeof(EOSPacket))
                {
                    player.SubmitEosPacket(ESPlayer.StreamType.Audio);
                    return false;
                }

                packetTask = esStreams[(int)StreamType.Audio].PacketProcessed()
                    .FirstAsync(pt => pt != typeof(BufferConfigurationPacket))
                    .ToTask(token);

                esStreams[(int)StreamType.Audio].Start(token);

                firstPacket = await packetTask;
                logger.Info($"First audio packet {firstPacket}");
                // Video without audio.. should play
                return true;
            }
            catch (Exception e)
            when (e is OperationCanceledException || e is InvalidOperationException)
            {
                /* Empty FirstAsync may raise InvalidOperationException if observable is empty or predicate has no match*/
                logger.Info("Operation cancelled");
                throw new OperationCanceledException();
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
            _dataClock.Start();
            _playerClock.Start();
            logger.Info("");
        }

        /// <summary>
        /// Terminates clock generation task
        /// </summary>
        private void StopClockGenerator()
        {
            _dataClock.Stop();
            _playerClock.Stop();
            logger.Info("");
        }

        private StateSnapshot GetSuspendState()
        {
            var suspendPoint = new StateSnapshot();

            switch (player.GetState())
            {
                case ESPlayer.ESPlayerState.Ready:
                    suspendPoint.State = PlayerState.Prepared;
                    break;
                case ESPlayer.ESPlayerState.Paused:
                    suspendPoint.State = PlayerState.Paused;
                    break;
                case ESPlayer.ESPlayerState.Playing:
                    suspendPoint.State = PlayerState.Playing;
                    break;
                default:
                    suspendPoint.State = PlayerState.Idle;
                    break;
            }

            suspendPoint.Position = _pendingPosition ?? _playerClock.Clock;
            _pendingPosition = null;
            logger.Info($"State snapshot. State/Position {suspendPoint.State}/{suspendPoint.Position}");

            return suspendPoint;
        }

        private async Task RestoreStateSnapshot(StateSnapshot restorePoint, CancellationToken token)
        {
            logger.Info($"Restoring to State/Position {restorePoint.State}/{restorePoint.Position}");

            // Push stream configurations, if any
            SetPlayerConfiguration();
            if (!AllStreamsHaveConfiguration)
            {
                logger.Info($"Not all streams have configuration");
                return;
            }

            // Try starting. Suspend in paused/idle is ignored. If configurations exist, start.
            _dataClock.Clock = restorePoint.Position;

            if (false == await ExecutePreparePlayback(token))
                return;

            Play();
            SubscribeBufferingEvent();
        }
        #endregion

        #region Dispose support

        private void TerminateAsyncOperations()
        {
            // Stop clock & async operations
            StopClockGenerator();
            StopTransfer();
            DisableInput();
            activeTaskCts.Cancel();
            logger.Info("Done");
        }

        private Task AsyncOperationCompletions() =>
            Task.WhenAll(
                esStreams.Where(esStream => esStream != null)
                .Select(esStream => esStream.GetActiveTask()));

        private void DisposeObjects()
        {
            logger.Info("");

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

            logger.Info("Done");
        }
        private void WaitForAsyncOperationsCompletion()
        {
            using (var terminationMre = new ManualResetEvent(false))
            {
                Task.Run(async () =>
                {
                    await AsyncOperationCompletions().WithoutException(logger);
                    terminationMre.Set();
                });

                WaitHandle.WaitAll(new WaitHandle[] { terminationMre });
            }

            logger.Info("Done");
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            logger.Info("");

            DetachEventHandlers();
            TerminateAsyncOperations();
            WaitForAsyncOperationsCompletion();
            DisposeAllSubscriptions();

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
            logger.Info("Player stopped");

            DisposeObjects();
            isDisposed = true;

            logger.Info("Done");
        }

        private void DisposeStreams()
        {
            // Dispose of individual streams.
            foreach (var esStream in esStreams)
                esStream?.Dispose();

            logger.Info("Done");
        }

        private void DisposeAllSubjects()
        {
            playbackErrorSubject.Dispose();
            stateChangedSubject.OnCompleted();
            stateChangedSubject.Dispose();
            bufferingProgressSubject.OnCompleted();
            bufferingProgressSubject.Dispose();

            logger.Info("Done");
        }

        private void DisposeAllSubscriptions()
        {
            foreach (var playbackErrorSub in playbackErrorSubs)
                playbackErrorSub?.Dispose();

            bufferingSub?.Dispose();

            logger.Info("Done");
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
            return _dataClock.DataClock().Do(dc => logger.Info($"DataClock: {dc}"));
        }

        public IObservable<TimeSpan> PlayerClock()
        {
            return _playerClock.PlayerClockObservable();
        }
    }
}
