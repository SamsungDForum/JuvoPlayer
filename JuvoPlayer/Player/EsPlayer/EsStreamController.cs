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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using JuvoPlayer.Common;
using JuvoLogger;
using System.Linq;
using System.Threading;
using ESPlayer = Tizen.TV.Multimedia.ESPlayer;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace JuvoPlayer.Player.EsPlayer
{
    /// <summary>
    /// Controls transfer stream operation
    /// </summary>
    internal sealed class EsStreamController : IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        // Instance reference & creation lock
        private readonly object initializeLock = new object();


        // Reference to all data streams representing transfer of individual
        // stream data and data storage
        private EsStream[] dataStreams;
        private EsPlayerPacketStorage packetStorage;

        // Reference to ESPlayer & associated window
        private ESPlayer.ESPlayer player;
        private ElmSharp.Window displayWindow;

        // event callbacks
        public event TimeUpdated TimeUpdated;
        public event PlaybackError PlaybackError;
        public event PlaybackCompleted PlaybackCompleted;
        public event PlayerInitialized PlayerInitialized;
        public event SeekCompleted SeekCompleted;
        public event BufferStatus BufferStatus;
        public event PlaybackRestart PlaybackRestart;

        // Timer process and supporting cancellation elements for clock extraction
        // and generation
        private Task clockGenerator = Task.CompletedTask;
        private CancellationTokenSource clockGeneratorCts;
        private TimeSpan currentClock;

        // Returns configuration status of all underlying streams.
        // True - all initialized streams are configures
        // False - at least one underlying stream is not configured
        private bool AllStreamsConfigured => dataStreams.All(streamEntry =>
            streamEntry?.IsConfigured ?? true);


        // Termination & serialization objects for async operations.
        private CancellationTokenSource activeTaskCts = new CancellationTokenSource();
        private AsyncLock asyncOpSerializer = new AsyncLock();

        #region Public API
        public void Initialize(Common.StreamType stream)
        {

            lock (initializeLock)
            {
                logger.Info(stream.ToString());

                if (dataStreams[(int)stream] != null)
                {
                    throw new ArgumentException($"Stream {stream} already initialized");
                }

                // Create new data stream in its place
                //
                dataStreams[(int)stream] = new EsStream(player, stream, packetStorage);
                dataStreams[(int)stream].ReconfigureStream += OnStreamReconfigure;
            }
        }

        public EsStreamController(EsPlayerPacketStorage storage)
        {
            // Create player & window
            displayWindow =
                EsPlayerUtils.CreateWindow(EsPlayerUtils.DefaultWindowWidth, EsPlayerUtils.DefaultWindowHeight);

            player = new ESPlayer.ESPlayer();

            player.Open();
            player.SetTrustZoneUse(true);
            player.SetDisplay(displayWindow);

            packetStorage = storage;

            // Create placeholder to data streams.
            dataStreams = new EsStream[(int)StreamType.Count];

            // Create storage places
            //attach event handlers
            player.EOSEmitted += OnEos;
            player.ErrorOccurred += OnError;
            player.BufferStatusChanged += OnBufferStatusChanged;
        }

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
                var pushResult = dataStreams[(int)streamType].SetStreamConfig(configPacket);

                // Configuration queued. Do not prepare stream :)
                if (pushResult == EsStream.SetStreamConfigResult.ConfigQueued)
                    return;


                // Check if all initialized streams are configured
                if (!AllStreamsConfigured)
                    return;

                var token = activeTaskCts.Token;
                StreamPrepare(token);


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

                EnableTransfer();
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

                EnableTransfer();
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

                DisableTransfer();
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

                DisableTransfer();
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

                StreamSeek(time, token);

            }
            catch (OperationCanceledException)
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
                var token = activeTaskCts.Token;
                RestartPlayer(token);
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
            var juvoStream = buffArgs.StreamType.JuvoStreamType();
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
            DisableTransfer();
            DisableInput();

            PlaybackCompleted?.Invoke();
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
            DisableTransfer();
            DisableInput();

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
            var streamType = esPlayerStreamType.JuvoStreamType();

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
                    await player.PrepareAsync(OnReadyToStartStream).WithCancellation(token);
                }

                logger.Info("Player.PrepareAsync() Completed");

                PlayerInitialized?.Invoke();
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
                PlaybackError?.Invoke(ioe.Message);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
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
                using (await asyncOpSerializer.LockAsync(token))
                {
                    // Stop data streams
                    DisableTransfer();

                    // Stop any underlying async ops
                    // 
                    var terminations = CompleteDataStreams();

                    logger.Info($"Waiting for completion of {terminations.Count} activities");
                    await Task.WhenAll(terminations).WithCancellation(token);

                    token.ThrowIfCancellationRequested();

                    player.GetPlayingTime(out var currentPlayTime);
                    PlaybackRestart?.Invoke(currentPlayTime);

                    logger.Info("Player Stop");
                    player.Stop();

                    logger.Info("Re-Setting display window");
                    player.SetDisplay(displayWindow);

                    player.SetTrustZoneUse(true);

                    logger.Info("Setting configs");


                    foreach (var esStream in dataStreams.Where(esStream => esStream != null))
                        esStream.ResetStreamConfig();

                    await player.PrepareAsync(OnReadyToStartStream).WithCancellation(token);

                    logger.Info("Player Start()");
                    player.Start();

                    logger.Info("All Done");
                }
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
                PlaybackError?.Invoke(ioe.Message);
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
                using (await asyncOpSerializer.LockAsync(token))
                {
                    // Stop data streams. They will be restarted from
                    // SeekAsync handler.
                    DisableTransfer();

                    // Stop clock generator. During seek, clock API
                    // does not work - throws exceptions
                    StopClockGenerator();

                    // Make sure data transfer is stopped!
                    // SeekAsync behaves unpredictably when data transfer to player
                    // is occuring while SeekAsync gets called
                    var terminations = CompleteDataStreams();
                    terminations.Add(clockGenerator);

                    logger.Info($"Waiting for completion of {terminations.Count} activities");

                    await Task.WhenAll(terminations).WithCancellation(token);

                    await player.SeekAsync(time, OnReadyToSeekStream).WithCancellation(token);

                    // Set the clock. 
                    // TODO: Remove this once time generation will be removed!
                    //
                    currentClock = time;

                    StartClockGenerator();
                }
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
                PlaybackError?.Invoke(ioe.Message);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Operation Cancelled");
            }
            finally
            {
                // Always notify UI on seek end regardless of seek status
                // to unblock it for further seeks ops.
                SeekCompleted?.Invoke();
            }
        }

        /// <summary>
        /// Stops all initialized data streams preventing transfer of data from associated
        /// data queue to underlying player. When stopped, stream can still accept new data
        /// </summary>
        private void DisableTransfer()
        {
            logger.Info("Stopping all data streams");
            foreach (var esStream in dataStreams.Where(esStream => esStream != null))
                esStream.Stop();
        }

        /// <summary>
        /// Starts all initialized data streams allowing transfer of data from associated
        /// data queues to underlying player
        /// </summary>
        private void EnableTransfer()
        {
            logger.Info("Starting all data streams");

            // Starts can happen.. when they happen. See no reason to
            // wait for their completion.
            foreach (var esStream in dataStreams.Where(esStream => esStream != null))
                esStream.Start();
        }

        /// <summary>
        /// Disables all initialized data streams preventing
        /// any further new input collection
        /// </summary>
        private void DisableInput()
        {
            logger.Info("Stop and Disable all data streams");

            foreach (var esStream in dataStreams.Where(esStream => esStream != null))
                esStream.Disable();

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

            var delayStart = DateTime.Now;
            try
            {
                while (!token.IsCancellationRequested)
                {
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
                        TimeUpdated?.Invoke(currentClock);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                logger.Info("Operation Cancelled");
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

            clockGeneratorCts?.Dispose();
            clockGeneratorCts = new CancellationTokenSource();
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
        }

        #endregion

        #region Dispose support
        private bool isDisposed;
        public void Dispose()
        {
            if (isDisposed)
                return;

            logger.Info("");

            logger.Info("Data Streams shutdown");
            // Stop data streams
            DisableTransfer();

            // Detach event handlers
            logger.Info("Detaching event handlers");
            player.EOSEmitted -= OnEos;
            player.ErrorOccurred -= OnError;
            player.BufferStatusChanged -= OnBufferStatusChanged;

            // Stop clock & async operations
            logger.Info("Clock/AsyncOps shutdown");
            activeTaskCts.Cancel();
            StopClockGenerator();

            // Wait for cancellations to be signaled to assure any listening tasks
            // will be in cancel mode before proceeding further.
            logger.Info($"Waiting for cancellations to be signaled");
            WaitHandle[] waitFor;
            if (clockGeneratorCts != null)
            {
                waitFor = new WaitHandle[]
                {
                    activeTaskCts.Token.WaitHandle,
                    clockGeneratorCts?.Token.WaitHandle
                };
            }
            else
            {
                waitFor = new WaitHandle[]
                {
                    activeTaskCts.Token.WaitHandle,
                };
            }

            WaitHandle.WaitAll(waitFor);

            // Dispose of individual streams.
            logger.Info("Data Streams shutdown");
            foreach (var esStream in dataStreams.Where(esStream => esStream != null))
            {
                esStream.ReconfigureStream -= OnStreamReconfigure;
                esStream.Dispose();
            }

            // Shut down player
            logger.Info("ESPlayer shutdown");

            // Don't call Close. Dispose does that. Otherwise exceptions will fly
            player.Dispose();
            EsPlayerUtils.DestroyWindow(ref displayWindow);
            player = null;

            // Clean up internal object
            activeTaskCts.Dispose();
            activeTaskCts = null;
            clockGeneratorCts?.Dispose();
            clockGeneratorCts = null;

            asyncOpSerializer = null;

            dataStreams = null;
            packetStorage = null;

            isDisposed = true;

        }
        #endregion

    }
}
