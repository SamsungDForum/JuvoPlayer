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
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using Tizen.TV.Smplayer;
using StreamType = Tizen.TV.Smplayer.StreamType;

namespace JuvoPlayer.Player.SMPlayer
{
    public static class TimeSpanExtensions
    {
        public static ulong TotalNanoseconds(this TimeSpan time)
        {
            return (ulong) (time.TotalMilliseconds * 1000000);
        }
    }

    public class SMPlayer : IPlayer, IPlayerEventListener
    {
        private enum SMPlayerState
        {
            Uninitialized,
            Ready,
            Playing,
            Paused,
            Stopped
        };

        private class BufferConfiguration : Packet
        {
            private BufferConfiguration() { }
            public static BufferConfiguration Create(StreamConfig config)
            {
                var result = new BufferConfiguration()
                {
                    Config = config,
                    StreamType = config.StreamType(),
                    Pts = TimeSpan.MinValue
                };

                return result;
            }

            public StreamConfig Config { get; private set; }
        };

        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public event PlaybackCompleted PlaybackCompleted;
        public event PlaybackError PlaybackError;
        public event PlayerInitialized PlayerInitialized;
        public event ShowSubtitile ShowSubtitle;
        public event TimeUpdated TimeUpdated;

        private readonly SMPlayerWrapper playerInstance;

        private ConcurrentQueue<Packet> audioPacketsQueue;
        private ConcurrentQueue<Packet> videoPacketsQueue;

        private SMPlayerState internalState = SMPlayerState.Uninitialized;

        private bool audioSet, videoSet;
        private bool needDataVideo, needDataAudio;

        private readonly AutoResetEvent wakeUpEvent = new AutoResetEvent(false);

        private System.UInt32 currentTime;

        // while SMPlayer is reconfigured after calling Seek we cant upload any packets
        // We need to wait for the first OnSeekData event what means that player is ready
        // to get packets
        private bool smplayerSeekReconfiguration;
        private readonly Task submitPacketTask;

        public SMPlayer()
        {
            try
            {
                Logger.Info("SMPlayer init");

                playerInstance = new SMPlayerWrapper();
                playerInstance.RegisterPlayerEventListener(this);

                bool result = playerInstance.Initialize();
                if (!result)
                {
                    Logger.Error("playerInstance.Initialize() Failed!!!!!!!");
                    return;
                }
                Logger.Info("playerInstance.Initialize() Success !!!!!!!");

                var playerContainer = new ElmSharp.Window("player");
                playerContainer.Geometry = new ElmSharp.Rect(0, 0, 1920, 1080);
                result = playerInstance.SetDisplay(PlayerDisplayType.Overlay, playerContainer);
                if (!result)
                {
                    Logger.Error("playerInstance.SetDisplay Failed !!!!!!!");
                    return;
                }

                Logger.Info("playerInstance.SetDisplay Success !!!!!!!");
            }
            catch (Exception e)
            {
                Logger.Error("got exception: " + e.Message);
                throw;
            }

            ResetPacketsQueues();

            submitPacketTask = Task.Run(() => SubmittingPacketsTask());
        }

        ~SMPlayer()
        {
            ReleaseUnmanagedResources();
        }

        public void AppendPacket(Packet packet)
        {
            if (packet == null)
                return;

            if (packet.StreamType == Common.StreamType.Video)
                videoPacketsQueue.Enqueue(packet);
            else if (packet.StreamType == Common.StreamType.Audio)
                audioPacketsQueue.Enqueue(packet);
            WakeUpSubmitTask();
        }

        public void PrepareES()
        {
            if (internalState != SMPlayerState.Uninitialized)
                return;

            if (!audioSet || !videoSet)
                return;

            bool result = playerInstance.PrepareES();
            if (result != true)
            {
                Logger.Error("playerInstance.PrepareES() Failed");
                return;
            }
            Logger.Info("playerInstance.PrepareES() Done");

            internalState = SMPlayerState.Ready;
        }

        internal void SubmittingPacketsTask()
        {
            while (internalState != SMPlayerState.Stopped)
            {
                Logger.Debug("SubmittingPacketsTask: AUDIO: " + audioPacketsQueue.Count + ", VIDEO: " + videoPacketsQueue.Count);
                var didSubmitPacket = false;

                if (!smplayerSeekReconfiguration)
                {
                    if (needDataAudio && audioPacketsQueue.TryDequeue(out var packet))
                    {
                        SubmitPacket(packet);
                        didSubmitPacket = true;
                    }

                    if (needDataVideo && videoPacketsQueue.TryDequeue(out packet))
                    {
                        SubmitPacket(packet);
                        didSubmitPacket = true;
                    }
                }

                if (didSubmitPacket) continue;

                try
                {
                    Logger.Debug("SubmittingPacketsTask: Need to wait one.");
                    wakeUpEvent.WaitOne();
                }
                catch (ObjectDisposedException ex)
                {
                    Logger.Warn(ex.Message);
                }
            }
        }

        private void SubmitPacket(Packet packet)
        {
            if (packet.IsEOS)
                SubmitEOSPacket(packet);
            else if (packet is EncryptedPacket)
                SubmitEncryptedPacket(packet as EncryptedPacket);
            else if (packet is BufferConfiguration)
                SubmitStreamConfiguration(packet as BufferConfiguration);
            else
                SubmitDataPacket(packet);
        }

        private void SubmitEncryptedPacket(EncryptedPacket packet)
        {
            if (packet.DrmSession == null)
            {
                SubmitDataPacket(packet);
                return;
            }

            var decryptedPacket = packet.Decrypt();
            if (decryptedPacket == null)
                throw new Exception("An error occured while decrypting encrypted packet!");

            if (decryptedPacket is DecryptedEMEPacket)
                SubmitDecryptedEmePacket(decryptedPacket as DecryptedEMEPacket);
        }

        private void SubmitDecryptedEmePacket(DecryptedEMEPacket packet)
        {
            var trackType = SMPlayerUtils.GetTrackType(packet);

            EsPlayerDrmInfo drmInfo = new EsPlayerDrmInfo
            {
                drmType = 15,
                tzHandle = packet.HandleSize.handle
            };

            IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(drmInfo));
            try
            {
                Marshal.StructureToPtr(drmInfo, pnt, false);

                Logger.Debug(string.Format("[HQ] send es data to SubmitDecryptedEmePacket: {0} {1} ( {2} )", packet.Pts, drmInfo.tzHandle, trackType));

                if (!playerInstance.SubmitPacket(IntPtr.Zero, packet.HandleSize.size, packet.Pts.TotalNanoseconds(),
                    trackType, pnt))
                {
                    Logger.Error("Submiting encrypted packet failed");
                    return;
                }

                packet.CleanHandle();
            }
            finally
            {
                // Free the unmanaged memory.
                Marshal.FreeHGlobal(pnt);
            }
        }


        private void SubmitDataPacket(Packet packet) // TODO(g.skowinski): Implement it properly.
        {
            // Initialize unmanaged memory to hold the array.
            int size = Marshal.SizeOf(packet.Data[0]) * packet.Data.Length;
            IntPtr pnt = Marshal.AllocHGlobal(size);
            try
            {
                // Copy the array to unmanaged memory. C++ can only use such unmanaged memory
                Marshal.Copy(packet.Data, 0, pnt, packet.Data.Length);

                // Copy the unmanaged array back to another managed array.
                //byte[] managedArray2 = new byte[managedArray.Length];
                //Marshal.Copy(pnt, managedArray2, 0, managedArray.Length);
                var trackType = SMPlayerUtils.GetTrackType(packet);
                Logger.Debug(string.Format("[HQ] send es data to SubmitDataPacket: {0} ( {1} )", packet.Pts, trackType));

                playerInstance.SubmitPacket(pnt, (uint)packet.Data.Length, packet.Pts.TotalNanoseconds(), trackType, IntPtr.Zero);
            }
            finally
            {
                // Free the unmanaged memory. It need to check, no need to clear here, in Amazon es play case, the es data memory is cleared by decoder or sink element after it is used and played
                  Marshal.FreeHGlobal(pnt);
            }
        }

        private void SubmitEOSPacket(Packet packet)
        {
            var trackType = SMPlayerUtils.GetTrackType(packet);

            Logger.Debug(string.Format("[HQ] send EOS packet: {0} ( {1} )", packet.Pts, trackType));

            playerInstance.SubmitEOSPacket(trackType);
        }

        private void SubmitStreamConfiguration(BufferConfiguration config)
        {
            if (config.StreamType == Common.StreamType.Video)
                SetVideoStreamConfig(config.Config as VideoStreamConfig);
            else if (config.StreamType == Common.StreamType.Audio)
                SetAudioStreamConfig(config.Config as AudioStreamConfig);
        }

        public void Play()
        {
            Logger.Debug("");

            bool ret;
            if (internalState == SMPlayerState.Paused)
                ret = playerInstance.Resume();
            else
                ret = playerInstance.Play();

            if (ret)
                internalState = SMPlayerState.Playing;
            else
                Logger.Error("Play failed.");
        }

        public void Seek(TimeSpan time)
        {
            Logger.Debug("");

            // Stop appending packests.
            smplayerSeekReconfiguration = true;

            playerInstance.Pause();

            ResetPacketsQueues();

            playerInstance.Seek((int)time.TotalMilliseconds);
        }

        private void ResetPacketsQueues()
        {
            audioPacketsQueue = new ConcurrentQueue<Packet>();
            videoPacketsQueue = new ConcurrentQueue<Packet>();
        }

        public void SetStreamConfig(StreamConfig config)
        {
            switch (config.StreamType())
            {
                case Common.StreamType.Audio:
                    if (audioPacketsQueue.Count > 0)
                    {
                        audioPacketsQueue.Enqueue(BufferConfiguration.Create(config));
                        WakeUpSubmitTask();
                    }
                    else
                        SetAudioStreamConfig(config as AudioStreamConfig);
                    break;
                case Common.StreamType.Video:
                    if (videoPacketsQueue.Count > 0)
                    {
                        videoPacketsQueue.Enqueue(BufferConfiguration.Create(config));
                        WakeUpSubmitTask();
                    }
                    else
                        SetVideoStreamConfig(config as VideoStreamConfig);
                    break;
                case Common.StreamType.Subtitle:
                case Common.StreamType.Teletext:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void WakeUpSubmitTask()
        {
            try
            {
                // this can throw when event is received after Dispose() was called
                wakeUpEvent.Set();
            }
            catch (ObjectDisposedException ex)
            {
                // ignored
            }
        }

        public void SetAudioStreamConfig(AudioStreamConfig config)
        {
            Logger.Debug("");

            var audioStreamInfo = new AudioStreamInfo
            {
                mime = Marshal.StringToHGlobalAnsi(SMPlayerUtils.GetCodecMimeType(config.Codec)),
                version = SMPlayerUtils.GetCodecVersion(config.Codec),
                drmType = 15,  // 0 for no DRM, 15 for EME
                sampleRate = (uint)config.SampleRate,
                channels = (uint)config.ChannelLayout,
            };

            try
            {
                if (config.CodecExtraData != null && config.CodecExtraData.Length > 0)
                {
                    int size = Marshal.SizeOf(config.CodecExtraData[0]) * config.CodecExtraData.Length;
                    audioStreamInfo.codecExtraAata = Marshal.AllocHGlobal(size);
                    audioStreamInfo.extraDataSize = (uint)config.CodecExtraData.Length;
                    Marshal.Copy(config.CodecExtraData, 0, audioStreamInfo.codecExtraAata, config.CodecExtraData.Length);
                }

                playerInstance.SetAudioStreamInfo(audioStreamInfo);
            }
            finally
            {
                if (audioStreamInfo.codecExtraAata != IntPtr.Zero)
                    Marshal.FreeHGlobal(audioStreamInfo.codecExtraAata);
            }
            audioSet = true;

            PrepareES();
        }

        public void SetVideoStreamConfig(VideoStreamConfig config)
        {
            Logger.Debug("");

            var videoStreamInfo = new VideoStreamInfo
            {
                mime = Marshal.StringToHGlobalAnsi(SMPlayerUtils.GetCodecMimeType(config.Codec)),
                version = SMPlayerUtils.GetCodecVersion(config.Codec),
                drmType = 15,  // 0 for no DRM, 15 for EME
                framerateNum = (uint)config.FrameRateNum,
                framerateDen = (uint)config.FrameRateDen,
                width = (uint)config.Size.Width,
                maxWidth = (uint)config.Size.Width,
                height = (uint)config.Size.Height,
                maxHeight = (uint)config.Size.Height,
            };

            try
            {
                if (config.CodecExtraData != null && config.CodecExtraData.Length > 0)
                {
                    int size = Marshal.SizeOf(config.CodecExtraData[0]) * config.CodecExtraData.Length;
                    videoStreamInfo.codecExtraData = Marshal.AllocHGlobal(size);
                    videoStreamInfo.extraDataSize = (uint)config.CodecExtraData.Length;
                    Marshal.Copy(config.CodecExtraData, 0, videoStreamInfo.codecExtraData, config.CodecExtraData.Length);
                }

                playerInstance.SetVideoStreamInfo(videoStreamInfo);
            }
            finally
            {
                if (videoStreamInfo.codecExtraData != IntPtr.Zero)
                    Marshal.FreeHGlobal(videoStreamInfo.codecExtraData);
            }
            videoSet = true;

            PrepareES();
        }

        public void SetDuration(TimeSpan duration)
        {
            Logger.Debug("");

            playerInstance.SetDuration((uint)duration.TotalMilliseconds);
        }

        public void SetExternalSubtitles(string file)
        {
            Logger.Debug("");

            playerInstance.SetExternalSubtitlesPath(file, string.Empty);
        }

        public void SetPlaybackRate(float rate)
        {
            Logger.Debug("");

            playerInstance.SetPlaybackRate(rate);
        }

        public void SetSubtitleDelay(int offset)
        {
            Logger.Debug("");

            //TODO(p.galiszewsk): check time format
            playerInstance.SetSubtitlesDelay(offset);
        }

        public void Stop()
        {
            Logger.Debug("");
            if (internalState == SMPlayerState.Stopped)
                return;

            internalState = SMPlayerState.Stopped;

            ResetPacketsQueues();

            WakeUpSubmitTask();
            submitPacketTask.Wait();

            playerInstance.Stop();
        }

        public void Pause()
        {
            Logger.Debug("");

            if (playerInstance.Pause())
                internalState = SMPlayerState.Paused;
            else
                Logger.Error("Pause failed.");
        }

        #region IPlayerEventListener
        public void OnEnoughData(StreamType streamType)
        {
            Logger.Debug("Received OnEnoughData: " + streamType);

            if (streamType == StreamType.Audio)
                needDataAudio = false;
            else if (streamType == StreamType.Video)
                needDataVideo = false;
        }

        public void OnNeedData(StreamType streamType, uint size)
        {
            Logger.Debug("Received OnNeedData: " + streamType);

            if (streamType == StreamType.Audio)
                needDataAudio = true;
            else if (streamType == StreamType.Video)
                needDataVideo = true;
            else
                return;

            WakeUpSubmitTask();
        }

        public void OnSeekData(StreamType streamType, System.UInt64 offset)
        {
            Logger.Debug(string.Format("Received OnSeekData: {0} offset: {1}", streamType, offset));

            if (streamType == StreamType.Audio)
                needDataAudio = true;
            else if (streamType == StreamType.Video)
                needDataVideo = true;
            else
                return;

            // We can start appending packets
            smplayerSeekReconfiguration = false;

            WakeUpSubmitTask();
        }

        public void OnError(PlayerErrorType errorType, string msg)
        {
            Logger.Info(string.Format("Type: {0} msg: {1}", errorType, msg));

            PlaybackError?.Invoke(msg);
        }

        public void OnMessage(PlayerMsgType msgType)
        {
            Logger.Info("Type" + msgType);
        }

        public void OnInitComplete()
        {
            Logger.Info("");

            PlayerInitialized?.Invoke();
        }

        public void OnInitFailed()
        {
            Logger.Info("");

            PlaybackError?.Invoke("Initialization error.");
        }

        public void OnEndOfStream()
        {
            Logger.Info("");

            PlaybackCompleted?.Invoke();
        }

        public void OnSeekCompleted()
        {
            Logger.Info("");

            if (internalState == SMPlayerState.Playing)
                playerInstance.Resume();

            TimeUpdated?.Invoke(TimeSpan.FromMilliseconds(playerInstance.currentPosition * 1000));
        }

        public void OnSeekStartedBuffering()
        {
            Logger.Info("");
        }

        public void OnCurrentPosition(System.UInt32 currTime)
        {
            if (currentTime == currTime)
                return;

            Logger.Info("OnCurrentPosition = " + currTime);

            GC.Collect();

            currentTime = currTime;

            TimeUpdated?.Invoke(TimeSpan.FromMilliseconds(currentTime));
        }

        #endregion

        private void ReleaseUnmanagedResources()
        {
            playerInstance.DestroyHandler();
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop();
                wakeUpEvent.Dispose();
            }
            ReleaseUnmanagedResources();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
