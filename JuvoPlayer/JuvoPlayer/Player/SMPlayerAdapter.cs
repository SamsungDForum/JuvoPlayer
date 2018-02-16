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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Delegates;
using JuvoPlayer.Common.Logging;
using Tizen.TV.Smplayer;
using StreamType = Tizen.TV.Smplayer.StreamType;

namespace JuvoPlayer.Player
{
    public static class TimeSpanExtensions
    {
        public static ulong TotalNanoseconds(this TimeSpan time)
        {
            return (ulong) (time.TotalMilliseconds * 1000000);
        }
    }

    public unsafe class SMPlayerAdapter : IPlayerAdapter, IPlayerEventListener
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public event PlaybackCompleted PlaybackCompleted;
        public event PlaybackError PlaybackError;
        public event PlayerInitialized PlayerInitialized;
        public event ShowSubtitile ShowSubtitle;
        public event TimeUpdated TimeUpdated;

        private readonly SMPlayerWrapper playerInstance;

        private bool audioSet, videoSet, isPlayerInitialized, playCalled;

        private readonly PacketBuffer audioBuffer;
        private readonly PacketBuffer videoBuffer;

        private bool needDataVideo, needDataAudio;
        private bool needDataInitMode = true;
        private bool stopped;

        private readonly AutoResetEvent needDataEvent = new AutoResetEvent(false);
        private object needDataLock = new object();

        private System.UInt32 currentTime;

        public unsafe SMPlayerAdapter()
        {
            try
            {
                Logger.Info("SMPlayer init");

                playerInstance = new SMPlayerWrapper();
                playerInstance.RegisterPlayerEventListener(this);

                bool result = playerInstance.Initialize();
                if (!result)
                {
                    Logger.Info(" playerInstance.Initialize() Failed !!!!!!!");
                    return;
                }
                Logger.Info(" playerInstance.Initialize() Success !!!!!!!");

                var playerContainer = new ElmSharp.Window("player");
                playerContainer.Geometry = new ElmSharp.Rect(0, 0, 1920, 1080);
                result = playerInstance.SetDisplay(PlayerDisplayType.Overlay, playerContainer);
                if (!result)
                {
                    Logger.Info(" playerInstance.SetDisplay Failed !!!!!!!");
                    return;
                }
                Logger.Info(" playerInstance.SetDisplay Success !!!!!!!");

                //The next steps of init player is as following sequences:
                //PrepareES() and Play() should be called after SetVideoStreamInfo() and SetAudioStreamInfo() success.
                //And SetVideoStreamInfo() and SetAudioStreamInfo() should be called after playerInstance.Initialize().
            }
            catch (Exception e)
            {
                Logger.Info("got exception: " + e.Message);
            }

            // -----------------------------------------------------------------------------------------------------------
            PacketBuffer.Ordering sortBy = PacketBuffer.Ordering.Fifo; // Change this enum if you want sort e.g. by PTS.
            // -----------------------------------------------------------------------------------------------------------

            audioBuffer = new PacketBuffer(sortBy);
            videoBuffer = new PacketBuffer(sortBy);

            Task.Run(() => SubmittingPacketsTask());
        }

        ~SMPlayerAdapter()
        {
            ReleaseUnmanagedResources();
        }

        public unsafe void AppendPacket(StreamPacket packet)
        {
            // todo
            if (packet == null)
                return;

            PrepareES();

            if (packet.StreamType == Common.StreamType.Video)
                videoBuffer.Enqueue(packet);
            else if (packet.StreamType == Common.StreamType.Audio)
                audioBuffer.Enqueue(packet);
        }

        public void PrepareES()
        {
            while (audioSet != true || videoSet != true)
                continue;

            if (isPlayerInitialized != true)
            {
                bool result = playerInstance.PrepareES();
                if (result != true)
                {
                    Logger.Info("playerInstance.PrepareES() Failed!!!!!!!!");
                    return;
                }
                Logger.Info("playerInstance.PrepareES() Done!!!!!!!!");
                isPlayerInitialized = true;
            }
        }

        public unsafe void SubmittingPacketsTask()
        {
            while (true)
            {
                if (stopped)
                    return;

                if ((needDataAudio && needDataVideo) // both must be needed, so we can choose the one with lower pts
                    || (needDataInitMode && (needDataAudio || needDataVideo)))
                { // but for first OnNeedData - we're sending both video and audio till first OnEnoughData
                    needDataInitMode = false;

//                    Logger.Info("SubmittingPacketsTask: AUDIO: " + audioBuffer.Count().ToString() + ", VIDEO: " + videoBuffer.Count().ToString());

                    StreamPacket packet = DequeuePacket();

                    //                    Logger.Info("Peeked");

                    if (packet.IsEOS)
                        SubmitEOSPacket(packet);
                    else if (packet is DecryptedEMEPacket)
                        SubmitEMEPacket(packet as DecryptedEMEPacket);
                    else
                        SubmitPacket(packet);
                }
                else
                {
                    // Logger.Info("SubmittingPacketsTask: Need to wait one.");

                    needDataEvent.WaitOne();
                }
            }
        }

        private StreamPacket DequeuePacket()
        {
            StreamPacket packet;
            if (audioBuffer.Count() > 0 && videoBuffer.Count() > 0)
            {
                if (audioBuffer.PeekSortingValue() <= videoBuffer.PeekSortingValue())
                    packet = audioBuffer.Dequeue();
                else
                    packet = videoBuffer.Dequeue();
            }
            else if (audioBuffer.Count() > 0)
                packet = audioBuffer.Dequeue();
            else
                packet = videoBuffer.Dequeue();

            return packet;
        }

        private unsafe void SubmitEMEPacket(DecryptedEMEPacket packet)
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

//                Logger.Info(string.Format("[HQ] send es data to SubmitPacket: {0} {1} ( {2} )", packet.Pts, drmInfo.tzHandle, trackType));

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


        private unsafe void SubmitPacket(StreamPacket packet) // TODO(g.skowinski): Implement it properly.
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
//                Logger.Info(string.Format("[HQ] send es data to SubmitPacket: {0} ( {1} )", packet.Pts, trackType));

                playerInstance.SubmitPacket(pnt, (uint)packet.Data.Length, packet.Pts.TotalNanoseconds(), trackType, IntPtr.Zero);
            }
            finally
            {
                // Free the unmanaged memory. It need to check, no need to clear here, in Amazon es play case, the es data memory is cleared by decoder or sink element after it is used and played
                  Marshal.FreeHGlobal(pnt);
            }
        }

        private unsafe void SubmitEOSPacket(StreamPacket packet)
        {
            var trackType = SMPlayerUtils.GetTrackType(packet);

            Logger.Info(string.Format("[HQ] send EOS packet: {0} ( {1} )", packet.Pts, trackType));

            playerInstance.SubmitEOSPacket(trackType);
        }

        public void Play() // TODO(g.skowinski): Handle asynchronicity (like in Stop() method?)
        {
            //TODO(p.galiszewsk) HACK
            if (playCalled)
                playerInstance.Resume();
            else
                playerInstance.Play();

            playCalled = true;
        }

        public void Seek(TimeSpan time)
        {
            playerInstance.Seek((int)time.TotalMilliseconds);
        }

        public void SetAudioStreamConfig(AudioStreamConfig config)
        {
            Logger.Info("");

            var audioStreamInfo = new AudioStreamInfo
            {
                mime = Marshal.StringToHGlobalAnsi(SMPlayerUtils.GetCodecMimeType(config.Codec)),
                version = SMPlayerUtils.GetCodecVersion(config.Codec),
                drmType = 15,  // 0 for no DRM, 15 for EME
                sampleRate = (uint)config.SampleRate,
                channels = (uint)config.ChannelLayout,
                propertyType = IntPtr.Zero,                   /**< video stream info: drminfo propertyType */
                typeLen = 0,                           /**< video stream info: drminfo propertyType length */
                propertyData = IntPtr.Zero,                   /**< video stream info: drminfo propertyData */
                dataLen = 0,

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
        }

        public void SetVideoStreamConfig(VideoStreamConfig config)
        {
            Logger.Info("");

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
                propertyType = IntPtr.Zero,                   /**< video stream info: drminfo propertyType */
                typeLen = 0,                           /**< video stream info: drminfo propertyType length */
                propertyData = IntPtr.Zero,                   /**< video stream info: drminfo propertyData */
                dataLen = 0,
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
        }

        public void SetDuration(TimeSpan duration)
        {
            playerInstance.SetDuration((uint)duration.TotalMilliseconds);
        }

        public void SetExternalSubtitles(string file)
        {
            playerInstance.SetExternalSubtitlesPath(file, string.Empty);
        }

        public void SetPlaybackRate(float rate)
        {
            playerInstance.SetPlaybackRate(rate);
        }

        public void SetSubtitleDelay(int offset)
        {
            //TODO(p.galiszewsk): check time format
            playerInstance.SetSubtitlesDelay(offset);
        }

        public void Stop() // TODO(g.skowinski): Handle asynchronicity.
        {
            stopped = true;

            audioBuffer.Clear();
            videoBuffer.Clear();

            playerInstance.Stop(); // This is async method - wait for D2TV_MESSAGE_STOP_SUCCESS message before doing anything else with the player.

            playCalled = false;
        }

        public void Pause() // TODO(g.skowinski): Handle asynchronicity (like in Stop() method?).
        {
            playerInstance.Pause();
        }

        #region IPlayerEventListener
        public void OnEnoughData(StreamType streamType)
        {
            // Logger.Info("Received OnEnoughData: " + streamType);

            if (streamType == StreamType.Audio)
                needDataAudio = false;
            else if (streamType == StreamType.Video)
                needDataVideo = false;
            else
                return;

            needDataInitMode = false;
        }

        public void OnNeedData(StreamType streamType, uint size)
        {
            // Logger.Info("Received OnNeedData: " + streamType);

            if (streamType == StreamType.Audio)
                needDataAudio = true;
            else if (streamType == StreamType.Video)
                needDataVideo = true;
            else
                return;

            try
            {
                // this can throw when event is received after Dispose() was called
                needDataEvent.Set();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void OnSeekData(StreamType streamType, System.UInt64 offset)
        {
            Logger.Info(string.Format("Received OnSeekData: {0} offset: {1}", streamType, offset));
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
            playerInstance.Reset();
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                stopped = true;
                audioBuffer.Clear();
                videoBuffer.Clear();

                needDataEvent?.Set();
                needDataEvent?.Dispose();
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
