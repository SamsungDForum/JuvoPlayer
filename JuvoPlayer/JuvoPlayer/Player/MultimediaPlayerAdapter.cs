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
using Tizen.Multimedia;

namespace JuvoPlayer.Player
{
    public class MultimediaPlayerAdapter : IPlayerAdapter
    {
        private ElmSharp.Window playerContainer;
        private Tizen.Multimedia.Player player;

        public MultimediaPlayerAdapter()
        {
            player = new Tizen.Multimedia.Player();

            playerContainer = new ElmSharp.Window("player");

            player.Display = new Display(playerContainer);
            player.DisplaySettings.Mode = PlayerDisplayMode.FullScreen;
            player.DisplaySettings.SetRoi(new Tizen.Multimedia.Rectangle(300, 300, 800, 600));
            playerContainer.Show();
            playerContainer.BringDown();
        }

        public void OnShowSubtitle(Subtitle subtitle)
        {

        }

        public void Play()
        {

        }

        public void Seek(double time)
        {

        }

        public void SetDuration(double duration)
        {

        }

        public void SetExternalSubtitles(string file)
        {

        }

        public void SetPlaybackRate()
        {

        }

        public void SetStreamInfo()
        {

        }

        public void Stop()
        {

        }

        public void TimeUpdated(double time)
        {

        }
    }

}