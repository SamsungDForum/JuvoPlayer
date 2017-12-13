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
using System.Collections.Generic;

namespace JuvoPlayer.Player
{
    public delegate void Pause();
    public delegate void Play();
    public delegate void Seek(double time);
    public delegate void Stop();

    public delegate void PlaybackCompleted();
    public delegate void ShowSubtitile(Subtitle subtitle);
    public delegate void TimeUpdated(double time);

    public interface IPlayerController
    {
        #region ui_slots
        void ChangeRepresentation(int pid);
        void OnPause();
        void OnPlay();
        void OnSeek(double time);
        void OnSetExternalSubtitles(string path);
        void OnSetPlaybackRate(float rate);
        void OnSetSubtitleDelay(int offset);
        void OnStop();
        #endregion

        #region data_provider_slots
        void OnDrmDataFound(DRMData data);
        void OnStreamConfigReady(StreamConfig config);
        void OnStreamPacketReady(StreamPacket packet);
        void OnStreamsFound(List<StreamDefinition> streams);
        #endregion

        // TODO(p.galiszewsk): rethink this
        void SetDataProvider(IDataProvider dataProvider);

        event Pause Pause;
        event Play Play;
        event Seek Seek;
        event Stop Stop;

        event PlaybackCompleted PlaybackCompleted;
        event ShowSubtitile ShowSubtitle;
        event TimeUpdated TimeUpdated;
    }
}