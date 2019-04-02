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
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using ESPlayer = Tizen.TV.Multimedia;
using StreamType = JuvoPlayer.Common.StreamType;
using static Configuration.EsStream;

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
        internal enum SetStreamConfigResult
        {
            ConfigPushed,
            ConfigQueued
        }

        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        /// Delegate holding PushConfigMethod. Different for Audio and Video
        private delegate void StreamConfigure(Common.StreamConfig streamConfig);

        private readonly StreamConfigure PushStreamConfig;

        // Reference to internal EsPlayer objects
        private ESPlayer.ESPlayer player;
        private readonly EsPlayerPacketStorage packetStorage;

        // transfer task & cancellation token
        private Task activeTask = Task.CompletedTask;
        private CancellationTokenSource transferCts;

        // Stream type for this instance to EsStream.
        private readonly Common.StreamType streamType;

        private ManualResetEventSlim wakeup;

        // Buffer configuration and supporting info
        public BufferConfigurationPacket CurrentConfig { get; internal set; }
        public bool IsConfigured => (CurrentConfig != null);

        // Events
        private readonly Subject<string> playbackErrorSubject = new Subject<string>();
        private readonly Subject<Unit> streamReconfigureSubject = new Subject<Unit>();

        private Packet currentPacket;
        private PacketBarrier barrier;
        private readonly StreamBuffer streamBuffer;

        public IObservable<string> PlaybackError()
        {
            return playbackErrorSubject.AsObservable();
        }

        public IObservable<Unit> StreamReconfigure()
        {
            return streamReconfigureSubject.AsObservable();
        }

        #region Public API

        public EsStream(Common.StreamType type, EsPlayerPacketStorage storage, StreamBufferController bufferController)
        {
            streamType = type;
            packetStorage = storage;
            streamBuffer = bufferController.GetStreamBuffer(type);

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

            wakeup = new ManualResetEventSlim(false);
            barrier = new PacketBarrier(TransferChunk);
        }

        /// <summary>
        /// Sets the player to be used by EsStream
        /// </summary>
        /// <param name="player">ESPlayer</param>
        public void SetPlayer(ESPlayer.ESPlayer player)
        {
            this.player = player;
        }

        /// <summary>
        /// Sets Stream configuration
        /// Non configured stream - stream config will be pushed directly to ES Player.
        /// Configured stream - stream config will be enqueue in packet storage
        /// and processed once retrieved.
        /// </summary>
        /// <param name="bufferConfig">BufferConfigurationPacket</param>
        /// <returns>SetStreamConfigResult</returns>
        public SetStreamConfigResult SetStreamConfig(BufferConfigurationPacket bufferConfig)
        {
            // Depending on current configuration state, packets are either pushed
            // directly to player or queued in packet queue.
            // To make sure current state is known, sync this operation.
            //
            logger.Info($"{streamType}: Already Configured: {IsConfigured}");

            if (IsConfigured)
            {
                packetStorage.AddPacket(bufferConfig);
                logger.Info($"{streamType}: New configuration queued");
                return SetStreamConfigResult.ConfigQueued;
            }

            CurrentConfig = bufferConfig;
            PushStreamConfig(CurrentConfig.Config);
            return SetStreamConfigResult.ConfigPushed;
        }

        /// <summary>
        /// Method resets current config. When config change occurs as a result
        /// of config packet being queued, CurrentConfig holds value of new configuration
        /// which needs to be pushed to player
        /// </summary>
        public void ResetStreamConfig()
        {
            logger.Info($"{streamType}:");

            PushStreamConfig(CurrentConfig.Config);
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
        public Task GetActiveTask()
        {
            return activeTask;
        }

        public void Wakeup()
        {
            logger.Info($"{streamType}:");
            wakeup.Set();
        }

        public void EmptyStorage()
        {
            packetStorage.Empty(streamType);
            currentPacket?.Dispose();
            currentPacket = null;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Audio Configuration push method.
        /// </summary>
        /// <param name="streamConfig">Common.StreamConfig</param>
        private void PushAudioConfig(StreamConfig streamConfig)
        {
            logger.Info($"{streamType}:");

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
            logger.Info($"{streamType}:");

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
            var token = transferCts.Token;

            activeTask = Task.Factory.StartNew(async () => await TransferTask(token)).Unwrap();
        }

        /// <summary>
        /// Stops data transfer, if already running, by terminating transfer task.
        /// </summary>
        private void DisableTransfer()
        {
            logger.Info($"{streamType}:");
            transferCts?.Cancel();
            logger.Info($"{streamType}: Stopping transfer");
        }

        /// <summary>
        /// Disables further data transfer. Existing data in queue will continue
        /// to be pushed to the player.
        /// </summary>
        private void DisableInput()
        {
            logger.Info($"{streamType}:");
            packetStorage.Disable(streamType);
        }

        private async Task<bool> ProcessPacket(Packet packet, CancellationToken transferToken)
        {
            var continueProcessing = true;

            switch (packet)
            {
                case EOSPacket eosPacket:
                    PushEosPacket(eosPacket, transferToken);
                    continueProcessing = false;
                    break;

                case BufferConfigurationPacket bufferConfigPacket:
                    CurrentConfig = bufferConfigPacket;

                    if (CurrentConfig.StreamType == StreamType.Audio && !CurrentConfig.Compatible(bufferConfigPacket))
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
                    break;

                case Packet dataPacket:
                    PushUnencryptedPacket(dataPacket, transferToken);
                    break;

                default:
                    throw new ArgumentException($"{streamType}: Unsupported packet type {packet.GetType()}");
            }

            return continueProcessing;
        }

        private void DelayTransfer(TimeSpan delay, CancellationToken token)
        {
            logger.Info($"{streamType}: Transfer task restart in {delay}");
            wakeup.Reset();
            if (delay > TimeSpan.Zero)
            {
                logger.Info($"{streamType}: {delay}");
                wakeup.Wait(delay, token);
            }

            wakeup.Reset();
            logger.Info($"{streamType}: Transfer restarted");
        }

        /// <summary>
        /// Transfer task. Retrieves data from underlying storage and pushes it down
        /// to ESPlayer
        /// </summary>
        /// <param name="token">CancellationToken</param>
        private async Task TransferTask(CancellationToken token)
        {
            logger.Info($"{streamType}: Transfer task started");

            barrier.Reset();

            try
            {
                while (true)
                {
                    var shouldContinue = await ProcessNextPacket(token);
                    if (!shouldContinue)
                        break;
                    if (!barrier.Reached())
                        continue;

                    var delay = barrier.TimeToNextFrame();

                    logger.Info(
                        $"{streamType}: Transfer task halted. Buffer {streamBuffer.BufferFill}% {streamBuffer.CurrentBufferSize}");

                    DelayTransfer(delay, token);

                    barrier.Reset();
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

            logger.Info(
                $"{streamType}: Transfer task terminated. Buffer {streamBuffer.BufferFill}% {streamBuffer.CurrentBufferSize}");
        }

        private async Task<bool> ProcessNextPacket(CancellationToken token)
        {
            if (currentPacket == null)
                currentPacket = packetStorage.GetPacket(streamType, token);

            var shouldContinue = await ProcessPacket(currentPacket, token);

            barrier.PacketPushed(currentPacket);
            streamBuffer.DataOut(currentPacket);
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
        private void PushUnencryptedPacket(Packet dataPacket, CancellationToken token)
        {
            for (;;)
            {
                var submitStatus = player.Submit(dataPacket);

                logger.Debug(
                    $"{dataPacket.StreamType}: ({submitStatus} )PTS: {dataPacket.Pts} Duration: {dataPacket.Duration}");

                if (submitStatus == ESPlayer.SubmitStatus.Success)
                    return;

                if (!ShouldRetry(submitStatus))
                    throw new PacketSubmitException("Packet Submit Error", submitStatus);

                var delay = CalculateDelay(submitStatus);
                Wait(delay, token);
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
        private async Task PushEncryptedPacket(EncryptedPacket dataPacket, CancellationToken token)
        {
            using (var decryptedPacket = await dataPacket.Decrypt(token) as DecryptedEMEPacket)
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

                    var delay = CalculateDelay(submitStatus);
                    Wait(delay, token);
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
        private void PushEosPacket(EOSPacket packet, CancellationToken token)
        {
            logger.Info("");

            // Continue pushing packet till success or terminal failure
            for (;;)
            {
                var submitStatus = player.Submit(packet);
                if (submitStatus == ESPlayer.SubmitStatus.Success)
                    return;

                if (!ShouldRetry(submitStatus))
                    throw new PacketSubmitException("Packet Submit Error", submitStatus);

                var delay = CalculateDelay(submitStatus);
                Wait(delay, token);
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
                    return TimeSpan.FromSeconds(1);
                case ESPlayer.SubmitStatus.Full:
                    return TimeSpan.FromMilliseconds(500);
                default:
                    return TimeSpan.Zero;
            }
        }

        private void Wait(TimeSpan delay, CancellationToken token)
        {
            using (var @event = new ManualResetEventSlim(false))
                @event.Wait(delay, token);
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

            wakeup.Dispose();

            currentPacket?.Dispose();

            isDisposed = true;
        }

        #endregion
    }
}
