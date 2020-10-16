/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using ESPlayer = Tizen.TV.Multimedia;
using StreamType = JuvoPlayer.Common.StreamType;

namespace JuvoPlayer.Player.EsPlayer
{
    internal class UnsupportedStreamException : Exception
    {
        public UnsupportedStreamException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Packet submit exception. Raised when packet push to ESPlayer failed in a terminal
    /// way.
    /// </summary>
    internal class PacketSubmitException : Exception
    {
        public ESPlayer.SubmitStatus SubmitStatus { get; internal set; }

        public PacketSubmitException(string message, ESPlayer.SubmitStatus status) : base(message)
        {
            SubmitStatus = status;
        }
    }

    /// <summary>
    /// Class representing and individual stream being transferred
    /// </summary>
    internal class EsStream : IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        /// Delegate holding PushConfigMethod. Different for Audio and Video
        private delegate void StreamConfigure(StreamConfig streamConfig);
        private readonly StreamConfigure PushStreamConfig;

        // Reference to internal EsPlayer objects
        private ESPlayer.ESPlayer player;
        private readonly EsPlayerPacketStorage packetStorage;

        // transfer task & cancellation token
        private volatile Task activeTask = Task.CompletedTask;
        private CancellationTokenSource transferCts;
        private CancellationTokenSource linkedCts;

        // Stream type for this instance to EsStream.
        private readonly StreamType streamType;

        public StreamType GetStreamType() => streamType;

        // Buffer configuration and supporting info
        public StreamConfig Configuration { get; set; }

        public bool HaveConfiguration => Configuration != null;

        // Events
        private readonly Subject<string> playbackErrorSubject = new Subject<string>();

        private Packet currentPacket;

        private readonly Synchronizer _dataSynchronizer;
        private readonly PlayerClockProvider _playerClock;

        private readonly ReplaySubject<bool> _bufferingSubject = new ReplaySubject<bool>(1);
        private readonly Subject<Type> _packetProcessed = new Subject<Type>();

        public IObservable<bool> StreamBuffering()
        {
            return _bufferingSubject.AsObservable()
                .DistinctUntilChanged(); // State gets updated "per packet". Only change is required.
        }

        public IObservable<string> PlaybackError()
        {
            return playbackErrorSubject.AsObservable();
        }

        public IObservable<Type> PacketProcessed()
        {
            return _packetProcessed.AsObservable().Do(_ => logger.Info($"{streamType}: Packet Done"));
        }
        #region Public API

        public EsStream(StreamType type, EsPlayerPacketStorage storage, Synchronizer synchronizer, PlayerClockProvider playerClock)
        {
            streamType = type;
            packetStorage = storage;
            _dataSynchronizer = synchronizer;
            _dataSynchronizer.Initialize(streamType);
            _playerClock = playerClock;

            switch (streamType)
            {
                case StreamType.Audio:
                    PushStreamConfig = PushAudioConfig;
                    break;
                case StreamType.Video:
                    PushStreamConfig = PushVideoConfig;
                    break;
                default:
                    throw new ArgumentException($"Stream Type {streamType} is unsupported");
            }
        }

        /// <summary>
        /// Sets the player to be used by EsStream
        /// </summary>
        /// <param name="newPlayer">ESPlayer</param>
        public void SetPlayer(ESPlayer.ESPlayer newPlayer)
        {
            logger.Info($"{streamType}");
            player = newPlayer;
        }

        /// <summary>
        /// Sets Stream configuration
        /// If no argument is provided, current configuration will be pushed.
        /// Otherwise provided configuration will be set as current and pushed.
        /// <param name="config">StreamConfig</param>
        /// </summary>
        public void SetStreamConfiguration(StreamConfig config = null)
        {
            logger.Info($"{streamType}: Using {(config == null ? "stored" : "provided")} configuration");

            if (config == null)
            {
                if (Configuration == null)
                    throw new ArgumentNullException(nameof(Configuration), "Current configuration is null");
            }
            else
            {
                Configuration = config;
            }

            PushStreamConfig(Configuration);
        }

        /// <summary>
        /// Public API for starting data transfer
        /// </summary>
        public void Start(CancellationToken token)
        {
            EnableTransfer(token);
        }

        /// <summary>
        /// Public API for stopping data transfer
        /// </summary>
        public void Stop()
        {
            DisableTransfer();
        }

        /// <summary>
        /// Awaitable function. Will return when a running task terminates.
        /// </summary>
        /// <returns></returns>
        public Task GetActiveTask()
        {
            logger.Info($"{streamType}: {activeTask.Status}");
            return activeTask;
        }

        public void EmptyStorage()
        {
            packetStorage.Disable(streamType);


            currentPacket?.Dispose();
            currentPacket = null;

            packetStorage.Empty(streamType);
        }

        /// <summary>
        /// Disables packet queue input. No new packets will be added
        /// to packet queue.
        /// to be pushed to the player.
        /// </summary>
        public void DisableInput()
        {
            logger.Info($"{streamType}:");
            packetStorage.Disable(streamType);
        }

        /// <summary>
        /// Enables packet queue input. New packets can be appended to packet queue.
        /// to be pushed to the player.
        /// </summary>
        public void EnableInput()
        {
            logger.Info($"{streamType}:");
            packetStorage.Enable(streamType);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Audio Configuration push method.
        /// </summary>
        /// <param name="streamConfig">Common.StreamConfig</param>
        private void PushAudioConfig(StreamConfig streamConfig)
        {
            logger.Info("");
            var streamInfo = streamConfig.ESAudioStreamInfo();

            logger.Info(streamInfo.DumpConfig());

            player.SetStream(streamInfo);

            logger.Info($"{streamType}: Stream configuration set");
        }

        /// <summary>
        /// Video Configuration push method.
        /// </summary>
        /// <param name="streamConfig">Common.StreamConfig</param>
        private void PushVideoConfig(StreamConfig streamConfig)
        {
            logger.Info("");

            var streamInfo = streamConfig.ESVideoStreamInfo();

            logger.Info(streamInfo.DumpConfig());

            player.SetStream(streamInfo);

            logger.Info($"{streamType}: Stream configuration set");
        }

        /// <summary>
        /// Starts data transfer, if not already running, by starting
        /// transfer task.
        /// </summary>
        private void EnableTransfer(CancellationToken token)
        {
            logger.Info($"{streamType}:");

            // No cancellation requested = task not stopped
            if (!activeTask.IsCompleted)
            {
                logger.Info($"{streamType}: Already running: {activeTask.Status}");
                return;
            }

            transferCts?.Dispose();
            transferCts = new CancellationTokenSource();
            linkedCts?.Dispose();
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(transferCts.Token, token);

            activeTask = Task.Run(async () => await TransferTask(linkedCts.Token), linkedCts.Token);
        }

        /// <summary>
        /// Stops data transfer, if already running, by terminating transfer task.
        /// </summary>
        private void DisableTransfer()
        {
            logger.Info($"{streamType}:");
            transferCts?.Cancel();
        }

        private async ValueTask<bool> ProcessPacket(Packet packet, CancellationToken transferToken)
        {
            var continueProcessing = true;

            switch (packet)
            {
                case EOSPacket eosPacket:
                    await PushEosPacket(eosPacket, transferToken);
                    continueProcessing = false;
                    break;

                case BufferConfigurationPacket bufferConfigPacket:
                    var oldConfiguration = Configuration;
                    Configuration = bufferConfigPacket.Config;
                    if (!oldConfiguration.IsCompatible(Configuration))
                    {
                        logger.Error($"{streamType}: Incompatible configuration");
                        playbackErrorSubject.OnNext("Incompatible configuration");
                        continueProcessing = false;
                    }

                    break;

                case EncryptedPacket encryptedPacket:
                    await PushEncryptedPacket(encryptedPacket, transferToken);
                    break;

                case Packet dataPacket:
                    await PushUnencryptedPacket(dataPacket, transferToken);
                    break;

                default:
                    throw new ArgumentException($"{streamType}: Unsupported packet type {packet.GetType()}");
            }

            return continueProcessing;
        }

        /// <summary>
        /// Transfer task. Retrieves data from underlying storage and pushes it down
        /// to ESPlayer
        /// </summary>
        /// <param name="token">CancellationToken</param>
        private async Task TransferTask(CancellationToken token)
        {
            logger.Info($"{streamType}: Started");
            _bufferingSubject.OnNext(true);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var shouldContinue = await ProcessNextPacket(token);
                    if (!shouldContinue)
                        break;

                    await _dataSynchronizer.Synchronize(streamType, token);
                }
            }
            catch (InvalidOperationException e)
            {
                logger.Error(e, $"{streamType}: Stream completed");
            }
            catch (OperationCanceledException)
            {
                logger.Info($"{streamType}: Transfer cancelled");
            }
            catch (PacketSubmitException pse)
            {
                logger.Error(pse, $"{streamType}: Submit Error " + pse.SubmitStatus);
                DisableInput();
            }
            catch (DrmException drme)
            {
                logger.Error(drme, $"{streamType}: Decrypt Error");
                DisableInput();
                playbackErrorSubject.OnNext("Playback Error");
            }
            catch (Exception e)
            {
                // Dump unhandled exception. Running as a task so they will not be reported.
                logger.Error(e, $"{streamType}");
                DisableInput();
                playbackErrorSubject.OnNext("Playback Error");
            }
            finally
            {
                logger.Info($"{streamType}: Terminated. ");
            }
        }

        private async Task<bool> ProcessNextPacket(CancellationToken token)
        {
            if (currentPacket == null)
            {
                var displayBuffering = packetStorage.Count(streamType) == 0 &&
                                       (_playerClock.Clock - _dataSynchronizer.GetPts(streamType)).Duration() <= EsStreamConfig.BufferingEventThreshold;

                _bufferingSubject.OnNext(displayBuffering);

                currentPacket = await packetStorage.GetPacket(streamType, token);
            }

            var shouldContinue = await ProcessPacket(currentPacket, token);

            _dataSynchronizer.DataOut(currentPacket);
            var packetType = currentPacket.GetType();
            currentPacket.Dispose();
            currentPacket = null;

            _packetProcessed.OnNext(packetType);
            return shouldContinue;
        }

        /// <summary>
        /// Pushes unencrypted data packet to ESPlayer
        /// </summary>
        /// <param name="dataPacket">Packet</param>
        /// <param name="token">CancellationToken</param>
        /// <exception cref="PacketSubmitException">
        /// Exception thrown on submit error
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Exception thrown on submit cancellation
        /// </exception>
        private async ValueTask PushUnencryptedPacket(Packet dataPacket, CancellationToken token)
        {
            for (; ; )
            {
                var submitStatus = player.Submit(dataPacket);

                logger.Debug(
                    $"{dataPacket.StreamType}: ({submitStatus}) PTS: {dataPacket.Pts} Duration: {dataPacket.Duration}");

                if (submitStatus == ESPlayer.SubmitStatus.Success)
                    return;

                if (!ShouldRetry(submitStatus))
                    throw new PacketSubmitException("Packet Submit Error", submitStatus);

                await Task.Delay(CalculateDelay(submitStatus), token);
            }
        }

        /// <summary>
        /// Pushes encrypted data packet to ESPlayer.
        /// Decryption is performed prior to packet push.
        /// </summary>
        /// <param name="dataPacket">Packet</param>
        /// <param name="token">CancellationToken</param>
        /// <exception cref="PacketSubmitException">
        /// Exception thrown on submit error
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Exception thrown on submit cancellation
        /// </exception>
        private async ValueTask PushEncryptedPacket(EncryptedPacket dataPacket, CancellationToken token)
        {
            ICdmInstance cdmInstance = dataPacket.CdmInstance;

            if (cdmInstance == null)
                throw new InvalidOperationException("Decrypt called without CdmInstance");

            Task sessionsInitializationsTask = cdmInstance.WaitForAllSessionsInitializations(token);
            if (!sessionsInitializationsTask.IsCompleted)
            {
                logger.Info($"{streamType}: DRM Initialization incomplete");
                _bufferingSubject.OnNext(true);
                await sessionsInitializationsTask;
                _bufferingSubject.OnNext(false);

                logger.Info($"{streamType}: DRM Initialization complete");
            }

            using (var decryptedPacket = await cdmInstance.DecryptPacket(dataPacket, token) as DecryptedEMEPacket)
            {
                // Continue pushing packet till success or terminal failure
                for (;;)
                {
                    var submitStatus = player.Submit(decryptedPacket);

                    logger.Debug(
                        $"{decryptedPacket.StreamType}: ({submitStatus}) PTS: {decryptedPacket.Pts} Duration:" +
                        $"{decryptedPacket.Duration} Handle: {decryptedPacket.HandleSize.handle} " +
                        $"HandleSize: {decryptedPacket.HandleSize.size}");

                    if (submitStatus == ESPlayer.SubmitStatus.Success)
                    {
                        decryptedPacket.CleanHandle();
                        return;
                    }

                    if (!ShouldRetry(submitStatus))
                        throw new PacketSubmitException("Packet Submit Error", submitStatus);

                    await Task.Delay(CalculateDelay(submitStatus), token);
                }
            }
        }

        /// <summary>
        /// Pushes EOS packet to ESPlayer
        /// </summary>
        /// <param name="dataPacket">Packet</param>
        /// <param name="token">CancellationToken</param>
        /// <exception cref="PacketSubmitException">
        /// Exception thrown on submit error
        /// </exception>
        private async ValueTask PushEosPacket(EOSPacket packet, CancellationToken token)
        {
            logger.Info($"{streamType}");

            // Continue pushing packet till success or terminal failure
            for (; ; )
            {
                var submitStatus = player.Submit(packet);
                if (submitStatus == ESPlayer.SubmitStatus.Success)
                    return;

                if (!ShouldRetry(submitStatus))
                    throw new PacketSubmitException("Packet Submit Error", submitStatus);

                await Task.Delay(CalculateDelay(submitStatus), token);
            }
        }

        private bool ShouldRetry(ESPlayer.SubmitStatus status)
        {
            return status == ESPlayer.SubmitStatus.NotPrepared || status == ESPlayer.SubmitStatus.Full;
        }

        private TimeSpan CalculateDelay(ESPlayer.SubmitStatus status)
        {
            // calculate delay
            switch (status)
            {
                case ESPlayer.SubmitStatus.NotPrepared:
                    logger.Info($"{streamType}: Packet NOT Prepared");
                    return TimeSpan.FromSeconds(1);
                case ESPlayer.SubmitStatus.Full:
                    return TimeSpan.FromMilliseconds(500);
                default:
                    return TimeSpan.Zero;
            }
        }
        #endregion

        #region IDisposable Support

        private bool isDisposed;

        public void Dispose()
        {
            if (isDisposed)
                return;

            logger.Info($"{streamType}:");

            DisableInput();
            DisableTransfer();

            playbackErrorSubject.Dispose();

            _bufferingSubject.OnCompleted();
            _bufferingSubject.Dispose();
            _packetProcessed.OnCompleted();
            _packetProcessed.Dispose();

            transferCts?.Dispose();
            linkedCts?.Dispose();

            currentPacket?.Dispose();

            isDisposed = true;
        }

        #endregion
    }
}
