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
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using ESPlayer = Tizen.TV.Multimedia;
using StreamType = JuvoPlayer.Common.StreamType;

namespace JuvoPlayer.Player.EsPlayer
{
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
    /// Internal EsPlayer event. Called when stream has to be reconfigured
    /// </summary>
    internal delegate void StreamReconfigure();

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
        private readonly ESPlayer.ESPlayer player;
        private readonly EsPlayerPacketStorage packetStorage;

        // transfer task & cancellation token
        private Task transferTask = Task.CompletedTask;
        private CancellationTokenSource transferCts;

        // Stream type for this instance to EsStream.
        private readonly Common.StreamType streamType;

        // Buffer configuration and supporting info
        public BufferConfigurationPacket CurrentConfig { get; internal set; }
        public bool IsConfigured => (CurrentConfig != null);

        // lock object used for serialization of internal operations
        // that can be externally accessed
        private readonly object syncLock = new object();

        // Event - Invoked when stream requires reconfiguration
        public event StreamReconfigure ReconfigureStream;

        #region Public API

        public EsStream(ESPlayer.ESPlayer player, Common.StreamType type, EsPlayerPacketStorage storage)
        {
            this.player = player;
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
            lock (syncLock)
            {
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
        }

        /// <summary>
        /// Method resets current config. When config change occurs as a result
        /// of config packet being queued, CurrentConfig holds value of new configuration
        /// which needs to be pushed to player 
        /// </summary>
        public void ResetStreamConfig()
        {
            logger.Info($"{streamType}:");

            lock (syncLock)
            {
                PushStreamConfig(CurrentConfig.Config);
            }
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
        public async Task AwaitCompletion()
        {
            logger.Info("");
            await transferTask;
            logger.Info("Done");
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// Audio Configuration push method.
        /// </summary>
        /// <param name="streamConfig">Common.StreamConfig</param>
        private void PushAudioConfig(Common.StreamConfig streamConfig)
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
        private void PushVideoConfig(Common.StreamConfig streamConfig)
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

            lock (syncLock)
            {
                // No cancellation requested = task not stopped
                if (!transferTask.IsCompleted)
                {
                    logger.Info($"{streamType}: Already running: {transferTask.Status}");
                    return;
                }

                if (!IsConfigured)
                {
                    throw new InvalidOperationException($"{streamType}: Not Configured");
                }

                transferCts?.Dispose();
                transferCts = new CancellationTokenSource();
                var token = transferCts.Token;

                transferTask = Task.Factory.StartNew(() => TransferTask(token), token);
            }
        }

        /// <summary>
        /// Stops data transfer, if already running, by terminating transfer task.
        /// </summary>
        private void DisableTransfer()
        {
            logger.Info($"{streamType}:");

            lock (syncLock)
            {
                transferCts?.Cancel();

                logger.Info($"{streamType}: Stopping transfer");
            }
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

        /// <summary>
        /// Transfer task. Retrieves data from underlying storage and pushes it down
        /// to ESPlayer
        /// </summary>
        /// <param name="token">CancellationToken</param>
        //private async Task TransferTask(CancellationToken token)
        private void TransferTask(CancellationToken token)
        {
            logger.Info($"{streamType}: Transfer task started");

            var disableTransfer = false;

            try
            {
                do
                {
                    var packet = packetStorage.GetPacket(streamType, token);

                    switch (packet)
                    {
                        case Packet eosPacket when eosPacket.IsEOS:
                            PushEosPacket(token);
                            disableTransfer = true;
                            return;

                        case BufferConfigurationPacket bufferConfigPacket when
                            !CurrentConfig.Compatible(bufferConfigPacket):

                            CurrentConfig = bufferConfigPacket;

                            logger.Info($"{streamType}: Incompatible Stream config change.");
                            ReconfigureStream?.Invoke();

                            // exit transfer task. This will prevent further transfers
                            // Stops/Restarts will be called by reconfiguration handler.
                            return;

                        case BufferConfigurationPacket bufferConfigPacket when
                            CurrentConfig.Compatible(bufferConfigPacket):

                            CurrentConfig = bufferConfigPacket;

                            logger.Info($"{streamType}: Compatible Stream config change");

                            // exit transfer task. This will prevent further transfers
                            // Stops/Restarts will be called by reconfiguration handler.
                            break;

                        case EncryptedPacket encryptedPacket:
                            PushEncryptedPacket(encryptedPacket, token);
                            break;

                        case Packet dataPacket when packet.IsEOS == false:
                            PushUnencryptedPacket(dataPacket, token);
                            break;
                    }

                } while (!token.IsCancellationRequested);
            }
            catch (InvalidOperationException)
            {
                logger.Info($"{streamType}: Stream completed");
                disableTransfer = true;
            }
            catch (OperationCanceledException)
            {
                logger.Info($"{streamType}: Transfer stopped");

                // Operation is cancelled thus Stop/StopInternal has already been
                // called. No need to repeat.
            }
            catch (PacketSubmitException pse)
            {
                logger.Error($"{streamType}: Submit Error " + pse.SubmitStatus);
                disableTransfer = true;
            }
            catch (DrmException drme)
            {
                logger.Error($"{streamType}: Decrypt Error: " + drme.Message);
                disableTransfer = true;
            }
            finally
            {
                if (disableTransfer)
                {
                    logger.Info($"{streamType}: Disabling transfer");
                    DisableInput();
                }

                logger.Info($"{streamType}: Transfer task terminated");
            }
        }

        /// <summary>
        /// Pushes data packet to ESPlayer
        /// </summary>
        /// <param name="dataPacket">Packet</param>
        /// <param name="token">CancellationToken</param>
        /// <exception cref="PacketSubmitException">
        /// Exception thrown on submit error
        /// </exception>
        private void PushUnencryptedPacket(Packet dataPacket, CancellationToken token)
        {
            // Convert Juvo packet to ESPlayer packet
            var esPacket = dataPacket.ESUnencryptedPacket();

            // Continue pushing packet till success or terminal failure
            bool doRetry;
            do
            {
                var res = player.SubmitPacket(esPacket);
                doRetry = ShouldRetry(res, token);
                logger.Debug($"{esPacket.type}: ({!doRetry}/{res}) PTS: {esPacket.pts} Duration: {esPacket.duration}");

            } while (doRetry && !token.IsCancellationRequested);
        }

        private void PushEncryptedPacket(EncryptedPacket dataPacket, CancellationToken token)
        {
            using (Common.DecryptedEMEPacket decryptedPacket = dataPacket.Decrypt() as Common.DecryptedEMEPacket)
            {
                if (decryptedPacket == null)
                {
                    logger.Error($"{dataPacket.StreamType}: Non an EME Packet!");
                    return;
                }

                var esPacket = decryptedPacket.ESDecryptedPacket();

                // Continue pushing packet till success or terminal failure
                bool doRetry;
                do
                {
                    var res = player.SubmitPacket(esPacket);

                    // reset unmanaged handle on successful submit
                    if (res == ESPlayer.SubmitStatus.Success)
                        decryptedPacket.CleanHandle();

                    doRetry = ShouldRetry(res, token);

                    logger.Debug($"{esPacket.type}: ({!doRetry}/{res}) PTS: {esPacket.pts} Duration: {esPacket.duration} Handle: {esPacket.handle} HandleSize: {esPacket.handleSize}");

                } while (doRetry && !token.IsCancellationRequested);
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

            bool doRetry;

            // Continue pushing packet till success or terminal failure
            do
            {
                var res = player.SubmitEosPacket(streamType.ESStreamType());
                doRetry = ShouldRetry(res, token);

            } while (doRetry && !token.IsCancellationRequested);
        }

        /// <summary>
        /// Processes packet push result. Returned is an indication if retry
        /// should take place or not
        /// </summary>
        /// <param name="status">ESPlayer.SubmitStatus</param>
        /// <param name="token">CancellationToken</param>
        /// <returns>
        /// True - retry packet push
        /// False - do not retry packet push
        /// </returns>
        /// <exception cref="PacketSubmitException">
        /// Exception thrown on submit error
        /// </exception>
        private bool ShouldRetry(ESPlayer.SubmitStatus status, CancellationToken token)
        {
            TimeSpan delay;

            switch (status)
            {
                case ESPlayer.SubmitStatus.Success:
                    return false;

                case ESPlayer.SubmitStatus.NotPrepared:
                    logger.Info(streamType + ": " + status);
                    delay = TimeSpan.FromSeconds(1);
                    break;

                case ESPlayer.SubmitStatus.Full:
                    delay = TimeSpan.FromMilliseconds(500);
                    break;

                default:
                    throw new PacketSubmitException("Packet Submit Error", status);
            }

            // We are left with Status.Full 
            // For now sleep, however, once buffer events will be 
            // emitted from ESPlayer, they could be used here
            using (var napTime = new ManualResetEventSlim(false))
                napTime.Wait(delay, token);

            return true;
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

            if (transferCts != null)
            {
                logger.Info($"{streamType}: Waiting for cancellation to be signaled");
                WaitHandle.WaitAll(new WaitHandle[] { transferCts.Token.WaitHandle });
                transferCts.Dispose();
                transferCts = null;
            }

            isDisposed = true;
        }
        #endregion
    }
}
