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

        internal enum SeekResult
        {
            Ok,
            RestartRequired
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

        // chunk transfer support elements. Max chunk size & wakeup object
        private static readonly TimeSpan transferChunk = TimeSpan.FromSeconds(2);
        private ManualResetEventSlim wakeup;

        // Buffer configuration and supporting info
        public BufferConfigurationPacket CurrentConfig { get; internal set; }
        public bool IsConfigured => (CurrentConfig != null);

        // Events
        private readonly Subject<string> playbackErrorSubject = new Subject<string>();
        private readonly Subject<Unit> streamReconfigureSubject = new Subject<Unit>();

        public IObservable<string> PlaybackError()
        {
            return playbackErrorSubject.AsObservable();
        }

        public IObservable<Unit> StreamReconfigure()
        {
            return streamReconfigureSubject.AsObservable();
        }


        #region Public API

        public EsStream(Common.StreamType type, EsPlayerPacketStorage storage)
        {
            streamType = type;
            packetStorage = storage;

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

        public Task<SeekResult> Seek(uint seekId, TimeSpan seekPosition, CancellationToken token)
        {
            return Task.Factory.StartNew(() => SeekTask(seekId, seekPosition, token), token);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Seek Task. Performs seek to specified seekId, followed by seek to specified
        /// seek position
        /// </summary>
        /// <param name="seekId">Seek ID to seek to</param>
        /// <param name="seekPosition">seek position to seek to</param>
        /// <param name="token">cancel token</param>
        /// <returns></returns>
        private SeekResult SeekTask(uint seekId, TimeSpan seekPosition, CancellationToken token)
        {
            logger.Info($"{streamType}: {seekId}");

            while (true)
            {
                try
                {
                    var packet = packetStorage.GetPacket(streamType, token);

                    switch (packet)
                    {
                        case BufferConfigurationPacket bufferConfigPacket:
                            var isCompatible = CurrentConfig.Compatible(bufferConfigPacket);
                            CurrentConfig = bufferConfigPacket;
                            if (CurrentConfig.StreamType == StreamType.Audio && !isCompatible)
                                return SeekResult.RestartRequired;
                            break;
                        case SeekPacket seekPacket:
                            if (seekPacket.SeekId != seekId)
                                break;

                            logger.Info($"{streamType}: Seek Id {seekId} found. Looking for time {seekPosition}");
                            return SeekResult.Ok;
                        default:
                            packet.Dispose();
                            break;
                    }
                }
                catch (InvalidOperationException)
                {
                    logger.Warn($"{streamType}: Stream completed");
                    return SeekResult.Ok;
                }
                catch (OperationCanceledException)
                {
                    logger.Warn($"{streamType}: Seek cancelled");
                    return SeekResult.Ok;
                }
                catch (Exception e)
                {
                    logger.Error(e, $"{streamType}");
                    throw;
                }
            }
        }

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
                case Packet eosPacket when eosPacket.IsEOS:
                    PushEosPacket(transferToken);
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

                case Packet dataPacket when packet.IsEOS == false:
                    PushUnencryptedPacket(dataPacket, transferToken);
                    break;

                default:
                    throw new ArgumentException($"{streamType}: Unsupported packet type");
            }

            return continueProcessing;
        }

        private void DelayTransfer(TimeSpan delay, CancellationToken token)
        {
            logger.Info($"{streamType}: Transfer task restart in {delay}");
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

            var disableInput = false;
            TimeSpan currentTransfer = TimeSpan.Zero;
            var haltTransfer = false;
            ulong dataLength = 0;
            TimeSpan? firstPts = null;
            DateTime startTime = DateTime.Now;
            bool invokeError = false;

            try
            {
                Packet packet;
                do
                {
                    if (haltTransfer)
                    {
                        var delay = currentTransfer - (DateTime.Now - startTime);

                        logger.Info(
                            $"{streamType}: Transfer task halted. {currentTransfer}/{(float)dataLength / 1024} kB pushed");

                        DelayTransfer(delay, token);

                        firstPts = null;
                        dataLength = 0;
                        haltTransfer = false;
                        currentTransfer = TimeSpan.Zero;
                    }

                    packet = packetStorage.GetPacket(streamType, token);

                    // Ignore non data packets (EOS/BufferChange/etc.)
                    if (packet.Data != null)
                    {
                        dataLength += (ulong)packet.Data.Length;

                        if (firstPts.HasValue)
                        {
                            currentTransfer = packet.Pts - firstPts.Value;

                            if (currentTransfer >= transferChunk)
                                haltTransfer = true;
                        }
                        else
                        {
                            firstPts = packet.Pts;
                            startTime = DateTime.Now;
                        }
                    }

                } while (await ProcessPacket(packet, token));
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
                disableInput = true;
            }
            catch (DrmException drme)
            {
                logger.Error(drme, $"{streamType}: Decrypt Error");
                disableInput = true;
                invokeError = true;
            }
            catch (Exception e)
            {
                // Dump unhandled exception. Running as a task so they will not be reported.
                logger.Error(e, $"{streamType}");
                disableInput = true;
                invokeError = true;
            }

            if (disableInput)
            {
                logger.Info($"{streamType}: Disabling Input");
                DisableInput();
            }

            logger.Info(
                $"{streamType}: Transfer task terminated. {currentTransfer}/{(float)dataLength / 1024} kB pushed");

            if (invokeError)
                playbackErrorSubject.OnNext("Playback Error");
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
            // Convert Juvo packet to ESPlayer packet
            var esPacket = dataPacket.ESUnencryptedPacket();

            for (; ; )
            {
                var submitStatus = player.SubmitPacket(esPacket);

                logger.Debug($"{esPacket.type}: ({submitStatus}) PTS: {esPacket.pts} Duration: {esPacket.duration}");

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
                var esPacket = decryptedPacket.ESDecryptedPacket();

                // Continue pushing packet till success or terminal failure
                for (; ; )
                {
                    var submitStatus = player.SubmitPacket(esPacket);

                    logger.Debug(
                        $"{esPacket.type}: ({submitStatus}) PTS: {esPacket.pts.FromNano()} Duration: {esPacket.duration.FromNano()} Handle: {esPacket.handle} HandleSize: {esPacket.handleSize}");

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
        private void PushEosPacket(CancellationToken token)
        {
            logger.Info("");

            // Continue pushing packet till success or terminal failure
            for (; ; )
            {
                var submitStatus = player.SubmitEosPacket(streamType.ESStreamType());
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

            isDisposed = true;
        }

        #endregion
    }
}
