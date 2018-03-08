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

namespace JuvoPlayer.Demuxers
{
    public enum InitializationMode
    {
        // Stream has been already initialized so preparinf StreamConfig is not needed 
        Minimal,
        // Stream needs full initialization
        Full
    };

    public interface IDemuxer : IDisposable
    {
        void StartForExternalSource(InitializationMode initMode);
        void StartForUrl(string url);
        void ChangePID(int pid);
        void Reset();
        void Paused();
        void Played();

        event ClipDurationChanged ClipDuration;
        event DRMInitDataFound DRMInitDataFound;
        event StreamConfigReady StreamConfigReady;
        event PacketReady PacketReady;
    }
}