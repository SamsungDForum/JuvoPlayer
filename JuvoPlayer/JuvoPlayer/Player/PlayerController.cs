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
    public class PlayerController : IPlayerController
    {

        public PlayerController()
        {

        }

        public PlayerController(IPlayerAdapter player)
        {

        }

        event Pause IPlayerController.Pause
        {
            add
            {
                throw new System.NotImplementedException();
            }

            remove
            {
                throw new System.NotImplementedException();
            }
        }

        event Play IPlayerController.Play
        {
            add
            {
                throw new System.NotImplementedException();
            }

            remove
            {
                throw new System.NotImplementedException();
            }
        }

        event Seek IPlayerController.Seek
        {
            add
            {
                throw new System.NotImplementedException();
            }

            remove
            {
                throw new System.NotImplementedException();
            }
        }


        public void OnDrmDataFound(DRMData data)
        {

        }

        public void OnStreamConfigReady(StreamConfig config)
        {

        }

        public void OnStreamPacketReady(StreamPacket packet)
        {

        }

        public void OnStreamsFound(List<StreamDefinition> streams)
        {

        }

        public void Pause()
        {

        }

        public void Play()
        {

        }

        public void Seek(double time)
        {

        }
    }

}