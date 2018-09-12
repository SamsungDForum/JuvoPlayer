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
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using ESPlayer = Tizen.TV.Multimedia.ESPlayer;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace JuvoPlayer.Player.EsPlayer
{
    [Serializable]
    public class PacketSubmitException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //
        public ESPlayer.SubmitStatus SubmitStatus { get; internal set; }

        public PacketSubmitException(ESPlayer.SubmitStatus status)
        {
            SubmitStatus = status;
        }

        public PacketSubmitException(string message, ESPlayer.SubmitStatus status) : base(message)
        {
            SubmitStatus = status;
        }

        public PacketSubmitException(string message, Exception inner, ESPlayer.SubmitStatus status) : base(message, inner)
        {
            SubmitStatus = status;
        }

        protected PacketSubmitException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }

    internal class EsStream : IDisposable
    {
        private static ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        /// <summary>
        /// Delegate holding PushConfigMethod.
        /// Different for Audio and Video
        /// </summary>
        private delegate void StreamConfigure(Common.StreamConfig streamConfig);
        private readonly StreamConfigure PushStreamConfig;

        /// <summary>
        /// Reference to ES Player
        /// </summary>
        private readonly ESPlayer.ESPlayer player;

        /// <summary>
        /// Reference to packet storage
        /// </summary>
        private readonly EsPlayerPacketStorage packetStorage;

        /// <summary>
        /// Reference to task responsible for transferring data
        /// between packet storage and ES Player
        /// </summary>
        private Task transferTask;

        /// <summary>
        /// Cancellation source responsible for terminating
        /// transfer task.
        /// </summary>
        private CancellationTokenSource transferCts;

        /// <summary>
        /// Stream Type as known by Juvo Player
        /// </summary>
        private readonly Common.StreamType streamTypeJuvo;

        /// <summary>
        /// Stream Type as known by ESPlayer. Corresponds to
        /// Juvo Player stream type
        /// </summary>
        private readonly ESPlayer.StreamType streamTypeEsPlayer;

        /// <summary>
        /// Currently used stream configuration
        /// </summary>
        private Common.StreamConfig currentConfig;

        /// <summary>
        /// Flag indicating if stream has been configured
        /// </summary>
        public bool IsConfigured => (currentConfig != null);

        /// <summary>
        /// Flag indicating if stream is transferring data between
        /// packet storage and ESPlayer
        /// </summary>
        public bool IsRunning => (transferTask != null);

        /// <summary>
        /// lock object used for serialization of internal operations
        /// that can be accessed externally
        /// </summary>
        private readonly Object syncLock = new Object();

        public EsStream(ESPlayer.ESPlayer player, Common.StreamType type)
        {
            this.player = player;
            streamTypeJuvo = type;
            packetStorage = EsPlayerPacketStorage.GetInstance();

            switch (streamTypeJuvo)
            {
                case StreamType.Audio:
                    PushStreamConfig = PushAudioConfig;
                    streamTypeEsPlayer = ESPlayer.StreamType.Audio;
                    break;
                case StreamType.Video:
                    PushStreamConfig = PushVideoConfig;
                    streamTypeEsPlayer = ESPlayer.StreamType.Video;
                    break;
                default:
                    logger.Warn($"Unsupported stream type: {streamTypeJuvo}");
                    break;
            }
        }

        /// <summary>
        /// Sets Stream configuration
        /// Non configured stream - stream config will be pushed directly to ES Player.
        /// Configured stream - stream config will be enqueue in packet storage
        /// and processed once retrieved.
        /// </summary>
        /// <param name="bufferConfig">BufferConfigurationPacket</param>
        public void SetStreamConfig(BufferConfigurationPacket bufferConfig)
        {
            logger.Info($"Already Configured: {IsConfigured}");

            if (IsConfigured)
            {
                packetStorage.AddPacket(bufferConfig);
                return;
            }

            PushStreamConfig(bufferConfig.Config);
        }

        private void PushAudioConfig(Common.StreamConfig streamConfig)
        {
            logger.Info($"{streamTypeJuvo}");
            AudioStreamConfig audioConfig = streamConfig as Common.AudioStreamConfig;

            if (audioConfig == null)
            {
                logger.Error("Invalid stream configuration. Not audio.");
                return;
            }

            logger.Info($"{streamTypeJuvo}: Pushing Stream Config");

            var config = new ESPlayer.AudioStreamInfo
            {
                codecData = audioConfig.CodecExtraData,
                mimeType = EsPlayerUtils.GetCodecMimeType(audioConfig.Codec),
                sampleRate = audioConfig.SampleRate,
                channels = audioConfig.ChannelLayout
            };

            if (!player.AddStream(config))
            {
                logger.Warn($"Failed to set config {streamTypeJuvo}");
                return;
            }

            logger.Info($"{streamTypeJuvo} Stream configuration added");
            currentConfig = streamConfig;
        }

        private void PushVideoConfig(Common.StreamConfig streamConfig)
        {
            logger.Info($"{streamTypeJuvo}");

            VideoStreamConfig videoConfig = streamConfig as Common.VideoStreamConfig;

            if (videoConfig == null)
            {
                logger.Error("Invalid stream configuration. Not video");
                return;
            }

            logger.Info($"{streamTypeJuvo}: Pushing Stream Config");

            var config = new ESPlayer.VideoStreamInfo
            {
                codecData = videoConfig.CodecExtraData,
                mimeType = EsPlayerUtils.GetCodecMimeType(videoConfig.Codec),
                width = videoConfig.Size.Width,
                maxWidth = videoConfig.Size.Width,
                height = videoConfig.Size.Height,
                maxHeight = videoConfig.Size.Height,
                num = videoConfig.FrameRateNum,
                den = videoConfig.FrameRateDen
            };

            if (!player.AddStream(config))
            {
                logger.Warn($"Failed to set config {streamTypeJuvo}");
                return;
            }

            logger.Info($"{streamTypeJuvo} Stream configuration added");

            currentConfig = streamConfig;
        }

        public void Start()
        {
            StartTransfer();
        }

        private void StartTransfer()
        {
            logger.Info($"{streamTypeJuvo}");

            lock (syncLock)
            {
                if (IsRunning)
                {
                    logger.Warn($"{streamTypeJuvo}: Playback not stopped. {transferTask.Status}");
                    return;
                }

                if (!IsConfigured)
                {
                    logger.Warn($"{streamTypeJuvo}: Not Configured");
                    return;
                }

                transferCts = new CancellationTokenSource();
                var token = transferCts.Token;

                transferTask = Task.Factory.StartNew(() => TransferTask(token), token);
            }
        }

        public void Stop()
        {
            StopTransfer();
        }

        private void StopTransfer()
        {
            logger.Info($"{streamTypeJuvo}");

            lock (syncLock)
            {
                if (!IsRunning)
                {
                    logger.Warn($"{streamTypeJuvo}: Playback not started");
                    return;
                }

                logger.Info($"{streamTypeJuvo} Stopping transfer");

                transferCts.Cancel();
                transferCts.Dispose();
                transferTask = null;
            }
        }

        public void Disable()
        {
            logger.Info($"{streamTypeJuvo}");

            DisableTransfer();
        }

        private void DisableTransfer()
        {
            logger.Info($"{streamTypeJuvo}");

            lock (syncLock)
            {
                // Stop Transfer and disable packet storage so no further packets 
                // can be collected.
                StopTransfer();
                packetStorage.Disable(streamTypeJuvo);
            }
        }

        private void TransferTask(CancellationToken token)
        {
            logger.Info($"{streamTypeJuvo}: Transfer task started");

            var doDisable = false;

            try
            {
                do
                {
                    var packet = packetStorage.GetPacket(streamTypeJuvo, token);

                    switch (packet)
                    {
                        case Packet eosPacket when eosPacket.IsEOS:
                            PushEosPacket(token);
                            DisableTransfer();
                            break;
                        case BufferConfigurationPacket bufferConfigPacket:
                            logger.Info($"{streamTypeJuvo}: Buffer Reconfiguration not implemented");
                            break;
                        case EncryptedPacket encryptedPacket:
                            logger.Info($"{streamTypeJuvo}: Encrypted packet not implemented");
                            break;
                        case Packet dataPacket when packet.IsEOS == false:
                            PushDataPacket(dataPacket, token);
                            break;
                    }

                    packet.Dispose();

                } while (!token.IsCancellationRequested);
            }
            catch (InvalidOperationException)
            {
                logger.Info($"{streamTypeJuvo}: Stream completed");
                doDisable = true;
            }
            catch (OperationCanceledException)
            {
                logger.Info($"{streamTypeJuvo}: Transfer stopped");

                // Operation is cancelled thus Stop/StopInternal has already been
                // called. No need to repeat.
            }
            catch (PacketSubmitException pse)
            {
                logger.Error($"{streamTypeJuvo}: Submit Error " + pse.SubmitStatus);
                doDisable = true;
            }
            finally
            {
                if (doDisable)
                    DisableTransfer();

                logger.Info($"{streamTypeJuvo}: Transfer task terminated");
            }
        }

        private void PushDataPacket(Packet dataPacket, CancellationToken token)
        {
            var doRetry = false;
            var esPacket = dataPacket.ToESPlayerPacket(streamTypeEsPlayer);

            do
            {
                var res = player.SubmitPacket(esPacket);
                doRetry = ProcessPushResult(res, token);

            } while (doRetry);
        }

        private void PushEosPacket(CancellationToken token)
        {
            logger.Info("");

            var doRetry = false;

            do
            {
                var res = player.SubmitEosPacket(streamTypeEsPlayer);
                doRetry = ProcessPushResult(res, token);

            } while (doRetry);
        }

        private bool ProcessPushResult(ESPlayer.SubmitStatus status, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                logger.Info($"{streamTypeJuvo}: Operation Cancelled");
                return false;
            }

            if (status == ESPlayer.SubmitStatus.Success)
                return false;

            if (status != ESPlayer.SubmitStatus.Full)
            {
                throw new PacketSubmitException("Packet Submit Error", status);
            }

            // We are left with Status.Full 
            // For now sleep, however, once buffer events will be 
            // emitted from ESPlayer, they could be used here
            Task.Delay(10, token);

            // recheck token - could be cancelled during wait...
            if (token.IsCancellationRequested)
            {
                logger.Info($"{streamTypeJuvo}: Operation Cancelled");
                return false;
            }

            return true;

        }

        #region IDisposable Support
        private bool isDisposed;
        public void Dispose()
        {
            if (isDisposed)
                return;

            logger.Info(streamTypeJuvo.ToString());

            if (IsRunning)
                StopTransfer();

            isDisposed = true;
        }
        #endregion
    }
}
