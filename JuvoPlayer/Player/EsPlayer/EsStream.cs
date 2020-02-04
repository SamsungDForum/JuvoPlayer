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
using System.Reactive;
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
        private Task activeTask = Task.CompletedTask;
        private CancellationTokenSource transferCts;

        private TaskCompletionSource<object> _firstDataPacketTcs;

        // Stream type for this instance to EsStream.
        private readonly StreamType streamType;

        // Buffer configuration and supporting info
        public StreamConfig CurrentConfig { get; internal set; }
        public StreamConfig LastQueuedConfig { get; internal set; }

        public bool IsConfigured => (CurrentConfig != null);
        public bool HaveConfig => (LastQueuedConfig != null);

        // Events
        private readonly Subject<string> playbackErrorSubject = new Subject<string>();
        private readonly Subject<Unit> streamReconfigureSubject = new Subject<Unit>();

        private Packet currentPacket;
        private TimeSpan currentPts;


        private readonly Synchronizer _dataSynchronizer;
        private readonly SuspendResumeLogic _suspendResumeLogic;
        private readonly PlayerClockProvider _playerClock;

        public IObservable<string> PlaybackError()
        {
            return playbackErrorSubject.AsObservable();
        }

        public IObservable<Unit> StreamReconfigure()
        {
            return streamReconfigureSubject.AsObservable();
        }

        #region Public API

        public EsStream(StreamType type, EsPlayerPacketStorage storage, Synchronizer synchronizer, SuspendResumeLogic suspendRedumeLogic, PlayerClockProvider playerClock)
        {
            streamType = type;
            packetStorage = storage;
            _dataSynchronizer = synchronizer;
            _dataSynchronizer.Initialize(streamType);
            _suspendResumeLogic = suspendRedumeLogic;
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

            _firstDataPacketTcs = new TaskCompletionSource<object>();
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
        /// Non configured stream - stream config will be pushed directly to ES Player.
        /// Configured stream - stream config will be enqueue in packet storage
        /// and processed once retrieved.
        /// </summary>
        /// <param name="config">stream configuration</param>
        public void StoreConfiguration(StreamConfig config)
        {
            logger.Info($"{streamType}");

            LastQueuedConfig = config;
        }

        /// <summary>
        /// Method resets current config. When config change occurs as a result
        /// of config packet being queued, CurrentConfig holds value of new configuration
        /// which needs to be pushed to player
        /// </summary>
        public void PushStreamConfiguration()
        {
            logger.Info($"{streamType}");

            PushStreamConfig(LastQueuedConfig);
            CurrentConfig = LastQueuedConfig;
        }

        /// <summary>
        /// Public API for starting data transfer
        /// </summary>
        public void Start()
        {
            EnableTransfer();
        }

        /// <summary>
        /// Public API for stopping data transfer
        /// </summary>
        public void Stop()
        {
            DisableTransfer();
        }

        /// <summary>
        /// Public API for disabling data transfer. Once called, no further
        /// data transfer will be possible.
        /// </summary>
        public void Disable()
        {
            DisableInput();
        }

        /// <summary>
        /// Awaitable function. Will return when a running task terminates.
        /// </summary>
        /// <returns></returns>
        public ref Task GetActiveTask()
        {
            logger.Info($"{streamType}: {activeTask.Status}");
            return ref activeTask;
        }

        public void EmptyStorage()
        {
            currentPacket?.Dispose();
            currentPacket = null;

            packetStorage.Empty(streamType);
        }

        public void EmptyBuffer()
        {
            packetStorage.Empty(streamType);
        }

        public void RequestFirstDataPacketNotification()
        {
            // Note. RequestFirstDataPacketNotification() is not thread safe.
            // It is to be called once per first packet need.
            // Currently, this is async Ops (Seek/Prepare). Not by Pause/Resume

            if (!GetFirstDataPacketNotificationTask().IsCompleted)
                return;

            _firstDataPacketTcs = new TaskCompletionSource<object>(streamType, TaskCreationOptions.RunContinuationsAsynchronously);

            logger.Info($"{streamType}: Data packet processed confirmation requested");
        }

        public Task<object> GetFirstDataPacketNotificationTask() =>
            _firstDataPacketTcs?.Task ?? Task.FromResult<object>(null);

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
        private void EnableTransfer()
        {
            logger.Info($"{streamType}:");

            // No cancellation requested = task not stopped
            if (!activeTask.IsCompleted)
            {
                logger.Info($"{streamType}: Already running: {activeTask.Status}");
                return;
            }

            if (!IsConfigured)
            {
                throw new InvalidOperationException($"{streamType}: Not Configured");
            }

            transferCts?.Dispose();
            transferCts = new CancellationTokenSource();

            activeTask = Task.Run(async () => await TransferTask());
        }

        /// <summary>
        /// Stops data transfer, if already running, by terminating transfer task.
        /// </summary>
        private void DisableTransfer()
        {
            logger.Info($"{streamType}:");
            transferCts?.Cancel();
        }

        /// <summary>
        /// Disables further data transfer. Existing data in queue will continue
        /// to be pushed to the player.
        /// </summary>
        public void DisableInput()
        {
            logger.Info($"{streamType}:");
            packetStorage.Disable(streamType);
        }

        public void EnableInput()
        {
            logger.Info($"{streamType}:");
            packetStorage.Enable(streamType);
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
                    // Video is always compatible
                    var isCompatible = CurrentConfig.StreamType() == StreamType.Video
                                       || (CurrentConfig.StreamType() == StreamType.Audio
                                            && CurrentConfig.IsCompatible(bufferConfigPacket.Config));

                    CurrentConfig = bufferConfigPacket.Config;

                    if (!isCompatible)
                    {
                        logger.Warn($"{streamType}: Incompatible Stream config change.");
                        streamReconfigureSubject.OnNext(Unit.Default);

                        // exit transfer task. This will prevent further transfers
                        // Stops/Restarts will be called by reconfiguration handler.
                        continueProcessing = false;
                    }

                    break;

                case EncryptedPacket encryptedPacket:
                    await PushEncryptedPacket(encryptedPacket, transferToken);
                    currentPts = packet.Pts;
                    break;

                case Packet dataPacket:
                    await PushUnencryptedPacket(dataPacket, transferToken);
                    currentPts = packet.Pts;
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
        private async Task TransferTask()
        {
            CancellationToken token = transferCts.Token;
            logger.Info($"{streamType}: Started");

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
                if (_firstDataPacketTcs?.Task.IsCompleted == false)
                {
                    logger.Info($"{streamType}: Cancelling first data packet request");
                    _firstDataPacketTcs.TrySetException(
                        new OperationCanceledException("Terminated before notifying first data packet"));
                }

                logger.Info($"{streamType}: Terminated. ");
            }
        }

        private void ConfirmFirstDataPacket()
        {
            if (currentPacket.ContainsData())
            {
                _firstDataPacketTcs.SetResult(null);
                logger.Info($"{currentPacket.StreamType}: First packet is DATA. {currentPacket.Dts}");
                return;
            }

            if (!(currentPacket is EOSPacket))
                return;

            _firstDataPacketTcs.SetException(new OperationCanceledException("First packet is EOS"));
            logger.Info($"{currentPacket.StreamType}: First Packet is EOS");
        }


        private async ValueTask<Packet> GetPacket(CancellationToken token)
        {
            var bufferingNeeded = packetStorage.Count(streamType) == 0
                                  && (_playerClock.LastClock - currentPts).Duration() <=
                                  EsStreamConfig.BufferingEventThreshold;

            if (bufferingNeeded)
                await _suspendResumeLogic.RequestBuffering(true).ConfigureAwait(false);

            var packet = packetStorage.GetPacket(streamType, token);

            if (bufferingNeeded)
            {
#pragma warning disable 4014 // No need to wait for dissapear. Will happen.. eventually.
                _suspendResumeLogic.RequestBuffering(false);
#pragma warning restore 4014
            }

            return packet;

        }
        private async ValueTask<bool> ProcessNextPacket(CancellationToken token)
        {
            currentPacket = currentPacket ?? packetStorage.GetPacket(streamType, token);

            var shouldContinue = await ProcessPacket(currentPacket, token);

            _dataSynchronizer.DataOut(currentPacket);

            if (_firstDataPacketTcs?.Task.IsCompleted == false)
                ConfirmFirstDataPacket();

            currentPacket.Dispose();
            currentPacket = null;

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
                    $"{dataPacket.StreamType}: ({submitStatus} )PTS: {dataPacket.Pts} Duration: {dataPacket.Duration}");

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
            if (!dataPacket.DrmSession.CanDecrypt())
            {
#pragma warning disable 4014    // No await intentional. Do not care much when buffering will be displayed
                _suspendResumeLogic.RequestBuffering(true);
                await dataPacket.DrmSession.WaitForInitialization(token);
                _suspendResumeLogic.RequestBuffering(false);
#pragma warning restore 4014    // of hidden for that matter.
                logger.Info($"{streamType}: DRM Initialization complete");
            }

            using (var decryptedPacket = await dataPacket.Decrypt(token) as DecryptedEMEPacket)
            {
                // Continue pushing packet till success or terminal failure
                for (; ; )
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
            streamReconfigureSubject.Dispose();

            transferCts?.Dispose();
            currentPacket?.Dispose();

            isDisposed = true;
        }

        #endregion
    }
}
