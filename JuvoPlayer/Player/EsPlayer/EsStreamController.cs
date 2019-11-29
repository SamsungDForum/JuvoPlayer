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
using Nito.AsyncEx;
using AsyncLock = Nito.AsyncEx.AsyncLock;
using PlayerState = JuvoPlayer.Common.PlayerState;

namespace JuvoPlayer.Player.EsPlayer
{
    /// <summary>
    /// Controls transfer stream operation
    /// </summary>
    internal sealed class EsStreamController : IDisposable
    {
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
        private readonly Subject<PlayerState> stateChangedSubject = new Subject<PlayerState>();

        // Support for merged Pause()/Resume()/Buffering(on/off)
        private readonly SuspendResumeLogic _suspendResumeLogic;

        // Returns configuration status of all underlying streams.
        // True - all initialized streams are configures
        // False - at least one underlying stream is not configured
        private bool AllStreamsConfigured => esStreams.All(streamEntry =>
            streamEntry?.IsConfigured ?? true);

        public IPlayerClient Client { get; set; }

        // Termination & serialization objects for async operations.
        private readonly CancellationTokenSource activeTaskCts = new CancellationTokenSource();
        private readonly AsyncLock asyncOpSerializer = new AsyncLock();
        private volatile Task _firstPacketTask = Task.CompletedTask;

        private readonly IDisposable[] streamReconfigureSubs;
        private readonly IDisposable[] playbackErrorSubs;

        private bool isDisposed;
        private bool resourceConflict;

        private readonly IScheduler _clockScheduler = new EventLoopScheduler();
        private readonly PlayerClockProvider _playerClock;
        private readonly DataClockProvider _dataClock;
        private TimeSpan _suspendClock;

        #region Public API

        public void Initialize(StreamType stream)
        {
            logger.Info(stream.ToString());

            if (esStreams[(int)stream] != null)
            {
                throw new ArgumentException($"Stream {stream} already initialized");
            }

            dataSynchronizer.Initialize(stream);

            var esStream = new EsStream(stream, packetStorage, dataSynchronizer, _suspendResumeLogic, _playerClock);
            esStream.SetPlayer(player);

            streamReconfigureSubs[(int)stream] = esStream.StreamReconfigure()
                .Subscribe(async _ => await OnStreamReconfigure(), SynchronizationContext.Current);
            playbackErrorSubs[(int)stream] = esStream.PlaybackError()
                .Subscribe(OnEsStreamError, SynchronizationContext.Current);

            esStreams[(int)stream] = esStream;
        }

        public EsStreamController(EsPlayerPacketStorage storage)
            : this(storage,
                WindowUtils.CreateElmSharpWindow())
        {
            usesExternalWindow = false;
        }

        public EsStreamController(EsPlayerPacketStorage storage, Window window)
        {
            _playerClock = new PlayerClockProvider(_clockScheduler);
            _dataClock = new DataClockProvider(_clockScheduler, _playerClock);
            _suspendResumeLogic = new SuspendResumeLogic(asyncOpSerializer, SuspendPlayback, ResumePlayback, SetPlayerState,
                GetVideoPlayerState, SetDataTransferState, activeTaskCts.Token);

            displayWindow = window;

            CreatePlayer();

            packetStorage = storage;

            // Create placeholder to data streams & chunk states
            esStreams = new EsStream[(int)StreamType.Count];
            streamReconfigureSubs = new IDisposable[(int)StreamType.Count];
            playbackErrorSubs = new IDisposable[(int)StreamType.Count];
            dataSynchronizer = new Synchronizer(_playerClock);

            AttachEventHandlers();
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
                        return PlayerClockProviderConfig.NoClockReturnValue;

                    logger.Error(e);
                    throw;
                }
            }

            return Del;
        }

        private void CreatePlayer()
        {
            logger.Info("");
            player = new ESPlayer.ESPlayer();
            player.Open();

            //The Tizen TV emulator is based on the x86 architecture. Using trust zone (DRM'ed content playback) is not supported by the emulator.
            if (RuntimeInformation.ProcessArchitecture != Architecture.X86) player.SetTrustZoneUse(true);

            player.SetDisplay(displayWindow);
            resourceConflict = false;

            _playerClock.SetPlayerClockSource(CreatePlayerClockFunction(player));
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
        public async Task SetStreamConfiguration(BufferConfigurationPacket config)
        {
            var streamType = config.StreamType;

            logger.Info($"{streamType}:");

            try
            {
                if (config.Config is BufferStreamConfig metaData)
                    await _dataClock.UpdateBufferDepth(metaData.StreamType(), metaData.BufferDuration);

                var pushResult = esStreams[(int)streamType].SetStreamConfiguration(config);

                if (pushResult == EsStream.SetStreamConfigResult.QueueConfiguration)
                {
                    AppendPacket(config);
                    return;
                }

                esStreams[(int)streamType].PushStreamConfiguration();

                // Check if all initialized streams are configured
                if (!AllStreamsConfigured)
                    return;

                var token = activeTaskCts.Token;
                await StreamPrepare(token);
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
        }

        /// <summary>
        /// Starts playback on all initialized streams. Streams do have to be
        /// configured in order for the call to start playback.
        /// </summary>
        public async Task Play()
        {
            var state = player.GetState();
            logger.Info($"{state}");

            if (!AllStreamsConfigured)
            {
                logger.Info("Initialized streams are not configured. Start will occur after receiving configurations");
                return;
            }

            try
            {
                var token = activeTaskCts.Token;
                if (token.IsCancellationRequested)
                    return;

                if (resourceConflict)
                {
                    logger.Info("Player Resource conflict. Trying to restart");

                    await RestartPlayer(token);
                    return;
                }

                var asyncOpRunning = IsAsyncOpRunning();
                logger.Info($"Async Op Running: {asyncOpRunning}");
                switch (state)
                {
                    case ESPlayer.ESPlayerState.Playing:
                        return;

                    case ESPlayer.ESPlayerState.Ready:
                        await StreamStart(token);
                        return;

                    case ESPlayer.ESPlayerState.Paused when asyncOpRunning:
                        stateChangedSubject.OnNext(PlayerState.Playing);
                        return;

                    case ESPlayer.ESPlayerState.Paused:
                        await _suspendResumeLogic.RequestPlay();
                        return;

                    default:
                        throw new InvalidOperationException($"Play called in invalid state: {state}");
                }
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
        public Task Pause()
        {
            logger.Info("");

            return _suspendResumeLogic.RequestPause();
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
                _dataClock.Stop();
                DisableTransfer();
                StopClockGenerator();

                player.Stop();
                stateChangedSubject.OnNext(PlayerState.Idle);
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
                    _suspendResumeLogic.SetAsyncOpRunningState(true);

                    await stateChangedSubject
                        .AsObservable()
                        .FirstAsync(state => state == PlayerState.Playing)
                        .ToTask(token);

                    token.ThrowIfCancellationRequested();

                    _suspendResumeLogic.SetAsyncOpRunningState(false);
                }

                await SeekStreamInitialize(token);
                var seekTotime = await Client.Seek(time, token);
                EnableInput();
                await _dataClock.SetClock(time, token);
                await StreamSeek(seekTotime, resumeNeeded, token);
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
            }
            catch (Exception e)
            {
                logger.Error(e);
                playbackErrorSubject.OnNext("Seek Failed");
            }
        }

        public void AppendPacket(Packet packet)
        {
            try
            {
                packetStorage.AddPacket(packet);
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

        #endregion

        #region Private Methods

        #region Internal EsPlayer event handlers

        private async Task OnStreamReconfigure()
        {
            logger.Info("");

            try
            {
                var token = activeTaskCts.Token;
                await RestartPlayer(token);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation canceled");
            }
            catch (ObjectDisposedException)
            {
                logger.Info("Operation cancelled and disposed");
            }
        }

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

            // Stop and disable all initialized data streams.
            TerminateAsyncOperations();
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

            // Stop and disable all initialized data streams.
            TerminateAsyncOperations();
            playbackErrorSubject.OnNext(error);

        }

        private void OnResourceConflicted(object sender, ESPlayer.ResourceConflictEventArgs e)
        {
            logger.Info("");
            resourceConflict = true;
        }

        private void OnEsStreamError(string error)
        {
            logger.Error(error);

            // Stop and disable all initialized data streams.
            DisableTransfer();
            DisableInput();

            // Perform error notification
            playbackErrorSubject.OnNext(error);
        }

        public IObservable<string> ErrorOccured()
        {
            return playbackErrorSubject.AsObservable();
        }

        #endregion

        private void SuspendPlayback()
        {
            // Get most current player time.
            _suspendClock = _playerClock.LastClock;

            // Stop data clock to halt data providers. Not required for UI based operation
            // but needed for multitasking with player preemption. Prevents data clock difference
            // between player & data provider.
            _dataClock.Stop();
            StopClockGenerator();
            player.Pause();

            logger.Info($"Playback time {_suspendClock}");
        }

        private void ResumePlayback()
        {
            player.Resume();
            StartClockGenerator();
            _dataClock.Start();
            logger.Info("");
        }

        private void SetPlayerState(PlayerState state)
        {
            logger.Info(state.ToString());
            stateChangedSubject.OnNext(state);
        }

        private ESPlayer.ESPlayerState GetVideoPlayerState()
        {
            return player.GetState();
        }

        private void SetDataTransferState(bool isRunning)
        {
            logger.Info($"{isRunning}");
            if (isRunning)
            {
                EnableTransfer(StreamType.Video);
                EnableTransfer(StreamType.Audio);
                return;
            }

            DisableTransfer();
            WaitForAsyncOperationsCompletion();
        }

        private async Task ExecutePrepareAsync(CancellationToken token)
        {
            _suspendResumeLogic.SetBuffering(true);
            esStreams[(int)StreamType.Video].RequestFirstDataPacketNotification();

            dataSynchronizer.Prepare();

            logger.Info("Player.PrepareAsync()");

            var asyncOp = player.PrepareAsync(EnableTransfer);
            dataSynchronizer.SetAsyncOperation(asyncOp);
            await asyncOp.WithCancellation(token);

            _suspendResumeLogic.SetBuffering(false);
            logger.Info("Player.PrepareAsync() Completed");
        }
        /// <summary>
        /// Method executes PrepareAsync on ESPlayer. On success, notifies
        /// event PlayerInitialized. At this time player is ALREADY PLAYING
        /// </summary>
        /// <returns>bool
        /// True - AsyncPrepare
        /// </returns>
        private async Task StreamPrepare(CancellationToken token)
        {
            logger.Info("");

            try
            {
                using (await asyncOpSerializer.LockAsync(token))
                {
                    if (token.IsCancellationRequested)
                        return;

                    _suspendResumeLogic.SetAsyncOpRunningState(true);

                    await ExecutePrepareAsync(token);

                    _suspendResumeLogic.SetAsyncOpRunningState(false);
                    stateChangedSubject.OnNext(PlayerState.Prepared);

                }
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe);
                playbackErrorSubject.OnNext(ioe.Message);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
                DisableTransfer();
            }
            catch (Exception e)
            {
                logger.Error(e);
                playbackErrorSubject.OnNext("Start Failed");
            }
        }

        private async Task StreamStart(CancellationToken token)
        {
            logger.Info("");
            using (await asyncOpSerializer.LockAsync(token))
            {
                if (token.IsCancellationRequested)
                    return;

                player.Start();
                StartClockGenerator();
                stateChangedSubject.OnNext(PlayerState.Playing);
            }
        }

        /// <summary>
        /// Completes data streams.
        /// </summary>
        /// <returns>IEnumerable<Task> List of data streams being terminated</returns>
        private IEnumerable<Task> GetActiveTasks() =>
            esStreams.Where(esStream => esStream != null).Select(esStream => esStream.GetActiveTask());

        private async Task RestartPlayer(CancellationToken token)
        {
            logger.Info("");

            try
            {
                using (await asyncOpSerializer.LockAsync(token))
                {
                    token.ThrowIfCancellationRequested();

                    // Stop data streams & clock
                    DisableTransfer();
                    StopClockGenerator();

                    // Stop any underlying async ops
                    await WaitForAsyncOperationsCompletionAsync().WithCancellation(token);

                    token.ThrowIfCancellationRequested();

                    RecreatePlayer();

                    await ExecutePrepareAsync(token);

                    player.Start();
                    StartClockGenerator();
                    await _dataClock.SetClock(_suspendClock, token);
                }
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
                DisableTransfer();
            }
            catch (Exception e)
            {
                logger.Error(e);
                playbackErrorSubject.OnNext("Restart Error");
            }
        }

        private void RecreatePlayer()
        {
            logger.Info("");

            DetachEventHandlers();

            _playerClock.SetPlayerClockSource(null);

            player.Stop();
            player.Dispose();

            CreatePlayer();

            AttachEventHandlers();

            foreach (var esStream in esStreams)
            {
                if (esStream == null)
                    continue;

                esStream.SetPlayer(player);
                esStream.PushStreamConfiguration();
            }
        }

        private async Task SeekStreamInitialize(CancellationToken token)
        {
            logger.Info("");

            // Stop data streams. They will be restarted from
            // SeekAsync handler.
            _dataClock.Stop();
            DisableInput();
            DisableTransfer();
            StopClockGenerator();

            // Make sure data transfer is stopped!
            // SeekAsync behaves unpredictably when data transfer to player
            // is occuring while SeekAsync gets called
            await WaitForAsyncOperationsCompletionAsync().WithCancellation(token);

            EmptyStreams();
            token.ThrowIfCancellationRequested();
            logger.Info("Buffer reset confirmed");

            esStreams[(int)StreamType.Video].RequestFirstDataPacketNotification();
        }

        private async Task StreamSeek(TimeSpan time, bool resumeNeeded, CancellationToken token)
        {
            using (await asyncOpSerializer.LockAsync(token))
            {
                token.ThrowIfCancellationRequested();

                dataSynchronizer.Prepare();

                logger.Info($"Player.SeekAsync(): Resume needed: {resumeNeeded} {player.GetState()}");

                var asyncOp = player.SeekAsync(time, EnableTransfer);

                dataSynchronizer.SetAsyncOperation(asyncOp);
                await asyncOp.WithCancellation(token);

                logger.Info("Player.SeekAsync() Completed");

                token.ThrowIfCancellationRequested();

                if (resumeNeeded)
                    player.Resume();

                StartClockGenerator();
            }
        }

        /// <summary>
        /// Stops all initialized data streams preventing transfer of data from associated
        /// data queue to underlying player. When stopped, stream can still accept new data
        /// </summary>
        private void DisableTransfer()
        {
            logger.Info("Stopping all data streams");

            foreach (var esStream in esStreams)
                esStream?.Stop();
        }

        private void EmptyStreams()
        {
            foreach (var stream in esStreams)
                stream?.EmptyStorage();
        }

        private void EnableInput()
        {
            foreach (var stream in esStreams)
                stream?.EnableStorage();
        }

        private void EnableTransfer(ESPlayer.StreamType stream, TimeSpan time) =>
            EnableTransfer(stream.JuvoStreamType());

        private void EnableTransfer(ESPlayer.StreamType stream) =>
            EnableTransfer(stream.JuvoStreamType());

        private void EnableTransfer(StreamType stream)
        {
            logger.Info($"{stream}");

            // Audio packet must be started after video packets.
            if (stream == StreamType.Video)
            {
                esStreams[(int)StreamType.Video].Start();
            }
            else
            {
                // Start first packet listener.
                // Blocking calls, if executed from within (Prepare/Seek)Async() may cause deadlocks
                // with packet submission (?!?)
                _firstPacketTask = Task.Run(async () => await FirstPacketWait());
            }
        }

        private async Task FirstPacketWait()
        {
            logger.Info(
                $"{StreamType.Audio}: Waiting for {StreamType.Video} first data packet {Thread.CurrentThread.ManagedThreadId}");

            try
            {
                await esStreams[(int)StreamType.Video].GetFirstDataPacketNotificationTask();

                logger.Info(
                    $"{StreamType.Audio}: {StreamType.Video} first data packet processed");

                if (!activeTaskCts.Token.IsCancellationRequested)
                {
                    esStreams[(int)StreamType.Audio].Start();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                logger.Info(
                    $"{StreamType.Audio}: Wait for {StreamType.Video} first data packet cancelled");
            }

            // Issue EOS if Player Async Operation (seek/prepare) is in progress
            if (IsAsyncOpRunning())
            {
                logger.Info("Terminating player async operation by A/V EOS");
                player.SubmitEosPacket(ESPlayer.StreamType.Video);
                player.SubmitEosPacket(ESPlayer.StreamType.Audio);
            }
        }

        /// <summary>
        /// Disables all initialized data streams preventing
        /// any further new input collection
        /// </summary>
        private void DisableInput()
        {
            logger.Info("Stop and Disable all data streams");

            foreach (var esStream in esStreams)
                esStream?.Disable();
        }

        /// <summary>
        /// Starts clock generation task
        /// </summary>
        private void StartClockGenerator()
        {
            logger.Info("");
            _playerClock.EnableClock();
        }

        /// <summary>
        /// Terminates clock generation task
        /// </summary>
        private void StopClockGenerator()
        {
            logger.Info("");
            _playerClock.DisableClock();
        }

        private bool IsAsyncOpRunning()
        {
            try
            {
                // Check if async op is in progress
                asyncOpSerializer.Lock(new CancellationToken(true)).Dispose();
                return false;
            }
            catch (TaskCanceledException)
            {
                return true;
            }
        }

        private Task WaitForAsyncOperationsCompletionAsync() =>
            Task.Run(action: WaitForAsyncOperationsCompletion);

        #endregion

        #region Dispose support

        private void TerminateAsyncOperations()
        {
            // Stop clock & async operations
            logger.Info("");

            activeTaskCts.Cancel();

            _dataClock.Stop();
            StopClockGenerator();

            DisableTransfer();
            DisableInput();
            activeTaskCts.Cancel();

            logger.Info("Clock/AsyncOps shutdown");
        }

        private void WaitForAsyncOperationsCompletion()
        {
            var terminations = (GetActiveTasks().Append(_firstPacketTask)).ToArray();
            logger.Info($"Waiting for {terminations.Length} operations to complete");
            Task.WhenAll(terminations).WaitWithoutException();
            logger.Info("Done");
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            logger.Info("");
            _suspendResumeLogic.SetBuffering(false);

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
        }

        private void DisposeAllSubscriptions()
        {
            foreach (var streamReconfigureSub in streamReconfigureSubs)
                streamReconfigureSub?.Dispose();
            foreach (var playbackErrorSub in playbackErrorSubs)
                playbackErrorSub?.Dispose();
        }

        #endregion

        public IObservable<PlayerState> StateChanged()
        {
            return stateChangedSubject.AsObservable();
        }

        public IObservable<int> BufferingProgress()
        {
            return _suspendResumeLogic.BufferingProgressObservable();
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
