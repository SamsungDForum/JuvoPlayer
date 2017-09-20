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

namespace JuvoPlayer.RTSP
{
    public class RTPDataProvider : IDataProvider
    {

        public RTPDataProvider()
        {

        }

        public RTPDataProvider(string url)
        {

        }

        event DRMDataFound IDataProvider.DRMDataFound
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

        event StreamConfigReady IDataProvider.StreamConfigReady
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

        event StreamPacketReady IDataProvider.StreamPacketReady
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

        event StreamsFound IDataProvider.StreamsFound
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

        public void DRMDataFound(DRMData data)
        {

        }

        public void OnChangeRepresentation(int representationId)
        {

        }

        /**
         * 
         * @param url
         */
        public void OnPlay(string url)
        {

        }

        /**
         * 
         * @param time
         */
        public void OnSeek(double time)
        {

        }
    }
}