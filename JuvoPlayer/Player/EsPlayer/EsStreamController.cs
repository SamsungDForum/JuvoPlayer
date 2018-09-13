using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    class EsStreamController
    {
        private static ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        internal static EsStreamController StreamControl;
        internal static EsStream[] DataStreams;
        internal static ESPlayer.ESPlayer Player;

        private static readonly Object instanceLock = new Object();

        public event TimeUpdated TimeUpdated;
        public event PlaybackError PlaybackError;
        public event PlaybackCompleted PlaybackCompleted;

        public event PlayerInitialized PlayerInitialized;

        internal static Task timeUpdater;
        private static CancellationTokenSource stopCts;
        private static CancellationToken stopToken;

        private bool allStreamsConfigured => DataStreams.All(streamEntry =>
            streamEntry?.IsConfigured ?? true);

        private static TimeSpan currentClock;

        #region Instance Support
        public static EsStreamController GetInstance(ESPlayer.ESPlayer esPlayer)
        {
            lock (instanceLock)
            {
                if (StreamControl != null)
                    return StreamControl;

                // Initialize player & attach event handlers
                Player = esPlayer;
                Player.EOSEmitted += OnEos;
                Player.ErrorOccurred += OnError;

                StreamControl = new EsStreamController();
                DataStreams = new EsStream[(int)StreamType.Count];

                return StreamControl;
            }
        }

        public void Initialize(Common.StreamType stream)
        {
            logger.Info(stream.ToString());

            // Grab "old" stream 
            //
            var esStream = DataStreams[(int)stream];

            // Create new queue in its place
            //
            DataStreams[(int)stream] = new EsStream(Player, stream);

            // Remove previous data if existed in first place...
            //
            esStream?.Dispose();
        }

        public static void FreeInstance()
        {
            lock (instanceLock)
            {
                if (StreamControl == null)
                    return;

                logger.Info("");

                // Detach event handlers
                Player.EOSEmitted -= OnEos;
                Player.ErrorOccurred -= OnError;

                stopCts?.Cancel();
                stopCts?.Dispose();

                // Dispose of individual streams.
                DataStreams.AsParallel().ForAll((esStream) => esStream?.Dispose());

                StreamControl = null;
            }
        }
        #endregion

        #region Public API
        public void SetStreamConfiguration(StreamConfig config)
        {
            try
            {
                var stream = DataStreams[(int)config.StreamType()];
                stream.SetStreamConfig(BufferConfigurationPacket.Create(config));

                // Check if all initialized streams are configured
                if (!allStreamsConfigured)
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
                logger.Warn($"Uninitialized Stream Type {config.StreamType()}");
            }
            catch (InvalidOperationException)
            {
                // Queue has been marked as completed 
                logger.Warn($"Data queue terminated for stream: {config.StreamType()}");
            }
        }

        public void Play()
        {
            logger.Info("");

            if (!allStreamsConfigured)
            {
                logger.Info("Initialized streams are not configured. Play Aborted");
                return;
            }

            if (!Player.Start())
            {
                logger.Error("ESPlayer.Start() failed");
                return;
            }

            StartDataStreams();
            StartClockGenerator();
        }

        public void Resume()
        {
            logger.Info("");

            if (!Player.Resume())
            {
                logger.Error("ESPlayer.Resume() failed");
                return;
            }

            StartDataStreams();
            StartClockGenerator();
        }

        public void Pause()
        {
            logger.Info("");

            if (!Player.Pause())
            {
                logger.Error("ESPlayer.Pause() failed");
                return;
            }

            StopDataStreams();
            StopClockGenerator();
        }

        public void Stop()
        {
            logger.Info("");

            if (!Player.Stop())
            {
                logger.Error("ESPlayer.Stop() failed");
                return;
            }

            StopDataStreams();
            StopClockGenerator();
        }


        #endregion

        #region Private Methods
        #region ESPlayer event handlers    
        private static void OnEos(Object sender, ESPlayer.EosArgs eosArgs)
        {
            logger.Error(eosArgs.ToString());
            StopDataStreams();
            StreamControl?.PlaybackCompleted?.Invoke();
        }

        private static void OnError(Object sender, ESPlayer.ErrorArgs errorArgs)
        {
            var error = errorArgs.ToString();
            logger.Error(error);

            // Stop playback on all valid streams
            DataStreams.AsParallel().ForAll(esStream =>
            {
                esStream?.Stop();
                esStream?.Disable();
            });

            // Perform error notification
            StreamControl?.PlaybackError?.Invoke(error);
        }
        #endregion

        private static async Task GenerateTimeUpdates()
        {

            logger.Info("Starting clock extractor (GENERATED)");

            while (!stopToken.IsCancellationRequested)
            {
                var delayStart = DateTime.Now;
                await Task.Delay(500, stopToken);

                currentClock += DateTime.Now - delayStart;
                StreamControl?.TimeUpdated?.Invoke(currentClock);
            }
        }

        private static void StopDataStreams()
        {
            logger.Info("Stopping all data streams");
            DataStreams.AsParallel().ForAll(esStream => esStream?.Stop());
        }

        private static void StartDataStreams()
        {
            logger.Info("Starting all data streams");
            DataStreams.AsParallel().ForAll(esStream => esStream?.Start());
        }

        private static void StartClockGenerator()
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

        private static void StopClockGenerator()
        {
            stopCts.Cancel();
            stopCts.Dispose();
            stopCts = null;
        }

        private static void OnReadyToStartStream(ESPlayer.StreamType esPlayerStreamType)
        {
            var streamType = EsPlayerUtils.JuvoStreamType(esPlayerStreamType);
            DataStreams[(int)streamType].Start();
        }

        private async Task PrepareStream()
        {
            logger.Info("");

            var prepareRes = await Player.PrepareAsync(async esStreamType =>
                OnReadyToStartStream(esStreamType));

            if (!prepareRes)
            {
                logger.Error("Player.PrepareAsync() Failed");
                return;
            }

            // This has to be called from UI Thread.
            // It seems impossible to wait for AsyncPrepare completion - doing so
            // prevents AsyncPrepare from completing.
            // Currently, all events passed to UI are re-routed through main thread.
            //
            PlayerInitialized?.Invoke();

            logger.Info("Player.PrepareAsync() Completed");

        }
        #endregion
    }
}
