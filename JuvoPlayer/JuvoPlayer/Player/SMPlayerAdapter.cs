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
        private System.UInt32 currentTime;

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
            playerAdapter.OnEndOfStream();
        }

        public void OnSeekCompleted()
        {
            Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnSeekCompleted!");
        }

        public void OnSeekStartedBuffering()
        {
            Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnSeekStartedBuffering!");
        }

        public void OnCurrentPosition(System.UInt32 currTime)
        {
            if (currentTime == currTime)
                return;

            string msg = "CSDemoPlayerListener::OnCurrentPosition = " + currTime;
            Tizen.Log.Info("JuvoPlayer", msg);

            currentTime = currTime;
            playerAdapter.OnTimeUpdated(currentTime);
        }
    }

    unsafe public class SMPlayerAdapter : IPlayerAdapter
    {
        public event PlaybackCompleted PlaybackCompleted;
        public event ShowSubtitile ShowSubtitle;
        public event TimeUpdated TimeUpdated;

        SMPlayerWrapper playerInstance;
        static public HandleRef refMainStage;

        bool audioSet, videoSet, isPlayerInitialized, playCalled;

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

                    Log.Info("JuvoPlayer", "Peeked");

                    if (packet.IsEOS)
                        SubmitEOSPacket(packet);
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
        }

        unsafe private void SubmitEOSPacket(StreamPacket packet)
        {
            TrackType_Samsung trackType;
            if (packet.StreamType == StreamType.Video)
                trackType = TrackType_Samsung.TRACK_TYPE_VIDEO;
            else if (packet.StreamType == StreamType.Audio)
                trackType = TrackType_Samsung.TRACK_TYPE_AUDIO;
            else if (packet.StreamType == StreamType.Subtitle)
                trackType = TrackType_Samsung.TRACK_TYPE_SUBTITLE;
            else
                trackType = TrackType_Samsung.TRACK_TYPE_MAX; // TODO(g.skowinski): Handle StreamType.Teletext and TrackType_Samsung.TRACK_TYPE_MAX properly.

            Tizen.Log.Info("JuvoPlayer", "[HQ] send EOS packet: " + packet.Pts.ToString() + " (" + (trackType == TrackType_Samsung.TRACK_TYPE_AUDIO ? "AUDIO" : trackType == TrackType_Samsung.TRACK_TYPE_VIDEO ? "VIDEO" : "OTHER") + ")");

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

        public void Seek(double time) // TODO(g.skowinski): Make sure units are compatible.
        {
            playerInstance.Seek((int)(time * 1000000000));
        }

        public void SetAudioStreamConfig(AudioStreamConfig config)
        {
            Log.Info("JuvoPlayer", "SMPlayerAdapter1::SetAudioStreamConfig()");

            var audioStreamInfo = new AudioStreamInfo_Samsung
            {
                mime = Marshal.StringToHGlobalAnsi(GetCodecMimeType(config.Codec)),
                version = GetCodecVersion(config.Codec),
                sample_rate = (uint)config.SampleRate,
                channels = (uint)config.ChannelLayout
            };

            playerInstance.SetAudioStreamInfo(audioStreamInfo);

            audioSet = true;
        }

        public void SetVideoStreamConfig(VideoStreamConfig config)
        {
            Log.Info("JuvoPlayer", "SMPlayerAdapter1::SetVideoStreamConfig()");

            var videoStreamInfo = new VideoStreamInfo_Samsung
            {
                mime = Marshal.StringToHGlobalAnsi(GetCodecMimeType(config.Codec)),
                version = GetCodecVersion(config.Codec),
                drm_type = 0,  // 0 for no DRM
                framerate_num = (uint)config.FrameRateNum,
                framerate_den = (uint)config.FrameRateDen,
                width = (uint)config.Size.Width,
                max_width = (uint)config.Size.Width,
                height = (uint)config.Size.Height,
                max_height = (uint)config.Size.Height
            };

            playerInstance.SetVideoStreamInfo(videoStreamInfo);

            videoSet = true;
        }

        public void SetDuration(double duration) // TODO(g.skowinski): Make sure units are compatible. SMPlayer/gstreamer needs nanoseconds, duration is given in seconds
        {
            playerInstance.SetDuration((uint)(duration * 1000000000));
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

        public void OnTimeUpdated(double time)
        {
            TimeUpdated?.Invoke(time);
        }

        public void OnEndOfStream()
        {
            PlaybackCompleted?.Invoke();
        }

        public void Pause() // TODO(g.skowinski): Handle asynchronicity (like in Stop() method?).
        {
            playerInstance.Pause();
        }

        private string GetCodecMimeType(VideoCodec videoCodec)
        {
            switch (videoCodec)
            {
                case VideoCodec.H264:
                    return "video/x-h264";
                case VideoCodec.H265:
                    return "video/x-h265";
                case VideoCodec.MPEG2:
                case VideoCodec.MPEG4:
                    return "video/mpeg";
                case VideoCodec.VP8:
                    return "video/x-vp8";
                case VideoCodec.VP9:
                    return "video/x-vp9";
                case VideoCodec.WMV1:
                case VideoCodec.WMV2:
                case VideoCodec.WMV3:
                    return "video/x-wmv";
                default:
                    return "";
            }
        }

        private string GetCodecMimeType(AudioCodec audioCodec)
        {
            switch (audioCodec)
            {
                case AudioCodec.AAC:
                case AudioCodec.MP2:
                case AudioCodec.MP3:
                    return "audio/mpeg";
                case AudioCodec.PCM:
                    return "audio/x-raw-int";
                case AudioCodec.VORBIS:
                    return "audio/x-vorbis";
                case AudioCodec.FLAC:
                    return "audio/x-flac";
                case AudioCodec.AMR_NB:
                    return "audio/AMR";
                case AudioCodec.AMR_WB:
                    return "audio/AMR-WB";
                case AudioCodec.PCM_MULAW:
                    return "audio/x-mulaw";
                case AudioCodec.GSM_MS:
                    return "audio/ms-gsm";
                case AudioCodec.PCM_S16BE:
                    return "audio/x-raw";
                case AudioCodec.PCM_S24BE:
                    return "audio/x-raw";
                case AudioCodec.OPUS:
                    return "audio/ogg";
                case AudioCodec.EAC3:
                    return "audio/x-eac3";
                case AudioCodec.DTS:
                    return "audio/x-dts";
                case AudioCodec.AC3:
                    return "audio/x-ac3";
                case AudioCodec.WMAV1:
                case AudioCodec.WMAV2:
                    return "audio/x-ms-wma";
                default:
                    return "";
            }
        }

        uint GetCodecVersion(VideoCodec videoCodec)
        {
            switch (videoCodec)
            {
                case VideoCodec.MPEG2:
                case VideoCodec.WMV2:
                    return 2;
                case VideoCodec.MPEG4:
                    return 4;
                case VideoCodec.WMV1:
                    return 1;
                case VideoCodec.WMV3:
                    return 3;
                case VideoCodec.H264:
                    return 4;
                default:
                    return 0;
            }
        }

        uint GetCodecVersion(AudioCodec audioCodec)
        {
            switch (audioCodec)
            {
                case AudioCodec.AAC:
                    return 4;
                case AudioCodec.MP3:
                    return 1;
                case AudioCodec.MP2:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
