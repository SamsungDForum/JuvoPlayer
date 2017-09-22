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

using System.Collections.Generic;

namespace JuvoPlayer.Common
{
    public delegate void DRMDataFound(DRMData data);
    public delegate void StreamConfigReady(StreamConfig config);
    public delegate void StreamPacketReady(StreamPacket packet);
    public delegate void StreamsFound(List<StreamDefinition> streams);

    public interface IDataProvider
    {
        void OnChangeRepresentation(int representationId);
        void OnPlay();
        void OnSeek(double time);
        void Start();

        event DRMDataFound DRMDataFound;
        event StreamConfigReady StreamConfigReady;
        event StreamPacketReady StreamPacketReady;
        event StreamsFound StreamsFound;
    }
}