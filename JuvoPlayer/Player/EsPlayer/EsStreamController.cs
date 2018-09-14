using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Text;
using JuvoPlayer.Common;
using JuvoLogger;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using ESPlayer = Tizen.TV.Multimedia.ESPlayer;
using System.Threading.Tasks;
using Nito.AsyncEx;

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

        public event StreamReconfigure StreamReconfigure;

        /// <summary>
        /// Timer process and supporting cancellation elements
        /// </summary>
        private Task timeUpdater;
        private CancellationTokenSource stopCts;
        private CancellationToken stopToken;

        /// <summary>
        /// Returns configuration status of all underlying streams.
        /// True - all initialized streams are configures
        /// False - at least one underlying stream is not configured
        /// </summary>
        private bool AllStreamsConfigured => dataStreams.All(streamEntry =>
            streamEntry?.IsConfigured ?? true);

        /// <summary>
        /// Current clock time - used for fake clock generation
        /// </summary>
        private TimeSpan currentClock;

        private bool inStreamReconfiguration;

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

            stopCts?.Cancel();
            stopCts?.Dispose();

            // Dispose of individual streams.
            dataStreams.AsParallel().ForAll((esStream) => DisposeStream(ref esStream));

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

        private void DisposeStream(ref EsStream stream)
        {
            if (stream == null)
                return;

            stream.ReconfigureStream -= OnStreamReconfigure;
            stream.Dispose();
            stream = null;
        }
        /// <summary>
        /// Initializes a stream to be used with stream controller
        /// Must be called before usage of the stream with stream controller
        /// </summary>
        /// <param name="stream">Common.StreamType</param>
        public void Initialize(Common.StreamType stream)
        {
            logger.Info(stream.ToString());

            // Grab "old" stream 
            //
            var esStream = dataStreams[(int)stream];

            // Create new data stream in its place
            //
            dataStreams[(int)stream] = new EsStream(player, stream);
            dataStreams[(int)stream].ReconfigureStream += OnStreamReconfigure;

            // Remove previous data if existed in first place...
            //
            DisposeStream(ref esStream);
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
                PrepareStream();

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

            if (!player.Start())
            {
                logger.Error("ESPlayer.Start() failed");
                return;
            }

            StartDataStreams();
            StartClockGenerator();
        }

        /// <summary>
        /// Resumes playback on all initialized streams. Playback had to be
        /// paused.
        /// </summary>
        public void Resume()
        {
            logger.Info("");

            if (!player.Resume())
            {
                logger.Error("ESPlayer.Resume() failed");
                return;
            }

            StartDataStreams();
            StartClockGenerator();
        }

        /// <summary>
        /// Pauses playback on all initialized streams. Playback had to be played.
        /// </summary>
        public void Pause()
        {
            logger.Info("");

            if (!player.Pause())
            {
                logger.Error("ESPlayer.Pause() failed");
                return;
            }

            StopDataStreams();
            StopClockGenerator();
        }

        /// <summary>
        /// Stops playback on all initialized streams.
        /// </summary>
        public void Stop()
        {
            logger.Info("");

            if (!player.Stop())
            {
                logger.Error("ESPlayer.Stop() failed");
                return;
            }

            StopDataStreams();
            StopClockGenerator();
        }
        #endregion

        #region Private Methods

        #region Internal EsPlayer event handlers

        private void OnStreamReconfigure(BufferConfigurationPacket configPacket)
        {
            logger.Info("");
            inStreamReconfiguration = true;

            DoStreamChange(configPacket);
        }
        private async Task DoStreamChange(BufferConfigurationPacket configPacket)
        {
            await Task.CompletedTask;

            logger.Info("");

            logger.Error("***\n*** STREAM CHANGE CURRENTLY NOT SUPPORTED. Playback will terminate\n***");
            inStreamReconfiguration = true;

            StopDataStreams();
            StopClockGenerator();

            return;
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
            var streamType = EsPlayerUtils.JuvoStreamType(esPlayerStreamType);

            logger.Info(streamType.ToString());

            await Task.Factory.StartNew(() => dataStreams[(int)streamType].Start(), TaskCreationOptions.DenyChildAttach);

            logger.Info($"{streamType}: Completed");

        }
        #endregion

        /// <summary>
        /// Method executes PrepareAsync on ESPlayer. On success, notifies
        /// event PlayerInitialized. At this time player is ALREADY PLAYING
        /// </summary>
        /// <returns>Task</returns>
        private async Task<bool> PrepareStream()
        {
            logger.Info("");

            var prepRes = await player.PrepareAsync(OnReadyToStartStream);

            if (!prepRes)
            {
                logger.Error("Player.PrepareAsync() Failed");
                StopAndDisable();
                return false;
            }

            logger.Info("Player.PrepareAsync() Completed");

            if (inStreamReconfiguration)
            {
                logger.Info("Reconfiguration mode. PlayerInitialized won't be notified");
                return true;
            }

            // This has to be called from UI Thread.
            // It seems impossible to wait for AsyncPrepare completion - doing so
            // prevents AsyncPrepare from completing.
            // Currently, all events passed to UI are re-routed through main thread.
            //
            logger.Info("Initial configuration mode. PlayerInitialized notification");
            PlayerInitialized?.Invoke();
            return true;
        }

        /// <summary>
        /// Stops all initialized data streams
        /// </summary>
        private void StopDataStreams()
        {
            logger.Info("Stopping all data streams");
            dataStreams.AsParallel().ForAll(esStream => esStream?.Stop());
        }

        /// <summary>
        /// Starts all initialized data streams
        /// </summary>
        private void StartDataStreams()
        {
            logger.Info("Starting all data streams");
            dataStreams.AsParallel().ForAll(esStream => esStream?.Start());
        }

        /// <summary>
        /// Stops and disables all initialized data streams preventing
        /// any further data transfer on those streams.
        /// </summary>
        private void StopAndDisable()
        {
            logger.Info("Stop and Disable all data streams");

            dataStreams.AsParallel().ForAll(esStream =>
            {
                esStream?.Stop();
                esStream?.Disable();
            });
        }

        /// <summary>
        /// Time generation task
        /// </summary>
        /// <returns>Task</returns>
        private async Task GenerateTimeUpdates()
        {
            logger.Info("Starting clock extractor (GENERATED)");

            while (!stopToken.IsCancellationRequested)
            {
                var delayStart = DateTime.Now;
                await Task.Delay(500, stopToken);

                currentClock += DateTime.Now - delayStart;
                streamControl?.TimeUpdated?.Invoke(currentClock);
            }
        }

        /// <summary>
        /// Starts clock generation task
        /// </summary>
        private void StartClockGenerator()
        {
            if (stopCts != null)
            {
                logger.Warn("Clock cannot be started. stopCts not cleared");
                return;
            }

            stopCts = new CancellationTokenSource();
            stopToken = stopCts.Token;

            // Start time updater
            timeUpdater = GenerateTimeUpdates();
        }

        /// <summary>
        /// Terminates clock generation task
        /// </summary>
        private void StopClockGenerator()
        {
            stopCts.Cancel();
            stopCts.Dispose();
            stopCts = null;
        }

        #endregion
    }
}
