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

using JuvoPlayer.Common;

namespace JuvoPlayer.Player
{
    public class SMPlayerAdapter : IPlayerAdapter
    {
        public event ShowSubtitile ShowSubtitle;
        public event PlaybackCompleted PlaybackCompleted;

        public SMPlayerAdapter()
        {

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