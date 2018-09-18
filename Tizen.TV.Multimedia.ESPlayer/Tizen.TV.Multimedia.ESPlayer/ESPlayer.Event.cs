using System;
using System.Runtime.InteropServices;
using System.Threading;
using static Interop;

namespace Tizen.TV.Multimedia.ESPlayer
{
    internal class ReadyToPrepareArgs : EventArgs
    {
        internal StreamType StreamType
        {
            get; private set;
        }

        internal ReadyToPrepareArgs(StreamType type)
        {
            this.StreamType = type;
        }
    }

    internal class ReadyToSeekArgs : EventArgs
    {
        internal StreamType StreamType
        {
            get; private set;
        }

        internal TimeSpan Offset
        {
            get; private set;
        }

        internal ReadyToSeekArgs(StreamType type, TimeSpan offset)
        {
            this.StreamType = type;
            this.Offset = offset;
        }
    }

    public partial class ESPlayer : IDisposable
    {
        // event handler / delegate 를 묶어서 새 class로 정의할 것.
        private EventHandler<ErrorArgs> ErrorOccurred_;
        private EventHandler<BufferStatusArgs> BufferStatusEmitted_;
        private EventHandler<ResourceConflictArgs> ResourceConflicted_;
        private EventHandler<EosArgs> EOSEmitted_;

        private EventHandler<ReadyToPrepareArgs> ReadyToPrepare_;
        private EventHandler<ReadyToSeekArgs> ReadyToSeek_;

        private NativeESPlusPlayer.OnError onError;
        private NativeESPlusPlayer.OnBufferStatus onBufferStatus;
        private NativeESPlusPlayer.OnResourceConflicted onResourceConflicted;
        private NativeESPlusPlayer.OnEos onEos;
        private NativeESPlusPlayer.OnReadyToPrepare onReadyToPrepare;
        private NativeESPlusPlayer.OnPrepareDone onPrepareDone;
        private NativeESPlusPlayer.OnSeekDone onSeekDone;
        private NativeESPlusPlayer.OnReadyToSeek onReadyToSeek;

        private void CallbackInitialize()
        {
            onError = (code) =>
            {
                Log.Info(LogTag, "onError");
                var handler = this.ErrorOccurred_;
                handler?.Invoke(this, new ErrorArgs(code));
            };

            onBufferStatus = (type, status) =>
            {
                Log.Info(LogTag, "onBufferStatus");
                var handler = this.BufferStatusEmitted_;
                handler?.Invoke(this, new BufferStatusArgs(type, status));
            };

            onResourceConflicted = () =>
            {
                Log.Info(LogTag, "onResourceConflicted");
                var handler = this.ResourceConflicted_;
                handler?.Invoke(this, new ResourceConflictArgs());
            };

            onEos = () =>
            {
                Log.Info(LogTag, "onEos");
                var handler = this.EOSEmitted_;
                handler?.Invoke(this, new EosArgs());
            };
        
            onPrepareDone = (result) => {
                Log.Info(LogTag, "onPrepareDone");
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
                Log.Info(LogTag, "onReadyToSeek");
                var handler = this.ReadyToSeek_;
                handler?.Invoke(this, new ReadyToSeekArgs(type, TimeSpan.FromMilliseconds(offset)));
                //this.ReadyToSeek_ = null;
            };

            onReadyToPrepare = (type) =>
            {
                Log.Info(LogTag, "onReadyToPrepare");
                var handler = this.ReadyToPrepare_;
                handler?.Invoke(this, new ReadyToPrepareArgs(type));
                //this.ReadyToPrepare_ = null;
            };

            NativeESPlusPlayer.RegisterOnErrorListener(player, Marshal.GetFunctionPointerForDelegate(onError));

            NativeESPlusPlayer.RegisteronBufferStatusListener(player, Marshal.GetFunctionPointerForDelegate(onBufferStatus));

            NativeESPlusPlayer.RegisterOnResourceConflicted(player, Marshal.GetFunctionPointerForDelegate(onResourceConflicted));

            NativeESPlusPlayer.RegisterOnEosListener(player, Marshal.GetFunctionPointerForDelegate(onEos));

            NativeESPlusPlayer.RegisterOnPrepareAsyncDoneListener(player, Marshal.GetFunctionPointerForDelegate(onPrepareDone));

            NativeESPlusPlayer.RegisterOnSeekDoneListener(player, Marshal.GetFunctionPointerForDelegate(onSeekDone));

            NativeESPlusPlayer.RegisterOnReadyToPrepareListener(player, Marshal.GetFunctionPointerForDelegate(onReadyToPrepare));

            NativeESPlusPlayer.RegisterOnReadyToSeekListener(player, Marshal.GetFunctionPointerForDelegate(onReadyToSeek));
        }
    }
}
