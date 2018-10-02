// Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using System;
using System.Collections.Generic;
using JuvoPlayer.Common;
using JuvoLogger;
using System.Linq;
using System.Threading;
using ESPlayer = Tizen.TV.Multimedia.ESPlayer;
using System.Threading.Tasks;

namespace JuvoPlayer.Player.EsPlayer
{
    /// <summary>
    /// Controls transfer stream operation
    /// </summary>
    internal class EsStreamController
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        /// <summary>
        /// Instance reference & creation lock
        /// </summary>
        private static EsStreamController streamControl;
        private static readonly object InstanceLock = new object();

        /// <summary>
        /// Reference to all data streams representing transfer
        /// of individual stream data
        /// </summary>
        private EsStream[] dataStreams;

        /// <summary>
        /// Reference to ESPlayer
        /// </summary>
        private ESPlayer.ESPlayer player;

        /// <summary>
        /// event callbacks
        /// </summary>
        public event TimeUpdated TimeUpdated;
        public event PlaybackError PlaybackError;
        public event PlaybackCompleted PlaybackCompleted;
        public event PlayerInitialized PlayerInitialized;
        public event SeekCompleted SeekCompleted;
        public event BufferStatus BufferStatus;

        /// <summary>
        /// Timer process and supporting cancellation elements
        /// </summary>
        private Task clockGenerator = Task.CompletedTask;
        private CancellationTokenSource clockGeneratorCts;

        /// <summary>
        /// Returns configuration status of all underlying streams.
        /// True - all initialized streams are configures
        /// False - at least one underlying stream is not configured
        /// </summary>
        private bool AllStreamsConfigured => dataStreams.All(streamEntry =>
            streamEntry?.IsConfigured ?? true);

        /// <summary>
        /// Placeholder for current async activity issued to ESPlayer and storage
        /// place for underlying async operation.
        /// Initialized with Task.Task.Completed to avoid null checks.
        /// </summary>
        private Task activeTask = Task.CompletedTask;

        /// <summary>
        /// Cancellation token source for terminating current active task.
        /// </summary>
        private CancellationTokenSource activeTaskCts;

        /// <summary>
        /// active task serializer. Used for assuring that only one active task will
        /// be created, offering awaitability.
        /// </summary>
        private SemaphoreSlim activeTaskSerializer;

        private ElmSharp.Window displayWindow;

        private TimeSpan currentClock;

        #region Instance Support
        /// <summary>
        /// Obtains an instance of Stream Controller
        /// </summary>
        /// <param name="esPlayer">ESPlayer</param>
        /// <returns>EsStreamController instance</returns>
        public static EsStreamController GetInstance()
        {
            lock (InstanceLock)
            {
                if (streamControl != null)
                    return streamControl;

                streamControl = new EsStreamController();
                streamControl.ConfigureInstance();

                return streamControl;
            }
        }

        /// <summary>
        /// Configure newly created instance
        /// </summary>
        /// <param name="esPlayer">ESPlayer</param>
        private void ConfigureInstance()
        {
            // Create player & window
            displayWindow =
                EsPlayerUtils.CreateWindow(EsPlayerUtils.DefaultWindowWidth, EsPlayerUtils.DefaultWindowHeight);

            player = new ESPlayer.ESPlayer();
            player.Open();
            player.SetDisplay(displayWindow);

            // Create async operation/task cancellations
            activeTaskCts = new CancellationTokenSource();
            clockGeneratorCts = new CancellationTokenSource();

            // Create serialization object assuring single async activity
            activeTaskSerializer = new SemaphoreSlim(1);

            // Create placeholder to data streams.
            dataStreams = new EsStream[(int)StreamType.Count];

            // Create storage places
            //attach event handlers
            player.EOSEmitted += OnEos;
            player.ErrorOccurred += OnError;
            player.BufferStatusChanged += OnBufferStatusChanged;
        }

        /// <summary>
        /// Unconfigures and instance
        /// </summary>
        private void UnconfigureInstance()
        {
            logger.Info("");

            streamControl = null;

            logger.Info("Data Streams shutdown");
            // Stop data streams
            StopDataStreams();

            // Detach event handlers
            logger.Info("Detaching event handlers");
            player.EOSEmitted -= OnEos;
            player.ErrorOccurred -= OnError;
            player.BufferStatusChanged -= OnBufferStatusChanged;

            // Stop clock & async operations
            logger.Info("Clock/AsyncOps shutdown");
            StopClockGenerator();

            activeTaskCts.Cancel();
            clockGeneratorCts.Cancel();

            // Wait for data streams/clock & active task to terminate
            // No need to add underlying async operation task. This is just placeholder
            // for async op which cannot be terminated.
            // This will also release any pending async activity
            //
            var terminations = CompleteDataStreams();
            terminations.Add(clockGenerator);
            terminations.Add(activeTask);

            logger.Info($"Waiting for completion of {terminations.Count} activities");

            try
            {
                Task.WhenAll(terminations);
            }
            catch (AggregateException)
            {
            }

            // Dispose of individual streams.
            logger.Info("Data Streams shutdown");
            foreach (var esStream in dataStreams.Where(esStream => esStream != null))
            {
                esStream.ReconfigureStream -= OnStreamReconfigure;
                esStream.Dispose();
            }

            // Nullify references to data streams
            dataStreams = Enumerable.Repeat<EsStream>(null, dataStreams.Length).ToArray();

            // Shut down player
            logger.Info("ESPlayer shutdown");

            // Don't call Close. Dispose does that. Otherwise exceptions will fly
            player.Dispose();
            EsPlayerUtils.DestroyWindow(ref displayWindow);

            // Should be safe now to dispose cancellation tokens &
            // serialization object
            activeTaskCts.Dispose();
            activeTaskSerializer.Dispose();
            clockGeneratorCts.Dispose();
        }

        /// <summary>
        /// Releases instance resources. 
        /// </summary>
        public static void FreeInstance()
        {
            lock (InstanceLock)
            {
                if (streamControl == null)
                    return;

                streamControl?.UnconfigureInstance();
            }
        }

        /// <summary>
        /// Initializes a stream to be used with stream controller
        /// Must be called before usage of the stream with stream controller
        /// </summary>
        /// <param name="stream">Common.StreamType</param>
        public void Initialize(Common.StreamType stream)
        {
            lock (InstanceLock)
            {
                logger.Info(stream.ToString());

                if (dataStreams[(int)stream] != null)
                {
                    logger.Info($"{stream}: Already initialized");
                    return;
                }

                // Create new data stream in its place
                //
                dataStreams[(int)stream] = new EsStream(player, stream);
                dataStreams[(int)stream].ReconfigureStream += OnStreamReconfigure;

                activeTaskCts = new CancellationTokenSource();
            }

        }
        #endregion

        #region Public API
        /// <summary>
        /// Sets provided configuration to appropriate stream.
        /// </summary>
        /// <param name="config">StreamConfig</param>
        public void SetStreamConfiguration(BufferConfigurationPacket configPacket)
        {
            logger.Info("");

            var streamType = configPacket.StreamType;
            try
            {
                var isConfigPushed = dataStreams[(int)streamType].SetStreamConfig(configPacket);

                // Configuration queued. Do not prepare stream :)
                if (!isConfigPushed)
                    return;


                // Check if all initialized streams are configured
                if (!AllStreamsConfigured)
                    return;

                // All configured. Do preparation.
                // This will block if there is an active async operation in progress
                // however, can be made async/non blocking
                if (activeTaskSerializer.CurrentCount == 0)
                {
                    logger.Info("Waiting for current async activity to complete");
                }

                var token = activeTaskCts.Token;
                WaitForActiveTaskCompletion(token);
                activeTask = StreamPrepare(token);
            }
            catch (NullReferenceException)
            {
                // packetQueue can hold ALL StreamTypes, but not all of them
                // have to be supported. 
                logger.Warn($"Uninitialized Stream Type {streamType}");
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
            }
            catch (ObjectDisposedException)
            {
                logger.Info("Operation Cancelled and disposed");
            }
            catch (InvalidOperationException)
            {
                // Queue has been marked as completed 
                logger.Warn($"Data queue terminated for stream: {streamType}");
            }
        }

        /// <summary>
        /// Starts playback on all initialized streams. Streams do have to be
        /// configured in order for the call to start playback.
        /// </summary>
        public void Play()
        {
            logger.Info("");

            if (!AllStreamsConfigured)
            {
                logger.Info("Initialized streams are not configured. Play Aborted");
                return;
            }

            try
            {
                player.Start();

                StartDataStreams();
                StartClockGenerator();
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
            }

        }

        /// <summary>
        /// Resumes playback on all initialized streams. Playback had to be
        /// paused.
        /// </summary>
        public void Resume()
        {
            logger.Info("");

            try
            {
                player.Resume();

                StartDataStreams();
                StartClockGenerator();
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
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
                player.Pause();

                StopDataStreams();
                StopClockGenerator();
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
            }
        }

        /// <summary>
        /// Stops playback on all initialized streams.
        /// </summary>
        public void Stop()
        {
            logger.Info("");

            try
            {
                player.Stop();

                StopDataStreams();
                StopClockGenerator();
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
            }
        }

        public void Seek(TimeSpan time)
        {
            logger.Info("");

            try
            {
                var token = activeTaskCts.Token;
                WaitForActiveTaskCompletion(token);
                activeTask = StreamSeek(time, token);
            }
            catch (OperationCanceledException oce)
            {
                logger.Info("Operation Canceled");
            }
        }
        #endregion

        #region Private Methods

        #region Internal EsPlayer event handlers

        private void OnStreamReconfigure()
        {
            logger.Info("");

            try
            {
                if (activeTaskSerializer.CurrentCount == 0)
                {
                    logger.Info("Waiting for current async activity to complete");
                }

                var token = activeTaskCts.Token;
                WaitForActiveTaskCompletion(token);
                activeTask = RestartPlayer(token);
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
            var juvoStream = EsPlayerUtils.JuvoStreamType(buffArgs.StreamType);
            var state = buffArgs.BufferStatus == ESPlayer.BufferStatus.Overrun
                ? BufferState.BufferOverrun
                : BufferState.BufferUnderrun;

            BufferStatus?.Invoke(juvoStream, state);
        }

        /// <summary>
        /// ESPlayer event handler. Notifies that ALL played streams have
        /// completed playback (EOS was sent on all of them)
        /// Methods 
        /// </summary>
        /// <param name="sender">Object</param>
        /// <param name="eosArgs">ESPlayer.EosArgs</param>
        private void OnEos(object sender, ESPlayer.EosEventArgs eosArgs)
        {
            logger.Error(eosArgs.ToString());

            // Stop and disable all initialized data streams.
            StopAndDisable();

            streamControl?.PlaybackCompleted?.Invoke();
        }

        /// <summary>
        /// ESPlayer event handler. Notifies of an error condition during
        /// playback.
        /// Stops and disables all initialized streams and notifies of an error condition
        /// through PlaybackError event.
        /// </summary>
        /// <param name="sender">Object</param>
        /// <param name="errorArgs">ESPlayer.ErrorArgs</param>
        private void OnError(object sender, ESPlayer.ErrorEventArgs errorArgs)
        {
            var error = errorArgs.ToString();
            logger.Error(error);

            // Stop and disable all initialized data streams.
            StopAndDisable();

            // Perform error notification
            PlaybackError?.Invoke(error);
        }

        /// <summary>
        /// ESPlayer event handler. Issued after calling AsyncPrepare. Stream type
        /// passed as an argument indicates stream for which data transfer has be started.
        /// This effectively starts playback.
        /// </summary>
        /// <param name="esPlayerStreamType">ESPlayer.StreamType</param>
        private void OnReadyToStartStream(ESPlayer.StreamType esPlayerStreamType)
        {
            var streamType = EsPlayerUtils.JuvoStreamType(esPlayerStreamType);

            logger.Info(streamType.ToString());

            dataStreams[(int)streamType].Start();

            logger.Info($"{streamType}: Completed");

        }

        private void OnReadyToSeekStream(ESPlayer.StreamType esPlayerStreamType, TimeSpan time)
        {
            logger.Info($"{esPlayerStreamType}: {time}");
            OnReadyToStartStream(esPlayerStreamType);
        }
        #endregion

        private void WaitForActiveTaskCompletion(CancellationToken token)
        {
            if (activeTaskSerializer.CurrentCount == 0)
                logger.Info("Waiting for active task completion");

            activeTaskSerializer.Wait(token);
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
                await player.PrepareAsync(OnReadyToStartStream).WithCancellation(token);

                logger.Info("Player.PrepareAsync() Completed");

                // This has to be called from UI Thread.
                // It seems impossible to wait for AsyncPrepare completion - doing so
                // prevents AsyncPrepare from completing.
                // Currently, all events passed to UI are re-routed through main thread.
                //
                PlayerInitialized?.Invoke();
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
            }
            finally
            {
                // Unblock serializer
                activeTaskSerializer.Release();
            }
        }

        /// <summary>
        /// Completes data streams.
        /// </summary>
        /// <returns>List<Task> List of data streams being terminated</returns>
        private List<Task> CompleteDataStreams()
        {
            logger.Info("");
            var awaitables = new List<Task>(
                dataStreams.Where(esStream => esStream != null)
                    .Select(esStream => esStream.AwaitCompletion()));

            return awaitables;
        }

        private async Task RestartPlayer(CancellationToken token)
        {
            logger.Info("Restarting ESPlayer");

            try
            {
                // Stop data streams
                StopDataStreams();

                // Stop any underlying async ops
                // 
                var terminations = CompleteDataStreams();

                logger.Info($"Waiting for completion of {terminations.Count} activities");
                try
                {
                    await Task.WhenAll(terminations);
                }
                catch (AggregateException)
                {
                }


                logger.Info("Player Stop");
                player.Stop();

                logger.Info("Re-Setting display window");
                player.SetDisplay(displayWindow);

                logger.Info("Setting configs");

                foreach (var esStream in dataStreams.Where(esStream => esStream != null))
                {
                    var conf = esStream.CurrentConfig;
                    esStream.ClearStreamConfig();
                    esStream.SetStreamConfig(conf);
                }

                await player.PrepareAsync(OnReadyToStartStream).WithCancellation(token);

                logger.Info("Player Start()");
                player.Start();

                logger.Info("All Done");
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
            }
        }

        private async Task StreamSeek(TimeSpan time, CancellationToken token)
        {
            logger.Info(time.ToString());

            try
            {
                // Stop clock generator. During seek, clock API
                // does not work - throws exceptions
                StopClockGenerator();

                // Stop data streams. They will be restarted from
                // SeekAsync handler.
                StopDataStreams();

                await player.SeekAsync(time, OnReadyToSeekStream).WithCancellation(token);

                // Set the clock. 
                // TODO: Remove this once time generation will be removed!
                //
                currentClock = time;

                StartClockGenerator();
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
            }
            finally
            {
                activeTaskSerializer.Release();

                // Always notify UI on seek end regardless of seek status
                // to unblock it for further seeks ops.
                SeekCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Stops all initialized data streams
        /// </summary>
        private void StopDataStreams()
        {
            logger.Info("Stopping all data streams");
            foreach (var esStream in dataStreams.Where(esStream => esStream != null))
                esStream.Stop();
        }

        /// <summary>
        /// Starts all initialized data streams
        /// </summary>
        private void StartDataStreams()
        {
            logger.Info("Starting all data streams");

            // Starts can happen.. when they happen. See no reason to
            // wait for their completion.
            dataStreams.AsParallel().ForAll(esStream => esStream?.Start());
        }

        /// <summary>
        /// Stops and disables all initialized data streams preventing
        /// any further data transfer on those streams.
        /// </summary>
        private void StopAndDisable()
        {
            logger.Info("Stop and Disable all data streams");

            foreach (var esStream in dataStreams.Where(esStream => esStream != null))
            {
                esStream.Stop();
                esStream.Disable();
            }
        }

        /// <summary>
        /// Time generation task. Time is generated from ESPlayer OR auto generated.
        /// Auto generation handles current representation change mechanism where
        /// full stop of ESPlayer is required and no new times are available.
        ///
        /// TODO Remove time generation feature when DashClient SharedBuffers usage
        /// TODO will not be time dependent.
        /// 
        /// </summary>
        /// <returns>Task</returns>
        private async Task GenerateTimeUpdates(CancellationToken token)
        {
            logger.Info($"Clock extractor: Started");

            DateTime delayStart;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    delayStart = DateTime.Now;
                    await Task.Delay(500, token);

                    try
                    {
                        player.GetPlayingTime(out var currentPlayTime);
                        if (currentPlayTime < currentClock)
                            continue;

                        currentClock = currentPlayTime;
                    }
                    catch (InvalidOperationException)
                    {
                        // GetPlayingTime not available after calling 
                        // Player.Stop()->SetWindow()->Before new AddStream()
                        // case: Stream Config Change.
                        // Generate fake clock
                        currentClock += DateTime.Now - delayStart;
                        logger.Info("Clock Generated");
                    }
                    finally
                    {
                        streamControl?.TimeUpdated?.Invoke(currentClock);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
            }
            finally
            {
                logger.Info("Clock extractor: Terminated");
            }
        }

        /// <summary>
        /// Starts clock generation task
        /// </summary>
        private void StartClockGenerator()
        {
            logger.Info("");

            if (!clockGenerator.IsCompleted)
            {
                logger.Warn($"Clock generator running: {clockGenerator.Status}");
                return;
            }

            var stopToken = clockGeneratorCts.Token;

            // Start time updater
            clockGenerator = GenerateTimeUpdates(stopToken);
        }

        /// <summary>
        /// Terminates clock generation task
        /// </summary>
        private void StopClockGenerator()
        {
            logger.Info("");

            if (clockGenerator.IsCompleted)
            {
                logger.Warn($"Clock generator not running: {clockGenerator.Status}");
                return;
            }

            clockGeneratorCts.Cancel();
            clockGeneratorCts.Dispose();
            clockGeneratorCts = new CancellationTokenSource();
        }

        #endregion
    }
}
