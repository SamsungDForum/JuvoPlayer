/// @file ESPlayer.Event.cs 
/// <published> N </published>
/// <privlevel> Non-privilege </privlevel>
/// <privilege> None </privilege>
/// <privacy> N </privacy>
/// <product> TV </product>
/// <version> 5.0.0 </version>
/// <SDK_Support> N </SDK_Support>
/// Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved  
/// PROPRIETARY/CONFIDENTIAL  
/// This software is the confidential and proprietary  
/// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall  
/// not disclose such Confidential Information and shall use it only in  
/// accordance with the terms of the license agreement you entered into with  
/// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the  
/// suitability of the software, either express or implied, including but not  
/// limited to the implied warranties of merchantability, fitness for a  
/// particular purpose, or non-infringement. SAMSUNG shall not be liable for any  
/// damages suffered by licensee as a result of using, modifying or distributing  
/// this software or its derivatives.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using static Interop;

namespace Tizen.TV.Multimedia
{
    public partial class ESPlayer : IDisposable
    {
        // event handler / delegate 를 묶어서 새 class로 정의할 것.
        private EventHandler<ErrorEventArgs> ErrorOccurred_;
        private EventHandler<BufferStatusEventArgs> BufferStatusEmitted_;
        private EventHandler<ResourceConflictEventArgs> ResourceConflicted_;
        private EventHandler<EOSEventArgs> EOSEmitted_;

        private EventHandler<ReadyToPrepareEventArgs> ReadyToPrepare_;
        private EventHandler<ReadyToSeekEventArgs> ReadyToSeek_;

        private NativeESPlusPlayer.OnError onError;
        private NativeESPlusPlayer.OnBufferStatus onBufferStatus;
        private NativeESPlusPlayer.OnResourceConflicted onResourceConflicted;
        private NativeESPlusPlayer.OnEos onEos;
        private NativeESPlusPlayer.OnReadyToPrepare onReadyToPrepare;
        private NativeESPlusPlayer.OnPrepareAsyncDone onPrepareAsyncDone;
        private NativeESPlusPlayer.OnSeekDone onSeekDone;
        private NativeESPlusPlayer.OnReadyToSeek onReadyToSeek;

        private void CallbackInitialize()
        {
            onError = (code) =>
            {
                Log.Info(LogTag, "onError");
                var handler = this.ErrorOccurred_;
                handler?.Invoke(this, new ErrorEventArgs(code));
            };

            onBufferStatus = (type, status) =>
            {
                Log.Info(LogTag, "onBufferStatus");
                var handler = this.BufferStatusEmitted_;
                handler?.Invoke(this, new BufferStatusEventArgs(type, status));
            };

            onResourceConflicted = () =>
            {
                Log.Info(LogTag, "onResourceConflicted");
                var handler = this.ResourceConflicted_;
                handler?.Invoke(this, new ResourceConflictEventArgs());
            };

            onEos = () =>
            {
                Log.Info(LogTag, "onEos");
                var handler = this.EOSEmitted_;
                handler?.Invoke(this, new EOSEventArgs());
            };
        
            onPrepareAsyncDone = (result) => {
                Log.Info(LogTag, "onPrepareAsyncDone");
                Log.Info(LogTag, $"native prepare async done. result : {result}");

                lock (lockerForPrepare)
                {
                    lockerForPrepare.Result = result;
                    Monitor.Pulse(lockerForPrepare);
                }
            };

            onSeekDone = () =>
            {
                Log.Info(LogTag, "onSeekDone");
                Log.Info(LogTag, $"native seek async done.");

                lock (lockerForSeek)
                {
                    Monitor.Pulse(lockerForSeek);
                }
            };

            onReadyToSeek = (type, offset) =>
            {
                Log.Info(LogTag, $"onReadyToSeek[{type}] - offset : [{offset} ms]");
                var handler = this.ReadyToSeek_;
                handler?.Invoke(this, new ReadyToSeekEventArgs(type, offset));
            };

            onReadyToPrepare = (type) =>
            {
                Log.Info(LogTag, "onReadyToPrepare");
                var handler = this.ReadyToPrepare_;
                handler?.Invoke(this, new ReadyToPrepareEventArgs(type));
            };

            NativeESPlusPlayer.SetOnErrorCallback(player, Marshal.GetFunctionPointerForDelegate(onError));
            NativeESPlusPlayer.SetOnBufferStatusCallback(player, Marshal.GetFunctionPointerForDelegate(onBufferStatus));
            NativeESPlusPlayer.SetOnResourceConflictedCallback(player, Marshal.GetFunctionPointerForDelegate(onResourceConflicted));
            NativeESPlusPlayer.SetOnEosCallback(player, Marshal.GetFunctionPointerForDelegate(onEos));
            NativeESPlusPlayer.SetOnReadyToPrepareCallback(player, Marshal.GetFunctionPointerForDelegate(onReadyToPrepare));
            NativeESPlusPlayer.SetOnPrepareAsyncDoneCallback(player, Marshal.GetFunctionPointerForDelegate(onPrepareAsyncDone));
            NativeESPlusPlayer.SetOnSeekDoneCallback(player, Marshal.GetFunctionPointerForDelegate(onSeekDone));
            NativeESPlusPlayer.SetOnReadyToSeekCallback(player, Marshal.GetFunctionPointerForDelegate(onReadyToSeek));
        }
    }
}
