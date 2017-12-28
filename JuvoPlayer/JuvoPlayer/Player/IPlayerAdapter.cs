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
using JuvoPlayer.Common.Delegates;

namespace JuvoPlayer.Player
{
    public interface IPlayerAdapter
    {
        event PlaybackCompleted PlaybackCompleted;
        event PlaybackError PlaybackError;
        event PlayerInitialized PlayerInitialized;
        event ShowSubtitile ShowSubtitle;
        event TimeUpdated TimeUpdated;

        void Pause();
        void Play();
        void Seek(double time);
        void SetDuration(double duration);
        void SetExternalSubtitles(string file);
        void SetPlaybackRate(float rate);
        void SetSubtitleDelay(int offset);
        void Stop();

        void SetAudioStreamConfig(AudioStreamConfig config);
        void SetVideoStreamConfig(VideoStreamConfig config);
        void AppendPacket(StreamPacket packet);
    }
}