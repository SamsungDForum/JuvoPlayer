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
    unsafe class CSDemoPlayerListener : IPlayerEventListener
    {
        private SMPlayerAdapter playerAdapter;

        public CSDemoPlayerListener(SMPlayerAdapter playerInstance)
        {
            playerAdapter = playerInstance;
        }

        public void OnEnoughData(StreamType_Samsung streamType)
        {
            if (streamType == StreamType_Samsung.STREAM_TYPE_SAMSUNG_AUDIO)
            {
                Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnEnoughData : STREAM_TYPE_SAMSUNG_AUDIO!");
                playerAdapter.EnoughData(StreamType.Audio);
            }
            else
            {
                Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnEnoughData : STREAM_TYPE_SAMSUNG_VIDEO!");
                playerAdapter.EnoughData(StreamType.Video);
            }
        }

        public void OnNeedData(StreamType_Samsung streamType, uint size)
        {
            if (streamType == StreamType_Samsung.STREAM_TYPE_SAMSUNG_AUDIO)
            {
                Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnNeedData : STREAM_TYPE_SAMSUNG_AUDIO!");
                playerAdapter.NeedData(StreamType.Audio);
            }
            else
            {
                Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnNeedData : STREAM_TYPE_SAMSUNG_VIDEO!");
                playerAdapter.NeedData(StreamType.Video);
            }
        }

        public void OnSeekData(StreamType_Samsung streamType, System.UInt64 offset)
        {
            if (streamType == StreamType_Samsung.STREAM_TYPE_SAMSUNG_AUDIO)
            {
                string msg = "CSDemoPlayerListener::OnSeekData : STREAM_TYPE_SAMSUNG_AUDIO offset = " + offset;
                Tizen.Log.Info("JuvoPlayer", msg);
            }
            else
            {
                string msg = "CSDemoPlayerListener::OnSeekData : STREAM_TYPE_SAMSUNG_VIDEO offset = " + offset;
                Tizen.Log.Info("JuvoPlayer", msg);
            }
        }

        public void OnError(PlayerErrorType_Samsung errorType, string msg)
        {
            string msg1 = "CSDemoPlayerListener::OnError msg:" + msg;
            Tizen.Log.Info("JuvoPlayer", msg1);
        }

        public void OnMessage(PlayerMsgType_Samsung msgType)
        {
            string msg = "CSDemoPlayerListener::OnMessage Type:" + msgType;
            Tizen.Log.Info("JuvoPlayer", msg);
        }

        public void OnInitComplete()
        {
            Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnInitComplete!");
        }

        public void OnInitFailed()
        {
            Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnInitFailed!");
        }

        public void OnEndOfStream()
        {
            Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnEndOfStream!");
        }

        public void OnSeekCompleted()
        {
            Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnSeekCompleted!");
        }

        public void OnSeekStartedBuffering()
        {
            Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnSeekStartedBuffering!");
        }

        public void OnCurrentPosition(System.UInt32 lCurrTime)
        {
            string msg = "CSDemoPlayerListener::OnCurrentPosition = " + lCurrTime;
            Tizen.Log.Info("JuvoPlayer", msg);

            playerAdapter.OnTimeUpdated(lCurrTime);
        }
    }

    unsafe public class SMPlayerAdapter : IPlayerAdapter
    {
        public event PlaybackCompleted PlaybackCompleted;
        public event ShowSubtitile ShowSubtitle;
        public event TimeUpdated TimeUpdated;

        SMPlayerWrapper playerInstance;
        static public HandleRef refMainStage;

        bool audioSet, videoSet, playCalled, isPlayerInit;

        PacketBuffer audioBuffer, videoBuffer;

        bool needDataVideo, needDataAudio, needDataInitMode;
        AutoResetEvent needDataEvent;
        object needDataLock;

        unsafe public SMPlayerAdapter()
        {
            try
            {
                playerInstance = new SMPlayerWrapper();
                var playerEventListener = new CSDemoPlayerListener(this);
                playerInstance.RegisterPlayerEventListener(playerEventListener);

                bool result = playerInstance.Initialize(true);
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

                audioSet = videoSet = isPlayerInit = playCalled = false;
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

            needDataVideo = false;
            needDataAudio = false;
            needDataInitMode = true;
            needDataEvent = new AutoResetEvent(false);
            needDataLock = new object();

            Task.Run(() => SubmittingPacketsTask());
        }

        ~SMPlayerAdapter()
        {
            playerInstance.DestroyHandler();
        }

        unsafe public void AppendPacket(StreamPacket packet)
        {
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

            if (isPlayerInit != true)
            {
                bool result = playerInstance.PrepareES();
                if (result != true)
                {
                    Tizen.Log.Info("JuvoPlayer", "playerInstance.PrepareES() Failed!!!!!!!!");
                    return;
                }
                Tizen.Log.Info("JuvoPlayer", "playerInstance.PrepareES() Done!!!!!!!!");
                isPlayerInit = true;
            }

            if (playCalled != true)
            {
                bool result = playerInstance.Play();
                if (result != true)
                {
                    Tizen.Log.Info("JuvoPlayer", "playerInstance.Play() Failed!!!!!!!!");
                    return;
                }
                playCalled = true;
            }
        }

        unsafe public void SubmittingPacketsTask()
        {
            for (int i = 0; true; ++i)
            {
                if ((needDataAudio == true && needDataVideo == true) // both must be needed, so we can choose the one with lower pts
                    || (needDataInitMode == true && (needDataAudio == true || needDataVideo == true)))
                { // but for first OnNeedData - we're sending both video and audio till first OnEnoughData
                    needDataInitMode = false;

                    Log.Info("JuvoPlayer", "SubmittingPacketsTask: Feeding (" + i.ToString() + ").");
                    Log.Info("JuvoPlayer", "SubmittingPacketsTask: AUDIO: " + audioBuffer.Count().ToString() + ", VIDEO: " + videoBuffer.Count().ToString());

                    StreamPacket packet;
                    if (audioBuffer.PeekSortingValue() <= videoBuffer.PeekSortingValue())
                        packet = audioBuffer.Dequeue();
                    else
                        packet = videoBuffer.Dequeue();

                    Log.Info("JuvoPlayer", "Peeked");

                    SubmitPacket(packet);
                }
                else
                {
                    Log.Info("JuvoPlayer", "SubmittingPacketsTask: Need to wait one.");
                    needDataEvent.WaitOne();
                }
            }
        }

        unsafe public void NeedData(StreamType streamType)
        {
            if (streamType == StreamType.Audio)
                needDataAudio = true;
            else if (streamType == StreamType.Video)
                needDataVideo = true;

            needDataEvent.Set();
        }

        unsafe public void EnoughData(StreamType streamType)
        {
            if (streamType == StreamType.Audio)
                needDataAudio = false;
            else if (streamType == StreamType.Video)
                needDataVideo = false;

            needDataInitMode = false;
        }

        unsafe public void SubmitPacket(StreamPacket packet) // TODO(g.skowinski): Implement it properly.
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
                TrackType_Samsung trackType;
                if (packet.StreamType == StreamType.Video)
                    trackType = TrackType_Samsung.TRACK_TYPE_VIDEO;
                else if (packet.StreamType == StreamType.Audio)
                    trackType = TrackType_Samsung.TRACK_TYPE_AUDIO;
                else if (packet.StreamType == StreamType.Subtitle)
                    trackType = TrackType_Samsung.TRACK_TYPE_SUBTITLE;
                else
                    trackType = TrackType_Samsung.TRACK_TYPE_MAX; // TODO(g.skowinski): Handle StreamType.Teletext and TrackType_Samsung.TRACK_TYPE_MAX properly.
                Tizen.Log.Info("JuvoPlayer", "[HQ] send es data to SubmitPacket: " + packet.Pts.ToString() + " (" + (trackType == TrackType_Samsung.TRACK_TYPE_AUDIO ? "AUDIO" : trackType == TrackType_Samsung.TRACK_TYPE_VIDEO ? "VIDEO" : "OTHER") + ")");

                playerInstance.SubmitPacket(pnt, (uint)packet.Data.Length, packet.Pts, trackType, IntPtr.Zero);

            }
            finally
            {
                // Free the unmanaged memory. It need to check, no need to clear here, in Amazon es play case, the es data memory is cleared by decoder or sink element after it is used and played
                //  Marshal.FreeHGlobal(pnt);
            }

            // ------------------------------------------------
            //After there is no es data,means End of Stream,  you need to call SubmitEOS for both audio and video.
            //playerInstance.SubmitEOSPacket(TrackType_Samsung.TRACK_TYPE_AUDIO);
            //playerInstance.SubmitEOSPacket(TrackType_Samsung.TRACK_TYPE_VIDEO);

            //D2TV_MESSAGE_END_OF_STREAM message will come when d2tv-player done EOS, then you need to make your own module prepare for stop and shutdown.

        }

        public void Play() // TODO(g.skowinski): Handle asynchronicity (like in Stop() method?)
        {
            Log.Info("JuvoPlayer", "SMPlayerAdapter::Play()");
            playerInstance.Play();
        }

        public void Seek(double time) // TODO(g.skowinski): Make sure units are compatible.
        {
            playerInstance.Seek((int)(time * 1000000000));
        }

        public void SetAudioStreamConfig(AudioStreamConfig config)
        {
            Log.Info("JuvoPlayer", "SMPlayerAdapter1::SetAudioStreamConfig()");
            AudioStreamInfo_Samsung m_AudioStreamInfo = new AudioStreamInfo_Samsung();
            switch (config.Codec)
            {
                case AudioCodec.AC3:
                    m_AudioStreamInfo.mime = Marshal.StringToHGlobalAnsi("audio/x-eac3"); // "audio/mpeg" or "audio/x-eac3"
                    break;
                case AudioCodec.AAC:
                default:
                    m_AudioStreamInfo.mime = Marshal.StringToHGlobalAnsi("audio/mpeg"); // "audio/mpeg" or "audio/x-eac3"
                    m_AudioStreamInfo.version = 4; // Needed when mime is set to "audio/mpeg". When it's set to "audio/x-eac3", the field should NOT be set.
                    break;
            }
            m_AudioStreamInfo.sample_rate = (uint)config.SampleRate; // m_AudioStreamInfo.sample_rate = 44100;
            m_AudioStreamInfo.channels = (uint)config.ChannelLayout; // m_AudioStreamInfo.channels = 2;
            playerInstance.SetAudioStreamInfo(m_AudioStreamInfo);
            audioSet = true;
        }

        public void SetVideoStreamConfig(VideoStreamConfig config)
        {
            Log.Info("JuvoPlayer", "SMPlayerAdapter1::SetVideoStreamConfig()");

            VideoStreamInfo_Samsung videoStreamInfo = new VideoStreamInfo_Samsung();
            switch (config.Codec)
            {
                case VideoCodec.H264:
                default:
                    videoStreamInfo.mime = Marshal.StringToHGlobalAnsi("video/x-h264"); // "video/x-h264" or "video/x-h265"
                    break;
            }
            videoStreamInfo.drm_type = 0;  // 0 for no DRM
            videoStreamInfo.framerate_num = 2997; // videoStreamInfo.framerate_num = (uint)config.FrameRateNum;
            videoStreamInfo.framerate_den = 125; // videoStreamInfo.framerate_den = (uint)config.FrameRateDen;
            videoStreamInfo.width = (uint)config.Size.Width; // videoStreamInfo.width = 640;
            videoStreamInfo.max_width = (uint)config.Size.Width; // videoStreamInfo.height = 480;
            videoStreamInfo.height = (uint)config.Size.Height; // videoStreamInfo.max_width = 640;
            videoStreamInfo.max_height = (uint)config.Size.Height; // videoStreamInfo.max_height = 480;

            playerInstance.SetVideoStreamInfo(videoStreamInfo);

            videoSet = true;
        }

        public void SetDuration(double duration) // TODO(g.skowinski): Make sure units are compatible. SMPlayer/gstreamer needs nanoseconds, duration is given in seconds
        {
            playerInstance.SetDuration((uint)(duration * 1000000000));
        }

        public void SetExternalSubtitles(string file)
        {

        }

        public void SetPlaybackRate(float rate)
        {

        }

        public void SetSubtitleDelay(int offset)
        {

        }

        public void Stop() // TODO(g.skowinski): Handle asynchronicity.
        {
            playerInstance.Stop(); // This is async method - wait for D2TV_MESSAGE_STOP_SUCCESS message before doing anything else with the player.
        }

        public void OnTimeUpdated(double time)
        {
            TimeUpdated?.Invoke(time);
        }

        public void Pause() // TODO(g.skowinski): Handle asynchronicity (like in Stop() method?).
        {
            playerInstance.Pause();
        }
    }
}
