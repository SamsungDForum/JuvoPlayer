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
using System.Reactive;
using JuvoPlayer.Subtitles;

namespace JuvoPlayer.DataProviders
{
    public interface IDataProvider : IDisposable
    {
        void OnChangeActiveStream(StreamDescription stream);
        void OnDeactivateStream(StreamType streamType);
        void OnPaused();
        void OnPlayed();
        void OnSeekStarted(TimeSpan time, uint seekId);
        void OnSeekCompleted();
        void OnStopped();
        void OnTimeUpdated(TimeSpan time);

        bool IsSeekingSupported();
        void Start();
        Cue CurrentCue { get; }
        List<StreamDescription> GetStreamsDescription(StreamType streamType);

        IObservable<TimeSpan> ClipDurationChanged();
        IObservable<DRMInitData> DRMInitDataFound();
        IObservable<DRMDescription> SetDrmConfiguration();
        IObservable<StreamConfig> StreamConfigReady();
        IObservable<Packet> PacketReady();
        IObservable<string> StreamError();
        IObservable<Unit> BufferingStarted();
        IObservable<Unit> BufferingCompleted();

    }
}
