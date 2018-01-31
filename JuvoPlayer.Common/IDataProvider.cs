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

using System;
using System.Collections.Generic;

namespace JuvoPlayer.Common
{
    public delegate void ClipDurationChanged(TimeSpan clipDuration);
    public delegate void DRMInitDataFound(DRMInitData data);
    public delegate void SetDrmConfiguration(DRMDescription description);
    public delegate void StreamConfigReady(StreamConfig config);
    public delegate void StreamPacketReady(StreamPacket packet);
    public delegate void StreamsFound(List<StreamDefinition> streams);

    public interface IDataProvider
    {
        void OnChangeRepresentation(int representationId);
        void OnPaused();
        void OnPlayed();
        void OnSeek(TimeSpan time);
        void OnStopped();
        void OnTimeUpdated(TimeSpan time);

        void Start();

        event ClipDurationChanged ClipDurationChanged;
        event DRMInitDataFound DRMInitDataFound;
        event SetDrmConfiguration SetDrmConfiguration;
        event StreamConfigReady StreamConfigReady;
        event StreamPacketReady StreamPacketReady;
        event StreamsFound StreamsFound;
    }
}
