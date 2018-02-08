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

using CSPlayer;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Delegates;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Tizen;

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

        private readonly AutoResetEvent needDataEvent = new AutoResetEvent(false);
        private object needDataLock = new object();

        private System.UInt32 currentTime;

        public unsafe SMPlayerAdapter()
        {
            try
            {
                Tizen.Log.Info("JuvoPlayer", "SMPlayer init");

                playerInstance = new SMPlayerWrapper();
                playerInstance.RegisterPlayerEventListener(this);

                bool result = playerInstance.Initialize();
                if (!result)
                {
                    Tizen.Log.Info("JuvoPlayer", " playerInstance.Initialize() Failed !!!!!!!");
                    return;
                }
                Tizen.Log.Info("JuvoPlayer", " playerInstance.Initialize() Success !!!!!!!");

                var playerContainer = new ElmSharp.Window("player");
                playerContainer.Geometry = new ElmSharp.Rect(0, 0, 1920, 1080);
                result = playerInstance.SetDisplay(PlayerDisplayType_Samsung.PLAYER_DISPLAY_TYPE_OVERLAY, playerContainer);
                if (!result)
                {
                    Tizen.Log.Info("JuvoPlayer", " playerInstance.SetDisplay Failed !!!!!!!");
                    return;
                }
                Tizen.Log.Info("JuvoPlayer", " playerInstance.SetDisplay Success !!!!!!!");

                //The next steps of init player is as following sequences:
                //PrepareES() and Play() should be called after SetVideoStreamInfo() and SetAudioStreamInfo() success.
                //And SetVideoStreamInfo() and SetAudioStreamInfo() should be called after playerInstance.Initialize().
            }
            catch (Exception e)
            {
                Tizen.Log.Info("JuvoPlayer", "got exception: " + e.Message);
            }

            // -----------------------------------------------------------------------------------------------------------
            PacketBuffer.Ordering sortBy = PacketBuffer.Ordering.Fifo; // Change this enum if you want sort e.g. by PTS.
            // -----------------------------------------------------------------------------------------------------------

            audioBuffer = new PacketBuffer(sortBy);
            videoBuffer = new PacketBuffer(sortBy);

            Task.Run(() => SubmittingPacketsTask());
        }

        public unsafe void AppendPacket(StreamPacket packet)
        {
            // todo
            if (packet == null)
                return;

            PrepareES();

            if (packet.StreamType == StreamType.Video)
                videoBuffer.Enqueue(packet);
            else if (packet.StreamType == StreamType.Audio)
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
                    Tizen.Log.Info("JuvoPlayer", "playerInstance.PrepareES() Failed!!!!!!!!");
                    return;
                }
                Tizen.Log.Info("JuvoPlayer", "playerInstance.PrepareES() Done!!!!!!!!");
                isPlayerInitialized = true;
            }
        }

        public unsafe void SubmittingPacketsTask()
        {
            for (int i = 0; true; ++i)
            {
                if ((needDataAudio == true && needDataVideo == true) // both must be needed, so we can choose the one with lower pts
                    || (needDataInitMode == true && (needDataAudio == true || needDataVideo == true)))
                { // but for first OnNeedData - we're sending both video and audio till first OnEnoughData
                    needDataInitMode = false;

//                    Log.Info("JuvoPlayer", "SubmittingPacketsTask: Feeding (" + i.ToString() + ").");
//                    Log.Info("JuvoPlayer", "SubmittingPacketsTask: AUDIO: " + audioBuffer.Count().ToString() + ", VIDEO: " + videoBuffer.Count().ToString());

                    StreamPacket packet = DequeuePacket();

                    //                    Log.Info("JuvoPlayer", "Peeked");

                    if (packet.IsEOS)
                        SubmitEOSPacket(packet);
                    else if (packet is DecryptedEMEPacket)
                        SubmitEMEPacket(packet as DecryptedEMEPacket);
                    else
                        SubmitPacket(packet);
                }
                else
                {
                    Log.Info("JuvoPlayer", "SubmittingPacketsTask: Need to wait one.");
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
                drm_type = 15,
                tz_handle = packet.HandleSize.handle
            };

            IntPtr pnt = Marshal.AllocHGlobal(Marshal.SizeOf(drmInfo));
            try
            {
                Marshal.StructureToPtr(drmInfo, pnt, false);

//                Tizen.Log.Info("JuvoPlayer", string.Format("[HQ] send es data to SubmitPacket: {0} {1} ( {2} )", packet.Pts, drmInfo.tz_handle, trackType));

                if (!playerInstance.SubmitPacket(IntPtr.Zero, packet.HandleSize.size, packet.Pts.TotalNanoseconds(), trackType, pnt))
                {
                    packet.CleanHandle();
                    Tizen.Log.Error("JuvoPlayer", "Submiting encrypted packet failed");
                }
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
//                Tizen.Log.Info("JuvoPlayer", string.Format("[HQ] send es data to SubmitPacket: {0} ( {1} )", packet.Pts, trackType));

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

            Tizen.Log.Info("JuvoPlayer", string.Format("[HQ] send EOS packet: {0} ( {1} )", packet.Pts, trackType));

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
            Log.Info("JuvoPlayer", "");

            var audioStreamInfo = new AudioStreamInfo_Samsung
            {
                mime = Marshal.StringToHGlobalAnsi(SMPlayerUtils.GetCodecMimeType(config.Codec)),
                version = SMPlayerUtils.GetCodecVersion(config.Codec),
                drm_type = 15,  // 0 for no DRM, 15 for EME
                sample_rate = (uint)config.SampleRate,
                channels = (uint)config.ChannelLayout,
                property_type = IntPtr.Zero,                   /**< video stream info: drminfo property_type */
                type_len = 0,                           /**< video stream info: drminfo property_type length */
                property_data = IntPtr.Zero,                   /**< video stream info: drminfo property_data */
                data_len = 0,

            };

            try
            {
                if (config.CodecExtraData != null && config.CodecExtraData.Length > 0)
                {
                    int size = Marshal.SizeOf(config.CodecExtraData[0]) * config.CodecExtraData.Length;
                    audioStreamInfo.codec_extradata = Marshal.AllocHGlobal(size);
                    audioStreamInfo.extradata_size = (uint)config.CodecExtraData.Length;
                    Marshal.Copy(config.CodecExtraData, 0, audioStreamInfo.codec_extradata, config.CodecExtraData.Length);
                }

                playerInstance.SetAudioStreamInfo(audioStreamInfo);
            }
            finally
            {
                if (audioStreamInfo.codec_extradata != IntPtr.Zero)
                    Marshal.FreeHGlobal(audioStreamInfo.codec_extradata);
            }
            audioSet = true;
        }

        public void SetVideoStreamConfig(VideoStreamConfig config)
        {
            Log.Info("JuvoPlayer", "");

            var videoStreamInfo = new VideoStreamInfo_Samsung
            {
                mime = Marshal.StringToHGlobalAnsi(SMPlayerUtils.GetCodecMimeType(config.Codec)),
                version = SMPlayerUtils.GetCodecVersion(config.Codec),
                drm_type = 15,  // 0 for no DRM, 15 for EME
                framerate_num = (uint)config.FrameRateNum,
                framerate_den = (uint)config.FrameRateDen,
                width = (uint)config.Size.Width,
                max_width = (uint)config.Size.Width,
                height = (uint)config.Size.Height,
                max_height = (uint)config.Size.Height,
                property_type = IntPtr.Zero,                   /**< video stream info: drminfo property_type */
                type_len = 0,                           /**< video stream info: drminfo property_type length */
                property_data = IntPtr.Zero,                   /**< video stream info: drminfo property_data */
                data_len = 0,
            };

            try
            {
                if (config.CodecExtraData != null && config.CodecExtraData.Length > 0)
                {
                    int size = Marshal.SizeOf(config.CodecExtraData[0]) * config.CodecExtraData.Length;
                    videoStreamInfo.codec_extradata = Marshal.AllocHGlobal(size);
                    videoStreamInfo.extradata_size = (uint)config.CodecExtraData.Length;
                    Marshal.Copy(config.CodecExtraData, 0, videoStreamInfo.codec_extradata, config.CodecExtraData.Length);
                }

                playerInstance.SetVideoStreamInfo(videoStreamInfo);
            }
            finally
            {
                if (videoStreamInfo.codec_extradata != IntPtr.Zero)
                    Marshal.FreeHGlobal(videoStreamInfo.codec_extradata);
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
            playerInstance.Stop(); // This is async method - wait for D2TV_MESSAGE_STOP_SUCCESS message before doing anything else with the player.

            playCalled = false;
        }

        public void Pause() // TODO(g.skowinski): Handle asynchronicity (like in Stop() method?).
        {
            playerInstance.Pause();
        }

        #region IPlayerEventListener
        public void OnEnoughData(StreamType_Samsung streamType)
        {
            Log.Info("JuvoPlayer", "Received OnEnoughData: " + streamType.ToString());

            if (streamType == StreamType_Samsung.STREAM_TYPE_SAMSUNG_AUDIO)
                needDataAudio = false;
            else if (streamType == StreamType_Samsung.STREAM_TYPE_SAMSUNG_VIDEO)
                needDataVideo = false;
            else
                return;

            needDataInitMode = false;
        }

        public void OnNeedData(StreamType_Samsung streamType, uint size)
        {
            Log.Info("JuvoPlayer", "Received OnNeedData: " + streamType.ToString());

            if (streamType == StreamType_Samsung.STREAM_TYPE_SAMSUNG_AUDIO)
                needDataAudio = true;
            else if (streamType == StreamType_Samsung.STREAM_TYPE_SAMSUNG_VIDEO)
                needDataVideo = true;
            else
                return;

            needDataEvent.Set();
        }

        public void OnSeekData(StreamType_Samsung streamType, System.UInt64 offset)
        {
            Log.Info("JuvoPlayer", string.Format("Received OnSeekData: {0} offset: {1}", streamType, offset));
        }

        public void OnError(PlayerErrorType_Samsung errorType, string msg)
        {
            Log.Info("JuvoPlayer", string.Format("Type: {0} msg: {1}", errorType, msg));

            PlaybackError?.Invoke(msg);
        }

        public void OnMessage(PlayerMsgType_Samsung msgType)
        {
            Log.Info("JuvoPlayer", "Type" + msgType.ToString());
        }

        public void OnInitComplete()
        {
            Log.Info("JuvoPlayer", "");

            PlayerInitialized?.Invoke();
        }

        public void OnInitFailed()
        {
            Log.Info("JuvoPlayer", "");

            PlaybackError?.Invoke("Initialization error.");
        }

        public void OnEndOfStream()
        {
            Log.Info("JuvoPlayer", "");

            PlaybackCompleted?.Invoke();
        }

        public void OnSeekCompleted()
        {
            Log.Info("JuvoPlayer", "");
        }

        public void OnSeekStartedBuffering()
        {
            Log.Info("JuvoPlayer", "");
        }

        public void OnCurrentPosition(System.UInt32 currTime)
        {
            if (currentTime == currTime)
                return;

            Log.Info("JuvoPlayer", "OnCurrentPosition = " + currTime);

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
            ReleaseUnmanagedResources();
            if (disposing)
            {
                needDataEvent?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
