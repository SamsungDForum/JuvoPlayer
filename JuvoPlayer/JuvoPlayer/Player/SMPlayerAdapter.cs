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
using System;
using System.Runtime.InteropServices;

namespace JuvoPlayer.Player
{
    class CSDemoPlayerListener : IPlayerEventListener
    {
        //This param and setPlayer() is just used for internal test print dlog, not official code
        private CSPlayer.IPlayerAdapter player;
        public void setPlayer(CSPlayer.IPlayerAdapter playerInstance)
        {
            player = playerInstance;
        }


        public void OnEnoughData(StreamType_Samsung streamType)
        {
            if (streamType == StreamType_Samsung.STREAM_TYPE_SAMSUNG_AUDIO)
            {
                Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnEnoughData : STREAM_TYPE_SAMSUNG_AUDIO!");
            }
            else
            {
                Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnEnoughData : STREAM_TYPE_SAMSUNG_VIDEO!");
            }
        }

        public void OnNeedData(StreamType_Samsung streamType, uint size)
        {
            if (streamType == StreamType_Samsung.STREAM_TYPE_SAMSUNG_AUDIO)
            {
                Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnNeedData : STREAM_TYPE_SAMSUNG_AUDIO!");
            }
            else
            {
                Tizen.Log.Info("JuvoPlayer", "CSDemoPlayerListener::OnNeedData : STREAM_TYPE_SAMSUNG_VIDEO!");
            }
        }

        public void OnSeekData(StreamType_Samsung streamType, System.UInt64 offset)  //usigned long long I use System.Uint64
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
        }
    }

    public class SMPlayerAdapter1 : IPlayerAdapter
    {
        public event ShowSubtitile ShowSubtitle;
        public event PlaybackCompleted PlaybackCompleted;

        CSPlayer.SMplayerAdapter playerAdatpter;
        static public HandleRef refMainStage;
        public SMPlayerAdapter1()
        {
            try
            {
                playerAdatpter = new CSPlayer.SMplayerAdapter();
                var playerEventListener = new CSDemoPlayerListener();
                playerAdatpter.RegisterPlayerEventListener(playerEventListener);
                var playerContainer = new ElmSharp.Window("player");

                //                playerAdatpter.SetDisplay(PlayerDisplayType_Samsung.PLAYER_DISPLAY_TYPE_OVERLAY, playerContainer);
                //                playerAdatpter.SetDisplay(playerContainer, 0, 0, 1920, 1080);
                Tizen.Log.Info("JuvoPlayer", "2222222222222222222222222222222222222222");

                playerAdatpter.Initialize();
                playerAdatpter.PrepareES();
                Tizen.Log.Info("JuvoPlayer", "33333333333333333333333333333333333");

            }
            catch (Exception e)
            {
                Tizen.Log.Info("JuvoPlayer", "got exception: " + e.Message);
            }
        }

        public void AppendPacket(StreamPacket packet)
        {

        }


        public void Play()
        {

        }

        public void Seek(double time)
        {

        }

        public void SetAudioStreamConfig(AudioStreamConfig config)
        {

        }

        public void SetDuration(double duration)
        {

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

        public void SetVideoStreamConfig(VideoStreamConfig config)
        {

        }

        public void Stop()
        {

        }

        public void TimeUpdated(double time)
        {

        }
        public void Pause()
        {

        }
    }
}