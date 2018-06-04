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
using System;
using System.Collections.Generic;
using JuvoPlayer.Subtitles;

namespace JuvoPlayer.DataProviders
{
    public delegate void SetDrmConfiguration(DRMDescription description);
    public delegate void StreamsFound(List<StreamDescription> streams);
    public delegate void StreamError(string errorMessage);

    public interface IDataProvider : IDisposable
    {
        void OnChangeActiveStream(StreamDescription stream);
        void OnDeactivateStream(StreamType streamType);
        void OnPaused();
        void OnPlayed();
        void OnSeek(TimeSpan time);
        void OnStopped();
        void OnTimeUpdated(TimeSpan time);

        bool IsSeekingSupported();
        void Start();
        Cue CurrentCue { get; }
        List<StreamDescription> GetStreamsDescription(StreamType streamType);

        event ClipDurationChanged ClipDurationChanged;
        event DRMInitDataFound DRMInitDataFound;
        event SetDrmConfiguration SetDrmConfiguration;
        event StreamConfigReady StreamConfigReady;
        event PacketReady PacketReady;
        event StreamsFound StreamsFound;
        event StreamError StreamError;
    }
}
