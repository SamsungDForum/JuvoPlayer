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
using System.Collections.Generic;
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
            public PlayerState State;
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
        private readonly Subject<PlayerState> stateChangedSubject =
            new Subject<PlayerState>();
        private readonly Subject<int> bufferingProgressSubject = new Subject<int>();

        // Returns configuration status of all underlying streams.
        // True - all initialized streams are configures
        // False - at least one underlying stream is not configured
        private bool AllStreamsHaveConfiguration => esStreams.All(streamEntry =>
            streamEntry?.HaveConfiguration ?? true);

        public IPlayerClient Client { get; set; }

        // Termination & serialization objects for async operations.
        private CancellationTokenSource activeTaskCts = new CancellationTokenSource();
        private readonly AsyncLock asyncOpSerializer = new AsyncLock();

        private readonly IDisposable[] streamReconfigureSubs;
        private readonly IDisposable[] playbackErrorSubs;
        private IDisposable bufferingSub;
        private bool isDisposed;

        private readonly IScheduler _clockScheduler = new EventLoopScheduler();
        private readonly PlayerClockProvider _playerClock;
        private readonly DataClockProvider _dataClock;
        private TimeSpan _suspendClock;
        private ESPlayer.ESPlayerState _suspendState;

        private readonly SynchronizationContext _syncCtx;

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

            _dataClock.SetClock(_restorePoint?.Clock ?? TimeSpan.Zero, activeTaskCts.Token);
            _dataClock.Start();
        }

        public EsStreamController(EsPlayerPacketStorage storage, object restorePoint)
            : this(storage,
                WindowUtils.CreateElmSharpWindow(), restorePoint)
        {
            usesExternalWindow = false;
        }

        public EsStreamController(EsPlayerPacketStorage storage, Window window, object restorePoint)
        {
            if (SynchronizationContext.Current == null)
                throw new ArgumentNullException(nameof(SynchronizationContext.Current));

            // Null is a valid restore point. No restore
            _restorePoint = restorePoint as StateSnapshot;

            _syncCtx = SynchronizationContext.Current;

            _playerClock = new PlayerClockProvider(_clockScheduler);
            _dataClock = new DataClockProvider(_clockScheduler, _playerClock);

            // Create placeholder to data streams & chunk states
            esStreams = new EsStream[(int)StreamType.Count];
            streamReconfigureSubs = new IDisposable[(int)StreamType.Count];
            playbackErrorSubs = new IDisposable[(int)StreamType.Count];

            dataSynchronizer = new Synchronizer(_playerClock);

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
        }

        /// <summary>
        /// Sets provided configuration to appropriate stream.
        /// </summary>
        /// <param name="config">StreamConfig</param>
        public async Task SetStreamConfiguration(StreamConfig config)
        {
            var streamType = config.StreamType();

            logger.Info($"{streamType}: {config.GetType()}");

            try
            {
                if (config is BufferStreamConfig metaData)
                {
                    // Use video for buffer depth control.
                    if (streamType == StreamType.Video)
                        _dataClock.UpdateBufferDepth(metaData.BufferDuration);
                    return;
                }


                if (esStreams[(int)streamType].HaveConfiguration)
                {
                    logger.Info($"{streamType}: Queuing configuration");
                    await AppendPacket(BufferConfigurationPacket.Create(config));
                    return;
                }

                esStreams[(int)streamType].SetStreamConfiguration(config);


                // Check if all initialized streams have configuration &
                // can be started
                if (!AllStreamsHaveConfiguration || activeTaskCts.IsCancellationRequested)
                {
                    logger.Info($"Cannot start {esStreams[(int)StreamType.Video].Configuration == null} {esStreams[(int)StreamType.Audio].Configuration == null} {activeTaskCts.IsCancellationRequested}");
                    return;
                }

                await PreparePlayback(activeTaskCts.Token);
                SetState(PlayerState.Prepared);

            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation cancelled");
            }
            catch (NullReferenceException)
            {
                // packetQueue can hold ALL StreamTypes, but not all of them
                // have to be supported.
                logger.Warn($"Uninitialized Stream Type {streamType}");
            }
            catch (ObjectDisposedException)
            {
                logger.Info($"{streamType}: Operation Cancelled and disposed");
            }
            catch (InvalidOperationException)
            {
                // Queue has been marked as completed
                logger.Warn($"Data queue terminated for stream: {streamType}");
            }
            catch (UnsupportedStreamException use)
            {
                logger.Error(use, $"{streamType}");
                OnEsStreamError(use.Message);
            }
            catch (Exception e)
            {
                logger.Error(e, $"{streamType}");
                throw;
            }
        }

        /// <summary>
        /// Starts playback on all initialized streams. Streams do have to be
        /// configured in order for the call to start playback.
        /// </summary>
        public void Play()
        {
            if (!AllStreamsHaveConfiguration)
            {
                logger.Info("Initialized streams are not configured. Start will occur after receiving configurations");
                return;
            }

            try
            {
                var token = activeTaskCts.Token;
                token.ThrowIfCancellationRequested();

                var state = player.GetState();
                logger.Info($"{state}");

                switch (state)
                {
                    case ESPlayer.ESPlayerState.Playing:
                        return;

                    case ESPlayer.ESPlayerState.Ready:
                        player.Start();
                        break;

                    case ESPlayer.ESPlayerState.Paused:
                        StartClockGenerator();
                        player.Resume();
                        ResumeTransfer(token);
                        break;

                    default:
                        throw new InvalidOperationException($"Play called in invalid state: {state}");
                }

                SetState(PlayerState.Playing);
                SubscribeBufferingEvent();
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
            logger.Info($"Current State: {currentState}");

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
            logger.Info("");

            var state = player.GetState();
            if (state != ESPlayer.ESPlayerState.Paused && state != ESPlayer.ESPlayerState.Playing)
                return;

            try
            {
                StopTransfer();
                StopClockGenerator();

                player.Stop();
                SetState(PlayerState.Idle);
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe);
            }
        }

        public async Task Seek(TimeSpan time)
        {
            logger.Info(time.ToString());
            var token = activeTaskCts.Token;

            try
            {
                var resumeNeeded = player.GetState() == ESPlayer.ESPlayerState.Paused;
                if (resumeNeeded)
                {
                    await stateChangedSubject
                        .AsObservable()
                        .FirstAsync(state => state == PlayerState.Playing)
                        .ToTask(token);

                    token.ThrowIfCancellationRequested();
                }

                using (await asyncOpSerializer.LockAsync(token))
                {
                    // Set suspend clock. Clock is not available during seek, but required during Suspend/Resume
                    _suspendClock = time;

                    token.ThrowIfCancellationRequested();
                    await SeekStreamInitialize(token);

                    var seekToTime = await Client.Seek(time, token);

                    _dataClock.SetClock(time, token);
                    EnableInput();
                    _dataClock.Start();

                    await StreamSeek(seekToTime, resumeNeeded, token);

                    // Invalidate _suspendClock
                    _suspendClock = PlayerClockProviderConfig.InvalidClock;
                }
            }
            catch (SeekException e)
            {
                logger.Error(e);
                playbackErrorSubject.OnNext($"Seek Failed, reason \"{e.Message}\"");
                throw;
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
                // This should never be required... 
            }
            catch (Exception e)
            {
                logger.Error(e);
                playbackErrorSubject.OnNext("Seek Failed");
            }
        }

        public async Task AppendPacket(Packet packet)
        {
            try
            {
                //logger.Info($"{packet.StreamType} {packet.Pts}");
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

            // Cancel token. During async ops (Seek/Prepare), will send player to Davy Jones locker
            // and prevent event generation via SetState().
            activeTaskCts.Cancel();
            _suspendState = player.GetState();

            StopTransfer();
            StopClockGenerator();
            player.Stop();
            ClosePlayer();

            if (_suspendClock == PlayerClockProviderConfig.InvalidClock)
                _suspendClock = esStreams[(int)StreamType.Video].CurrentPts;

            // Token already cancelled. Set directly
            stateChangedSubject.OnNext(PlayerState.Paused);

            logger.Info($"Suspended State/Clock: {_suspendState}/{_suspendClock}");
        }

        public async Task Resume()
        {
            if (!activeTaskCts.IsCancellationRequested)
                return;

            activeTaskCts = new CancellationTokenSource();
            var token = activeTaskCts.Token;

            logger.Info($"Resuming State/Clock {_suspendState}/{_suspendClock}");

            // If suspend happened before or during PrepareAsync, suspend state will be Idle
            // There is no state "have configuration" based on which prepare operation would be invoked,
            // as such we need to get to Ready state when suspend occured in Idle
            var targetState = _suspendState == ESPlayer.ESPlayerState.Idle
                ? ESPlayer.ESPlayerState.Ready : _suspendState;

            var currentState = ESPlayer.ESPlayerState.None;

            // Loop through startup states till target state reached, performing start activities
            // corresponding to each start step.
            do
            {
                switch (currentState)
                {
                    // Open player
                    case ESPlayer.ESPlayerState.None:
                        OpenPlayer();
                        currentState = ESPlayer.ESPlayerState.Idle;
                        break;

                    // Prepare playback
                    case ESPlayer.ESPlayerState.Idle:

                        // Set'n'start clocks to suspend time.
                        _dataClock.SetClock(_suspendClock, token);
                        StartClockGenerator();

                        // Push current configuration (if available)
                        if (!AllStreamsHaveConfiguration)
                        {
                            logger.Info(
                                $"Have Configuration. Audio: {esStreams[(int)StreamType.Audio].HaveConfiguration}  Video: {esStreams[(int)StreamType.Video].HaveConfiguration}");
                            return;
                        }

                        SetPlayerConfiguration();
                        try
                        {
                            await PreparePlayback(token);
                        }
                        catch (OperationCanceledException)
                        {
                            logger.Info("Resume cancelled");
                            return;
                        }

                        currentState = ESPlayer.ESPlayerState.Ready;
                        break;

                    // Suspended in Pause. Start then pause
                    case ESPlayer.ESPlayerState.Ready:
                        player.Start();
                        currentState = ESPlayer.ESPlayerState.Playing;
                        break;

                    case ESPlayer.ESPlayerState.Playing:
                        player.Pause();
                        currentState = ESPlayer.ESPlayerState.Paused;
                        break;
                }

            } while (currentState != targetState && !token.IsCancellationRequested);

            // Push out target state.
            switch (currentState)
            {
                case ESPlayer.ESPlayerState.Ready:
                    SetState(PlayerState.Prepared);
                    break;
                case ESPlayer.ESPlayerState.Playing:
                    SetState(PlayerState.Playing);
                    SubscribeBufferingEvent();
                    break;
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
                .Replay(1)  // Replay current state. Subscription will invoke OnStreamBuffering
                            // with current buffering state.
                .RefCount()
                .Do(bs => logger.Info($"Buffering State: {bs}"))
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
            logger.Info($"State: {currentState} Buffering: {isBuffering}");

            switch (currentState)
            {
                case ESPlayer.ESPlayerState.Playing when isBuffering:
                    logger.Info("Pausing");
                    player.Pause();
                    break;
                case ESPlayer.ESPlayerState.Paused when !isBuffering:
                    logger.Info("Resuming");
                    player.Resume();
                    break;
                default:
                    return;
            }

            bufferingProgressSubject.OnNext(isBuffering ? 0 : 100);
        }

        private void SetState(PlayerState newState)
        {
            if (activeTaskCts.IsCancellationRequested)
                return;

            // null restore point will always result in condition being true.
            if (!(newState <= _restorePoint?.State))
            {
                stateChangedSubject.OnNext(newState);
                return;
            }

            // TODO: Integrate with Suspend/Resume logic.
            logger.Info($"State {newState} not dispatched. Restore point state {_restorePoint?.State}");

            switch (newState)
            {
                case PlayerState.Prepared:
                    logger.Info("Issuing Player()");
                    Play();
                    return;
                case PlayerState.Playing when _restorePoint?.State == PlayerState.Playing:
                    break;
                case PlayerState.Playing when _restorePoint?.State == PlayerState.Paused:
                    Pause();
                    break;
            }

            logger.Info("Clearing restore point");
            _restorePoint = null;

        }

        /// <summary>
        /// Method executes PrepareAsync on ESPlayer. On success, notifies
        /// event PlayerInitialized. At this time player is ALREADY PLAYING
        /// </summary>
        /// <returns>bool
        /// True - AsyncPrepare
        /// </returns>
        private async Task PreparePlayback(CancellationToken token)
        {
            logger.Info("");

            try
            {
                using (await asyncOpSerializer.LockAsync(token))
                {
                    token.ThrowIfCancellationRequested();
                    dataSynchronizer.Prepare();

                    StartClockGenerator();

                    logger.Info("Player.PrepareAsync()");

                    var startOk = true;
                    await player.PrepareAsync(async s =>
                       {
                           startOk = await StartTransfer(s, token);
                       })
                        .WithCancellation(token);

                    // bitwise or is intentional
                    if (token.IsCancellationRequested | !startOk)
                        throw new OperationCanceledException();

                    logger.Info("Player.PrepareAsync() Done");
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
                StopClockGenerator();
            }
            catch (Exception e)
            {
                logger.Error(e);
                playbackErrorSubject.OnNext("Start Failed");
            }
        }

        private void PausePlayback()
        {
            StopTransfer();
            StopClockGenerator();
            player.Pause();

            SetState(PlayerState.Paused);
            logger.Info("Playback Paused");
        }

        /// <summary>
        /// Completes data streams.
        /// </summary>
        /// <returns>IEnumerable<Task> List of data streams being terminated</returns>
        private IEnumerable<Task> GetActiveTasks() =>
            esStreams.Where(esStream => esStream != null).Select(esStream => esStream.GetActiveTask());

        private void SetPlayerConfiguration()
        {
            logger.Info("");

            foreach (var esStream in esStreams)
            {
                if (esStream?.Configuration != null)
                    esStream.SetStreamConfiguration();
            }
        }

        private async Task SeekStreamInitialize(CancellationToken token)
        {
            logger.Info("");

            // Stop data streams. They will be restarted from SeekAsync handler.
            StopClockGenerator();
            DisableInput();
            StopTransfer();
            UnsubscribeBufferingEvent();
            EmptyStreams();

            // Make sure data transfer is stopped!
            // SeekAsync behaves unpredictably when data transfer to player
            // is occuring while SeekAsync gets called
            await WaitForAsyncOperationsCompletionAsync().WithCancellation(token);
        }

        private async Task StreamSeek(TimeSpan time, bool resumeNeeded, CancellationToken token)
        {
            dataSynchronizer.Prepare();

            logger.Info($"Player.SeekAsync(): Resume needed: {resumeNeeded} {player.GetState()}");

            var startOk = true;
            await player.SeekAsync(time, async (s, t) =>
               {
                   startOk |= await StartTransfer(s, token);
               })
                .WithCancellation(token);

            // bitwise or is intentional
            if (token.IsCancellationRequested | !startOk)
                throw new OperationCanceledException();

            logger.Info($"Player.SeekAsync() Completed {player.GetState()}");

            if (resumeNeeded)
                player.Resume();

            StartClockGenerator();
            SubscribeBufferingEvent();

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


        private async Task<bool> StartTransfer(ESPlayer.StreamType stream, CancellationToken token)
        {
            logger.Info($"{stream}");

            if (token.IsCancellationRequested)
            {
                player.SubmitEosPacket(stream);
                return false;
            }

            // Video must start first.
            if (stream != ESPlayer.StreamType.Video) return true;

            esStreams[(int)StreamType.Video].Start(token);
            logger.Info($"{StreamType.Audio}: Waiting for first video packet");
            try
            {
                var firstPacket = await esStreams[(int)StreamType.Video].PacketProcessed()
                    .FirstAsync(pt => pt != typeof(BufferConfigurationPacket))
                    .ToTask(token);

                logger.Info($"{StreamType.Audio}: First video packet {firstPacket}");
                if (firstPacket == typeof(EOSPacket))
                {
                    player.SubmitEosPacket(ESPlayer.StreamType.Audio);
                    return false;
                }

                esStreams[(int)StreamType.Audio].Start(token);
                return true;
            }
            catch (TaskCanceledException)
            {
                logger.Info("Operation cancelled");
            }
            catch (Exception e)
            {
                logger.Error(e, "Operation failed");
            }

            player.SubmitEosPacket(ESPlayer.StreamType.Audio);
            player.SubmitEosPacket(ESPlayer.StreamType.Video);
            return false;
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

        private Task WaitForAsyncOperationsCompletionAsync() =>
            Task.Run(action: WaitForAsyncOperationsCompletion);

        public object GetStateSnapshot()
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

            var clock = _playerClock.LastClock;
            logger.Info($"State snapshot. State: {ps} Clock: {clock}");

            return new StateSnapshot
            {
                Clock = _playerClock.LastClock,
                State = ps,
            };
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

            logger.Info("Clock/AsyncOps shutdown");
        }

        private void WaitForAsyncOperationsCompletion()
        {
            //var terminations = (GetActiveTasks().Append(_firstPacketTask)).ToArray();
            var terminations = GetActiveTasks().ToArray();
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

        private void DetachEventHandlers()
        {
            // Detach event handlers
            logger.Info("Detaching event handlers");

            player.EOSEmitted -= OnEos;
            player.ErrorOccurred -= OnESPlayerError;
            player.BufferStatusChanged -= OnBufferStatusChanged;
            player.ResourceConflicted -= OnResourceConflicted;
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
            foreach (var streamReconfigureSub in streamReconfigureSubs)
                streamReconfigureSub?.Dispose();
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
