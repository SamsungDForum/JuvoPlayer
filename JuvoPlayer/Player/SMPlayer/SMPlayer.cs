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
using System.Linq;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using JuvoLogger;
using Tizen.TV.Multimedia.IPTV;
using StreamType = Tizen.TV.Multimedia.IPTV.StreamType;

namespace JuvoPlayer.Player.SMPlayer
{
    public static class TimeSpanExtensions
    {
        public static ulong TotalNanoseconds(this TimeSpan time)
        {
            return (ulong)(time.TotalMilliseconds * 1000000);
        }

        public static TimeSpan FromNanoseconds(this UInt64 nanoTime)
        {
            return TimeSpan.FromMilliseconds(nanoTime / 1000000);
        }
    }

    public sealed class SMPlayer : IPlayer, IPlayerEventListener
    {
        private enum SMPlayerState
        {
            Uninitialized,
            Ready,
            Playing,
            Paused,
            Stopping
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
        public event SeekCompleted SeekCompleted;
        public event TimeUpdated TimeUpdated;

        private readonly SmplayerWrapper playerInstance;

        private ConcurrentQueue<Packet> audioPacketsQueue;
        private ConcurrentQueue<Packet> videoPacketsQueue;

        private SMPlayerState internalState = SMPlayerState.Uninitialized;

        private bool audioSet, videoSet;
        private bool needDataVideo, needDataAudio;

        private readonly AutoResetEvent submitting = new AutoResetEvent(false);

        private TimeSpan currentTime;

        private TimeSpan seekToTime;
        bool seekInProgress = false;

        private bool isDisposed;

        // while SMPlayer is reconfigured after calling Seek we cant upload any packets
        // We need to wait for the first OnSeekData event what means that player is ready
        // to get packets
        private bool smplayerSeekReconfiguration;
        private Task submitPacketTask;

        public SMPlayer()
        {
            try
            {
                Logger.Info("SMPlayer init");

                playerInstance = new SmplayerWrapper();
                playerInstance.RegisterPlayerEventListener(this);

                bool result = playerInstance.Initialize(true);
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
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
                throw new ObjectDisposedException("SMPlayer object is already disposed");
        }

        public void AppendPacket(Packet packet)
        {
            ThrowIfDisposed();

            if (packet == null)
                return;

            if (packet.StreamType == Common.StreamType.Video)
                videoPacketsQueue.Enqueue(packet);
            else if (packet.StreamType == Common.StreamType.Audio)
                audioPacketsQueue.Enqueue(packet);
            WakeUpSubmitTask();
        }

        private bool SeekVideo()
        {
            bool videoSeekReached = false;
            TimeSpan lastPts;

            while (videoPacketsQueue.TryPeek(out var packet))
            {
                if (packet.IsEOS || packet is BufferConfiguration)
                {
                    videoPacketsQueue.TryDequeue(out packet);
                    Logger.Warn("Video EOS/BufferConfiguration packet found during seek!");
                    continue;
                }

                lastPts = packet.Pts;
                if (packet.Pts < seekToTime)
                {
                    videoPacketsQueue.TryDequeue(out packet);
                }
                else
                {
                    videoSeekReached = packet.IsKeyFrame;
                    if (packet.IsKeyFrame == false)
                    {
                        if (videoPacketsQueue.TryDequeue(out packet))
                        {
                            // We have reached seekToTime, but no key frame found.
                            // increase seek to time to eat away audio data.
                            seekToTime = packet.Pts;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            Logger.Debug($"Video Seek to {seekToTime} Last PTS {lastPts} Found {videoSeekReached}");
            return videoSeekReached;
        }

        private bool SeekAudio()
        {
            bool audioSeekReached = false;
            TimeSpan lastPts;

            while (audioPacketsQueue.TryPeek(out var packet))
            {
                if (packet.IsEOS || packet is BufferConfiguration)
                {
                    audioPacketsQueue.TryDequeue(out packet);
                    Logger.Warn("Audio EOS/BufferConfiguration packet found during seek!");
                    continue;
                }

                lastPts = packet.Pts;
                if (packet.Pts < seekToTime)
                {
                    audioPacketsQueue.TryDequeue(out packet);
                }
                else
                {
                    audioSeekReached = true;
                    break;
                }
            }

            Logger.Debug($"Audio Seek to {seekToTime} Last PTS {lastPts} Found {audioSeekReached}");
            return audioSeekReached;
        }

        private bool SeekAV()
        {
            bool videoReached = SeekVideo();
            bool audioReached = SeekAudio();

            // If Audio & Video have been seeked to seek point, re-enable normal processing
            return !(videoReached && audioReached);
        }

        internal void SubmittingPacketsTask()
        {
            while (internalState != SMPlayerState.Stopping)
            {
                Logger.Debug("AUDIO: " + audioPacketsQueue.Count + ", VIDEO: " + videoPacketsQueue.Count);
                var moreDataToProcess = false;

                if (!smplayerSeekReconfiguration)
                {
                    if (seekInProgress)
                    {
                        // Seek AV returns TRUE when seek point has NOT been reached.
                        seekInProgress = SeekAV();

                        // Exit loop if seek completed;
                        if (seekInProgress == false)
                        {
                            Logger.Info($"Seek Key Frame completed @{seekToTime}");
                            continue;
                        }

                        // Contiue processing ONLY if both queues have data
                        // Data in one queue means one stream reached seek point.
                        // If seek point reach
                        moreDataToProcess = (audioPacketsQueue.Count > 0 && videoPacketsQueue.Count > 0);
                    }
                    else
                    {
                        if (needDataAudio && audioPacketsQueue.TryDequeue(out var packet))
                        {
                            SubmitPacket(packet);
                            moreDataToProcess = true;
                        }

                        if (needDataVideo && videoPacketsQueue.TryDequeue(out packet))
                        {
                            SubmitPacket(packet);
                            moreDataToProcess = true;
                        }
                    }
                }

                if (moreDataToProcess) continue;

                try
                {
                    Logger.Debug("SubmittingPacketsTask: Need to wait one.");
                    submitting.WaitOne();
                }
                catch (ObjectDisposedException ex)
                {
                    Logger.Warn(ex.ToString());
                }
            }
        }

        private void SubmitPacket(Packet packet)
        {
            ThrowIfDisposed();

            if (packet.IsEOS)
                SubmitEOSPacket(packet);
            else if (packet is EncryptedPacket)
                SubmitEncryptedPacket((EncryptedPacket)packet);
            else if (packet is BufferConfiguration)
                SubmitStreamConfiguration((BufferConfiguration)packet);
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

            try
            {
                using (var decryptedPacket = packet.Decrypt())
                {
                    SubmitDecryptedEmePacket(decryptedPacket as DecryptedEMEPacket);
                }
            }
            catch (ObjectDisposedException)
            {
                // decryptions has been canceled - drm session is already disposed
                Logger.Warn("Ignoring decryption error - drm session has been already closed");
            }
            catch (Exception e)
            {
                //log immediately, since the exception will propagate from the task sometime later
                Logger.Error($"{e}");
                throw;
            }
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
            ThrowIfDisposed();

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
            Logger.Info("");
            ThrowIfDisposed();

            if (playerInstance.Pause() == false)
            {
                Logger.Error("Pause Failed. Seek may fail!");
            }

            // Stop appending packests.
            smplayerSeekReconfiguration = true;

            seekToTime = time;
            seekInProgress = true;

            playerInstance.Seek((int)time.TotalMilliseconds);

            // Reset packet queue as late as possible to remove any stale data that might
            // be put there by still running data provider client.
            ResetPacketsQueues();
        }

        private void ResetPacketsQueues()
        {
            audioPacketsQueue = new ConcurrentQueue<Packet>();
            videoPacketsQueue = new ConcurrentQueue<Packet>();
        }

        public void SetStreamConfig(StreamConfig config)
        {
            Logger.Debug($"{config.StreamType()}");
            ThrowIfDisposed();

            if (config.StreamType() != Common.StreamType.Audio
                && config.StreamType() != Common.StreamType.Video)
                throw new NotImplementedException();

            if (internalState != SMPlayerState.Uninitialized)
            {
                EnqueueStreamConfig(config);
                return;
            }

            SetStreamConfigSync(config);

            lock (this)
            {
                if (audioSet && videoSet && internalState == SMPlayerState.Uninitialized)
                    StartEsMode();
            }
        }

        private void StartEsMode()
        {
            // This should not happen. We check state in SetStreamConfig method
            if (submitPacketTask != null && submitPacketTask.IsCompleted == false)
                throw new Exception("Invalid state when starting player es mode");

            if (!playerInstance.PrepareES())
            {
                Logger.Error("playerInstance.PrepareES() Failed");
                throw new Exception("playerInstance.PrepareES() Failed");
            }

            internalState = SMPlayerState.Ready;

            Logger.Debug("Spawning submitter task");

            submitPacketTask = Task.Factory.StartNew(SubmittingPacketsTask, TaskCreationOptions.LongRunning);
        }

        private void SetStreamConfigSync(StreamConfig config)
        {
            switch (config.StreamType())
            {
                case Common.StreamType.Audio:
                    SetAudioStreamConfig(config as AudioStreamConfig);
                    audioSet = true;
                    break;
                case Common.StreamType.Video:
                    SetVideoStreamConfig(config as VideoStreamConfig);
                    videoSet = true;
                    break;
            }
        }

        private void EnqueueStreamConfig(StreamConfig config)
        {
            // This should not happen. We check state in SetStreamConfig method
            if (!audioSet || !videoSet)
                throw new Exception("Invalid state when enqueuing stream configuration");

            var bufferedConfig = BufferConfiguration.Create(config);
            switch (config.StreamType())
            {
                case Common.StreamType.Audio:
                    audioPacketsQueue.Enqueue(bufferedConfig);
                    break;
                case Common.StreamType.Video:
                    videoPacketsQueue.Enqueue(bufferedConfig);
                    break;
            }

            WakeUpSubmitTask();
        }

        private void WakeUpSubmitTask([CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
        {
            Logger.Debug($"called from {file.Split('/').Last()}:{line} - {func}()");
            submitting.Set();
        }

        public void SetAudioStreamConfig(AudioStreamConfig config)
        {
            Logger.Debug("");
            ThrowIfDisposed();

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
                    audioStreamInfo.codecExtraData = Marshal.AllocHGlobal(size);
                    audioStreamInfo.extraDataSize = (uint)config.CodecExtraData.Length;
                    Marshal.Copy(config.CodecExtraData, 0, audioStreamInfo.codecExtraData, config.CodecExtraData.Length);
                }

                playerInstance.SetAudioStreamInfo(audioStreamInfo);
            }
            finally
            {
                if (audioStreamInfo.codecExtraData != IntPtr.Zero)
                    Marshal.FreeHGlobal(audioStreamInfo.codecExtraData);
            }
        }

        public void SetVideoStreamConfig(VideoStreamConfig config)
        {
            Logger.Debug("");
            ThrowIfDisposed();

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
        }

        public void SetDuration(TimeSpan duration)
        {
            Logger.Debug("");
            ThrowIfDisposed();

            playerInstance.SetDuration((uint)duration.TotalMilliseconds);
        }

        public void SetPlaybackRate(float rate)
        {
            Logger.Debug("");
            ThrowIfDisposed();

            playerInstance.SetPlaySpeed(rate);
        }

        public void Stop()
        {
            Logger.Info("");
            ThrowIfDisposed();

            if (internalState == SMPlayerState.Stopping)
                return;

            // Uninitialized state can switch to Ready during SetStreamConfig.
            // Calling stop during SetStreamConfig would silently would silently ignore Stop.
            // Transition from Uninitialized to Ready would occour.
            // Forcing state from uninitialized to Stopping within a lock used in SetStreamConfig
            // should prevent this case
            //
            // TODO: Change state setting to Interlocked.Exchange/Compare (?)
            // Will allow to get rid of lock(). 
            // Note: Interloacked API does not work with enums.
            //
            lock (this)
            {
                if (internalState == SMPlayerState.Uninitialized)
                {
                    internalState = SMPlayerState.Stopping;
                    return;
                }
            }


            internalState = SMPlayerState.Stopping;

            WakeUpSubmitTask();
            try
            {
                submitPacketTask?.Wait();
                ResetPacketsQueues();
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    if (e is DrmException == false)
                        return false;

                    Logger.Error($"{e}");
                    //todo(m.rybinski): notify the user here (or preferably back up)
                    return true;
                });
            }
            finally
            {
                playerInstance.Stop();

                ResetInternalState();
            }
        }

        private void ResetInternalState()
        {
            needDataAudio = false;
            needDataVideo = false;
            audioSet = false;
            videoSet = false;
            smplayerSeekReconfiguration = false;
            seekInProgress = false;

            currentTime = TimeSpan.Zero;

            internalState = SMPlayerState.Uninitialized;
        }

        public void Pause()
        {
            Logger.Debug("");
            ThrowIfDisposed();

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
            {
                needDataAudio = true;
            }
            else if (streamType == StreamType.Video)
            {
                needDataVideo = true;
            }
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

            SeekCompleted?.Invoke();

            TimeUpdated?.Invoke(TimeSpan.FromMilliseconds(playerInstance.currentPosition * 1000));
        }

        public void OnSeekStartedBuffering()
        {
            Logger.Info("");
        }

        public void OnCurrentPosition(uint currTime)
        {
            var currTimeSpan = TimeSpan.FromMilliseconds(currTime);
            if (currentTime == currTimeSpan)
                return;

            Logger.Info("OnCurrentPosition = " + currTimeSpan);

            currentTime = currTimeSpan;

            // TODO: Remove this code when even serialization will be merged.
            // This is a temporary workaround for SM Player to prevent stale time events from 
            // being sent up the pipeline.
            //
            if (seekInProgress)
                return;

            TimeUpdated?.Invoke(currentTime);
        }

        #endregion
        private void ReleaseUnmanagedResources()
        {
            playerInstance?.DestroyHandler();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            Stop();
            submitting.Dispose();

            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);

            isDisposed = true;
        }

        ~SMPlayer()
        {
            ReleaseUnmanagedResources();
        }
    }
}
