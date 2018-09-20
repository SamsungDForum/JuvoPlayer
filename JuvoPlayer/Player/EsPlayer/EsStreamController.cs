// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
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

        /// <summary>
        /// Timer process and supporting cancellation elements
        /// </summary>
        private Task clockGenerator = Task.CompletedTask;
        private CancellationTokenSource stopCts;

        /// <summary>
        /// Returns configuration status of all underlying streams.
        /// True - all initialized streams are configures
        /// False - at least one underlying stream is not configured
        /// </summary>
        private bool AllStreamsConfigured => dataStreams.All(streamEntry =>
            streamEntry?.IsConfigured ?? true);

        /// <summary>
        /// Placeholder for current async activity issued to ESPlayer.
        /// Initialized with Task.Task.Completed to avoid null checks.
        ///
        /// TODO: Add current state checks to prevent launch of multiple async ops.
        /// TODO: i.e. async operation in progress / not in progress.
        /// 
        /// </summary>
        private Task activeTask = Task.CompletedTask;

        #region Instance Support
        /// <summary>
        /// Obtains an instance of Stream Controller
        /// </summary>
        /// <param name="esPlayer">ESPlayer</param>
        /// <returns>EsStreamController instance</returns>
        public static EsStreamController GetInstance(ESPlayer.ESPlayer esPlayer)
        {
            lock (InstanceLock)
            {
                if (streamControl != null)
                    return streamControl;

                streamControl = new EsStreamController();
                streamControl.ConfigureInstance(esPlayer);

                return streamControl;
            }
        }

        /// <summary>
        /// Configure newly created instance
        /// </summary>
        /// <param name="esPlayer">ESPlayer</param>
        private void ConfigureInstance(ESPlayer.ESPlayer esPlayer)
        {
            // Initialize player & attach event handlers
            player = esPlayer;
            player.EOSEmitted += OnEos;
            player.ErrorOccurred += OnError;

            dataStreams = new EsStream[(int)StreamType.Count];
        }

        /// <summary>
        /// Unconfigures and instance
        /// </summary>
        private void UnconfigureInstance()
        {
            logger.Info("");

            // Detach event handlers
            player.EOSEmitted -= OnEos;
            player.ErrorOccurred -= OnError;

            // Stop clock
            StopClockGenerator();

            // Dispose of individual streams.
            foreach (var esStream in dataStreams.Where(esStream => esStream != null))
            {
                esStream.ReconfigureStream -= OnStreamReconfigure;
                esStream.Dispose();
            }

            // Nullify references
            dataStreams = Enumerable.Repeat<EsStream>(null, dataStreams.Length).ToArray();
            streamControl = null;
        }

        /// <summary>
        /// Releases instance resources. 
        /// </summary>
        public static void FreeInstance()
        {
            lock (InstanceLock)
            {
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
                // Not waiting for PrepareStream is intentional.
                // Prepare fails otherwise
                //
                activeTask = StreamPrepare();

            }
            catch (NullReferenceException)
            {
                // packetQueue can hold ALL StreamTypes, but not all of them
                // have to be supported. 
                logger.Warn($"Uninitialized Stream Type {streamType}");
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

            activeTask = StreamSeek(time);

        }
        #endregion

        #region Private Methods

        #region Internal EsPlayer event handlers

        private void OnStreamReconfigure(BufferConfigurationPacket configPacket)
        {
            logger.Info("");

            activeTask = StreamChange(configPacket);
        }

        #endregion
        #region ESPlayer event handlers    
        /// <summary>
        /// ESPlayer event handler. Notifies that ALL played streams have
        /// completed playback (EOS was sent on all of them)
        /// Methods 
        /// </summary>
        /// <param name="sender">Object</param>
        /// <param name="eosArgs">ESPlayer.EosArgs</param>
        private void OnEos(object sender, ESPlayer.EosArgs eosArgs)
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
        private void OnError(object sender, ESPlayer.ErrorArgs errorArgs)
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
        private async void OnReadyToStartStream(ESPlayer.StreamType esPlayerStreamType)
        {
            await Task.CompletedTask;

            var streamType = EsPlayerUtils.JuvoStreamType(esPlayerStreamType);

            logger.Info(streamType.ToString());

            Task.Factory.StartNew(() => dataStreams[(int)streamType].Start(), TaskCreationOptions.DenyChildAttach);

            logger.Info($"{streamType}: Completed");

        }

        private async void OnReadyToSeekStream(ESPlayer.StreamType esPlayerStreamType, TimeSpan time)
        {
            await Task.CompletedTask;

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
        private async Task StreamPrepare()
        {
            logger.Info("");

            try
            {
                await player.PrepareAsync(OnReadyToStartStream);

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
        }

        private async Task StreamChange(BufferConfigurationPacket configPacket)
        {
            logger.Info("");

            logger.Error("***\n*** STREAM CHANGE CURRENTLY HAS ISSUES. Video may not be seen\n***");

            try
            {
                logger.Info("Stopping streams");
                StopDataStreams();
                StopClockGenerator();

                logger.Info("Player Stop");
                player.Stop();

                logger.Info("Setting configs");

                foreach (var esStream in dataStreams.Where(esStream => esStream != null))
                {
                    var conf = esStream.CurrentConfig;
                    esStream.ClearStreamConfig();
                    esStream.SetStreamConfig(conf);
                }

                StartClockGenerator();

                await player.PrepareAsync(OnReadyToStartStream);

                logger.Info("Player Start()");
                player.Start();

                logger.Info("All Done");
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
            }
        }

        private async Task StreamSeek(TimeSpan time)
        {
            logger.Info(time.ToString());

            try
            {
                StopClockGenerator();
                StopDataStreams();

                await player.SeekAsync(time, OnReadyToSeekStream);

                StartClockGenerator();
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error(ioe.Message);
            }
            catch (Exception e)
            {
                logger.Error(e.Message);
            }
            finally
            {
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
        /// Time generation task
        /// </summary>
        /// <returns>Task</returns>
        private async Task GenerateTimeUpdates(CancellationToken token)
        {
            logger.Info($"Clock extractor: Started");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(500, token);

                    player.GetPlayingTime(out var playTime);

                    streamControl?.TimeUpdated?.Invoke(playTime);
                }
            }
            catch (InvalidOperationException ioe)
            {
                logger.Error($"GetPlayingTime failed: {ioe.Message}");
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

            stopCts = new CancellationTokenSource();
            var stopToken = stopCts.Token;

            // Start time updater
            clockGenerator = GenerateTimeUpdates(stopToken);
        }

        /// <summary>
        /// Terminates clock generation task
        /// </summary>
        private void StopClockGenerator()
        {
            logger.Info("");

            stopCts?.Cancel();
            stopCts?.Dispose();
        }

        #endregion
    }
}
