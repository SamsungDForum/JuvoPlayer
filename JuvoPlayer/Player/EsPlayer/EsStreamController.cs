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
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using ESPlayer = Tizen.TV.Multimedia;
using System.Threading.Tasks;
using Configuration;
using ElmSharp;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Utils;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;
using static Configuration.EsStreamControllerConfig;
using System.Runtime.InteropServices;

namespace JuvoPlayer.Player.EsPlayer
{
    /// <summary>
    /// Controls transfer stream operation
    /// </summary>
    internal sealed class EsStreamController : IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private enum SuspendState
        {
            Playing,
            Paused,
            Buffering,
            NotPlaying
        }

        [Flags]
        private enum SuspendRequest
        {
            SuspendingRequest = 1,
            StartBuffering = 2 | SuspendingRequest,
            StopBuffering = 4,
            StartPause = 8 | SuspendingRequest,
            StopPause = 16
        }

        // Reference to all data streams representing transfer of individual
        // stream data and data storage
        private readonly EsStream[] esStreams;
        private readonly EsPlayerPacketStorage packetStorage;
        private readonly EsBuffer dataBuffer;
        private readonly Synchronizer dataSynchronizer;

        // Reference to ESPlayer & associated window
        private ESPlayer.ESPlayer player;
        private readonly Window displayWindow;
        private readonly bool usesExternalWindow = true;

        // event callbacks
        private readonly Subject<string> playbackErrorSubject = new Subject<string>();
        private readonly Subject<PlayerState> stateChangedSubject = new Subject<PlayerState>();

        // Support for merged Pause()/Resume()/Buffering(on/off)
        private delegate void BufferingProgressDelegate(int progress);
        private event BufferingProgressDelegate BufferingProgressEvent;
        private readonly IObservable<int> bufferingProgressObservable;
        private readonly IDisposable pauseBufferingSubscription;
        private int bufferingRequests;

        // Returns configuration status of all underlying streams.
        // True - all initialized streams are configures
        // False - at least one underlying stream is not configured
        private bool AllStreamsConfigured => esStreams.All(streamEntry =>
            streamEntry?.IsConfigured ?? true);

        public IPlayerClient Client { get; set; }

        // Termination & serialization objects for async operations.
        private readonly CancellationTokenSource activeTaskCts = new CancellationTokenSource();
        private readonly AsyncLock asyncOpSerializer = new AsyncLock();
        private readonly AsyncLock pauseBufferSerializer = new AsyncLock();

        private readonly IDisposable[] streamReconfigureSubs;
        private readonly IDisposable[] playbackErrorSubs;

        private bool isDisposed;
        private bool resourceConflict;

        private readonly ClockProvider playerClock = ClockProvider.GetClockProvider();

        #region Public API

        public void Initialize(StreamType stream)
        {
            logger.Info(stream.ToString());

            if (esStreams[(int)stream] != null)
            {
                throw new ArgumentException($"Stream {stream} already initialized");
            }

            dataBuffer.Initialize(stream);
            dataSynchronizer.Initialize(stream);

            var esStream = new EsStream(stream, packetStorage, dataBuffer, dataSynchronizer);
            esStream.SetPlayer(player);

            streamReconfigureSubs[(int)stream] = esStream.StreamReconfigure()
                .Subscribe(async _ => await OnStreamReconfigure(), SynchronizationContext.Current);
            playbackErrorSubs[(int)stream] = esStream.PlaybackError()
                .Subscribe(OnEsStreamError, SynchronizationContext.Current);

            esStreams[(int)stream] = esStream;

            dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.DataRequest);
        }

        public EsStreamController(EsPlayerPacketStorage storage)
            : this(storage,
                WindowUtils.CreateElmSharpWindow())
        {
            usesExternalWindow = false;
        }

        public EsStreamController(EsPlayerPacketStorage storage, Window window)
        {
            displayWindow = window;

            CreatePlayer();

            packetStorage = storage;

            // Create placeholder to data streams & chunk states
            esStreams = new EsStream[(int)StreamType.Count];
            streamReconfigureSubs = new IDisposable[(int)StreamType.Count];
            playbackErrorSubs = new IDisposable[(int)StreamType.Count];

            dataBuffer = new EsBuffer();
            dataSynchronizer = new Synchronizer();

            pauseBufferingSubscription = dataBuffer
                .BufferingRequestObservable()
                .Select(on => (on: on, isPause: false))
                .Subscribe(async args =>
                    await OnSuspendResume(args.on ? SuspendRequest.StartBuffering : SuspendRequest.StopBuffering),
                    SynchronizationContext.Current);

            bufferingProgressObservable =
                Observable.FromEvent<BufferingProgressDelegate, int>(h => BufferingProgressEvent += h, h => BufferingProgressEvent -= h);

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
                        return ClockProviderConfig.InvalidClock;

                    logger.Error(e);
                    throw;
                }
            }

            return Del;
        }

        private void CreatePlayer()
        {
            player = new ESPlayer.ESPlayer();
            player.Open();

            //The Tizen TV emulator is based on the x86 architecture. Using trust zone (DRM'ed content playback) is not supported by the emulator.
            if (RuntimeInformation.ProcessArchitecture != Architecture.X86) player.SetTrustZoneUse(true);

            player.SetDisplay(displayWindow);
            resourceConflict = false;

            playerClock.SetPlayerClockSource(CreatePlayerClockFunction(player));
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
                if (config.Config is MetaDataStreamConfig metaData)
                {
                    dataBuffer.UpdateBufferConfiguration(metaData);
                    return;
                }

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
            logger.Info("");

            if (!AllStreamsConfigured)
            {
                logger.Info("Initialized streams are not configured. Start will occur after receiving configurations");
                return;
            }

            try
            {
                if (resourceConflict)
                {
                    logger.Info("Player Resource conflict. Trying to restart");
                    var token = activeTaskCts.Token;
                    await RestartPlayer(token);
                    return;
                }

                var state = player.GetState();
                switch (state)
                {
                    case ESPlayer.ESPlayerState.Playing:
                        return;
                    case ESPlayer.ESPlayerState.Ready:
                        stateChangedSubject.OnNext(PlayerState.Playing);
                        player.Start();
                        StartClockGenerator();
                        dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.All);
                        break;
                    case ESPlayer.ESPlayerState.Paused:
                        await OnSuspendResume(SuspendRequest.StopPause);
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
        public void Pause()
        {
            logger.Info("");

            try
            {
                // UTs expect this operation to be "synchronous".
                OnSuspendResume(SuspendRequest.StartPause).Wait(activeTaskCts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation cancelled");
            }
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
                dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.None);
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
            logger.Info("");
            var token = activeTaskCts.Token;

            try
            {
                await SeekStreamInitialize(token);
                time = await Client.Seek(time, token);
                EnableInput();
                await StreamSeek(time, token);
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
                dataBuffer.DataIn(packet);
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
            DisableTransfer();
            DisableInput();

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
            DisableTransfer();
            DisableInput();

            // Perform error notification
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

        private void OnReadyToStartStream(ESPlayer.StreamType esPlayerStreamType)
        {
            var streamType = esPlayerStreamType.JuvoStreamType();

            logger.Info(streamType.ToString());
            switch (streamType)
            {
                case StreamType.Audio:
                    esStreams[(int)StreamType.Audio].Start();

                    break;
                case StreamType.Video:
                    esStreams[(int)StreamType.Video].Start();

                    break;
                default:
                    throw new ArgumentException(nameof(streamType),
                        $"{streamType} not supported in current context");
            }

            logger.Info($"{streamType}: Completed");
        }

        private void OnReadyToSeekStream(ESPlayer.StreamType esPlayerStreamType, TimeSpan time)
        {
            var streamType = esPlayerStreamType.JuvoStreamType();

            logger.Info($"{esPlayerStreamType}: {time}");

            switch (streamType)
            {
                case StreamType.Audio:
                    esStreams[(int)StreamType.Audio].Start();

                    break;
                case StreamType.Video:
                    esStreams[(int)StreamType.Video].Start();

                    break;
                default:
                    throw new ArgumentException(nameof(streamType), $"{streamType} not supported in current context");
            }

            logger.Info($"{streamType}: Completed");
        }

        #endregion

        public IObservable<TimeSpan> TimeUpdated()
        {
            return playerClock.PlayerClockObservable()
                .Sample(ClockInterval);
        }

        private void SuspendPlayback(SuspendRequest request, SuspendState currently)
        {
            logger.Info($"Request: '{request}' Currently: '{currently}'");

            switch (request)
            {
                case SuspendRequest.StartPause when currently == SuspendState.Playing:
                    stateChangedSubject.OnNext(PlayerState.Paused);
                    dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.None);
                    DisableTransfer();
                    break;

                case SuspendRequest.StartPause when currently == SuspendState.Buffering:
                    stateChangedSubject.OnNext(PlayerState.Paused);
                    return;

                case SuspendRequest.StartBuffering when currently == SuspendState.Playing:
                    BufferingProgressEvent?.Invoke(0);
                    bufferingRequests++;
                    break;

                case SuspendRequest.StartBuffering when currently == SuspendState.Buffering:
                    bufferingRequests++;
                    return;

                default:
                    logger.Info("Request ignored");
                    return;
            }

            StopClockGenerator();
            player.Pause();
        }

        private void ResumePlayback(SuspendRequest request, SuspendState currently)
        {
            logger.Info($"Request: '{request}' Currently: '{currently}' Buffering Requests: {bufferingRequests}");

            switch (request)
            {
                case SuspendRequest.StopPause when currently == SuspendState.Paused:
                    stateChangedSubject.OnNext(PlayerState.Playing);
                    dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.All);
                    EnableTransfer();
                    break;

                case SuspendRequest.StopPause when currently == SuspendState.Buffering:
                    stateChangedSubject.OnNext(PlayerState.Playing);
                    return;

                case SuspendRequest.StopBuffering when currently == SuspendState.Buffering && bufferingRequests > 1:
                    bufferingRequests--;
                    return;

                case SuspendRequest.StopBuffering when currently == SuspendState.Buffering && bufferingRequests == 1:
                    BufferingProgressEvent?.Invoke(100);
                    bufferingRequests = 0;
                    break;

                default:
                    logger.Info("Request ignored");
                    return;
            }

            player.Resume();
            StartClockGenerator();
        }

        private SuspendState CurrentSuspendState()
        {
            var playerState = player.GetState();

            switch (playerState)
            {
                case ESPlayer.ESPlayerState.Playing:
                    return SuspendState.Playing;

                case ESPlayer.ESPlayerState.Paused when bufferingRequests == 0:
                    return SuspendState.Paused;

                case ESPlayer.ESPlayerState.Paused when bufferingRequests > 0:
                    return SuspendState.Buffering;

                default:
                    return SuspendState.NotPlaying;
            }
        }

        private async Task OnSuspendResume(SuspendRequest request)
        {
            var token = activeTaskCts.Token;
            using (await pauseBufferSerializer.LockAsync(token))
            {
                // Cancelled tokens can acquire async lock.
                if (token.IsCancellationRequested)
                    return;

                var currently = CurrentSuspendState();
                if ((request & SuspendRequest.SuspendingRequest) == SuspendRequest.SuspendingRequest)
                    SuspendPlayback(request, currently);
                else
                    ResumePlayback(request, currently);
            }
        }

        private async Task ExecutePrepareAsync(CancellationToken token)
        {
            esStreams[(int)StreamType.Audio].FirstDataPacketCompletionTask =
                esStreams[(int)StreamType.Video].RequestFirstDataPacketNotification();

            dataSynchronizer.InitializeSynchronization(false);
            dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.DataRequest);

            logger.Info("Player.PrepareAsync()");

            await player.PrepareAsync(OnReadyToStartStream).WithCancellation(token);

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
                    await ExecutePrepareAsync(token);

                    token.ThrowIfCancellationRequested();
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

        /// <summary>
        /// Completes data streams.
        /// </summary>
        /// <returns>IEnumerable<Task> List of data streams being terminated</returns>
        private IEnumerable<Task> GetActiveTasks() =>
            esStreams.Where(esStream => esStream != null)
                .Select(esStream => esStream.GetActiveTask());

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
                    var terminations = GetActiveTasks().ToArray();
                    logger.Info($"Waiting for {terminations.Length} activities to complete");
                    await Task.WhenAll(terminations).WithCancellation(token);

                    token.ThrowIfCancellationRequested();

                    RecreatePlayer();

                    await ExecutePrepareAsync(token);

                    player.Start();
                    dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.All);
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

            StopClockGenerator();
            playerClock.SetPlayerClockSource(null);

            player.Stop();
            player.Dispose();

            CreatePlayer();

            AttachEventHandlers();

            foreach (var esStream in esStreams)
            {
                esStream?.SetPlayer(player);
            }
        }

        private async Task SeekStreamInitialize(CancellationToken token)
        {
            logger.Info("");

            dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.None);
            // New data is not needed anymore. Halt data providers with
            // buffer full notification.
            dataBuffer.SendBufferFullDataRequest(StreamType.Video);
            dataBuffer.SendBufferFullDataRequest(StreamType.Audio);

            // Stop data streams. They will be restarted from
            // SeekAsync handler.
            DisableInput();
            DisableTransfer();
            StopClockGenerator();

            // Make sure data transfer is stopped!
            // SeekAsync behaves unpredictably when data transfer to player
            // is occuring while SeekAsync gets called
            var terminations = GetActiveTasks().ToArray();
            logger.Info($"Waiting for {terminations.Length} activities to complete");
            await Task.WhenAll(terminations).WithCancellation(token);

            var resetDone = dataBuffer.Reset();
            EmptyStreams();
            await resetDone.WithCancellation(token);
            token.ThrowIfCancellationRequested();
            logger.Info("Buffer reset confirmed");

            esStreams[(int)StreamType.Audio].FirstDataPacketCompletionTask =
                esStreams[(int)StreamType.Video].RequestFirstDataPacketNotification();
        }

        private async Task StreamSeek(TimeSpan time, CancellationToken token)
        {
            var seekToMsg = time.ToString();

            var skipClockDetection = player.GetState() == ESPlayer.ESPlayerState.Paused;
            if (skipClockDetection)
                seekToMsg += " Seeking in paused state. Running clock detection will be skipped";

            logger.Info(seekToMsg);

            using (await asyncOpSerializer.LockAsync(token))
            {
                token.ThrowIfCancellationRequested();

                dataSynchronizer.InitializeSynchronization(skipClockDetection);
                dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.DataRequest);

                logger.Info("Player.SeekAsync()");

                await player.SeekAsync(time, OnReadyToSeekStream).WithCancellation(token);

                logger.Info("Player.SeekAsync() Completed");

                token.ThrowIfCancellationRequested();

                StartClockGenerator();

                dataBuffer.SetAllowedEvents(EsBuffer.DataEvent.All);
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

        /// <summary>
        /// Starts all initialized data streams allowing transfer of data from associated
        /// data queues to underlying player
        /// </summary>
        /// <param name="startTransfer">
        /// True - Start Transfer
        /// False - Enable transfer but do not start it</param>
        private void EnableTransfer()
        {
            logger.Info("");

            // Starts can happen.. when they happen. See no reason to
            // wait for their completion.
            foreach (var esStream in esStreams)
                esStream?.Start();
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
            playerClock.EnableClock();
        }

        /// <summary>
        /// Terminates clock generation task
        /// </summary>
        private void StopClockGenerator()
        {
            logger.Info("");
            playerClock.DisableClock();
        }

        #endregion

        #region Dispose support

        private void TerminateAsyncOperations()
        {
            // Stop clock & async operations
            logger.Info("");
            activeTaskCts.Cancel();

            try
            {
                // Check if async op is in progress
                asyncOpSerializer.Lock(activeTaskCts.Token).Dispose();

                // No async ops. Disable transfer
                DisableTransfer();
            }
            catch (TaskCanceledException)
            {
                logger.Info("Async operation in progress. Terminating with EOS");
                var audioResult = player.SubmitEosPacket(ESPlayer.StreamType.Audio);
                var videoResult = player.SubmitEosPacket(ESPlayer.StreamType.Video);
                logger.Info($"{StreamType.Audio} EOS: {audioResult} {StreamType.Video} EOS: {videoResult}");
            }

            StopClockGenerator();
            playerClock.SetPlayerClockSource(null);
            logger.Info("Clock/AsyncOps shutdown");
        }

        private void WaitForAsyncOperationsCompletion()
        {
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
            dataBuffer.Dispose();
            DisposeStreams();
            DisposeAllSubjects();
            playerClock.Dispose();

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
        }

        private void DisposeAllSubscriptions()
        {
            foreach (var streamReconfigureSub in streamReconfigureSubs)
                streamReconfigureSub?.Dispose();
            foreach (var playbackErrorSub in playbackErrorSubs)
                playbackErrorSub?.Dispose();

            pauseBufferingSubscription?.Dispose();
        }

        #endregion

        public IObservable<PlayerState> StateChanged()
        {
            return stateChangedSubject
                .AsObservable();
        }

        public IObservable<int> BufferingProgress()
        {
            return bufferingProgressObservable;
        }

        public IObservable<DataRequest> DataNeededStateChanged()
        {
            return dataBuffer.DataRequestObservable();
        }

    }
}
