using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static Interop;

namespace Tizen.TV.Multimedia.ESPlayer
{
    public partial class ESPlayer : IDisposable
    {
        internal static readonly string LogTag = "Tizen.Multimedia.ESPlayer";

        public event EventHandler<ErrorArgs> ErrorOccurred
        {
            add
            {
                if (ErrorOccurred_ != null)
                {
                    Log.Error(LogTag, "ErrorOccurred is already added.");
                    return;
                }

                ErrorOccurred_ += value;
            }
            remove
            {
                ErrorOccurred_ -= value;
            }
        }

        public event EventHandler<ResourceConflictArgs> ResourceConflicted
        {
            add
            {
                if (ResourceConflicted_ != null)
                {
                    Log.Error(LogTag, "ResourceConflicted is already added.");
                    return;
                }

                ResourceConflicted_ += value;
            }
            remove
            {
                ResourceConflicted_ -= value;
            }
        }

        // EventHandler 를 확장해서, add를 1회만 할 수 있도록 변경할 수 있을지 ?
        public event EventHandler<EosArgs> EOSEmitted
        {
            add
            {
                if (EOSEmitted_ != null)
                {
                    Log.Error(LogTag, "EOSEmitted is already added.");
                    return; // throws new Exception()
                }

                EOSEmitted_ += value;
            }
            remove
            {
                EOSEmitted_ -= value;
            }
        }

        private IntPtr player = IntPtr.Zero;

        private LockerObject lockerForPrepare = new LockerObject();
        private LockerObject lockerForSeek = new LockerObject();

        private readonly TimeSpan timeoutForPrepare = TimeSpan.FromMilliseconds(20 * 1000);
        private readonly TimeSpan timeoutForSeek = TimeSpan.FromMilliseconds(20 * 1000);

        private EsDisplay display;

        internal IntPtr Player
        {
            get {
                return this.player;
            }
        }

        public EsDisplay Display
        {
            get { return Display; }
        }

        public ESPlayer()
        {
        }

        private void Initialize()
        {
            if(player == IntPtr.Zero)
            {
                player = NativeESPlusPlayer.Create();
                player.VerifyHandle("Player handle isn't valid.");

                CallbackInitialize();
            }
        }

        public void Open()
        {
            Log.Info(LogTag, "start");
            Initialize();
            NativeESPlusPlayer.Open(player).VerifyOperation("ESPlayer Open is failed.");
        }

        public void Close()
        {
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Close(player).VerifyOperation("ESPlayer Close is failed.");
        }

        public async Task PrepareAsync(Action<StreamType> onReadyToPrepare)
        {
            Log.Info(LogTag, "start");
            
            this.ReadyToPrepare_ += (s, e) => 
            {
                onReadyToPrepare(e.StreamType);
            };

            NativeESPlusPlayer.PrepareAsync(player).VerifyOperation("ESPlayer PrepareAsync is failed.");

            var task = Task.Factory.StartNew(() =>
            {
                bool withinTimeout = WaitFor(lockerForPrepare, timeoutForPrepare);
                withinTimeout.VerifyOperation("Timeout occurred in PrepareAsync.");

                Log.Info(LogTag, $"prepare_async in native esplayer is done. result : {lockerForPrepare.Result}");

                lockerForPrepare.Result.VerifyOperation("ESPlayer PrepareAsync is failed.");
                lockerForPrepare.Result = false;
            });

            await task;
        }

        public void Start()
        {
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Start(player).VerifyOperation("ESPlayer Start is failed.");
        }

        public void Stop()
        {
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Stop(player).VerifyOperation("ESPlayer Stop is failed.");
        }

        public void Pause()
        {
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Pause(player).VerifyOperation("ESPlayer Pause is failed.");
        }

        public void Resume()
        {
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Resume(player).VerifyOperation("ESPlayer Resume is failed.");
        }

        public async Task SeekAsync(TimeSpan timeInMilliseconds, Action<StreamType, TimeSpan> onReadyToSeek)
        {
            Log.Info(LogTag, "start");

            this.ReadyToSeek_ += (s, e) =>
            {
                onReadyToSeek(e.StreamType, e.Offset);
            };

            NativeESPlusPlayer.Seek(player, (ulong)timeInMilliseconds.TotalMilliseconds).VerifyOperation("ESPlayer Seek is failed.");

            var task = Task.Factory.StartNew(() =>
            {
                Log.Info(LogTag, $"before lock");

                var withinTimeout = WaitFor(lockerForSeek, timeoutForSeek);
                withinTimeout.VerifyOperation("Timeout occurred in SeekAsync");

                Log.Info(LogTag, $"seek in native esplayer is done.");
            });

            await task;
        }

        // elm
        public void SetDisplay(ElmSharp.Window window)
        {
            Log.Info(LogTag, "start");
            display = new EsDisplay(window);

            display.SetDisplay(player).VerifyOperation("ESPlayer SetDisplay is failed.");
        }

        // ecore
        public void SetDisplay(Tizen.NUI.Window window)
        {
            Log.Info(LogTag, "start");
            display = new EsDisplay(window);
            display.SetDisplay(player).VerifyOperation("ESPlayer SetDisplay is failed.");
        }

        public void SetDisplay(IntPtr window)
        {
            Log.Info(LogTag, "SetDisplay() start");            
            NativeESPlusPlayer.SetDisplay(player, DisplayType.Overlay, window, 0, 0, 1920, 1080).VerifyOperation("ESPlayer SetDisplay is failed.");
        }

        public void SetDisplayMode(DisplayMode mode)
        {
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.SetDisplayMode(player, mode).VerifyOperation("ESPlayer SetDisplayMode is failed.");
        }

        public void SetDisplayRoi(int x, int y, int width, int height)
        {
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.SetDisplayRoi(player, x, y, width, height).VerifyOperation("ESPlayer SetDisplayRoi is failed.");
        }

        public void SetDisplayVisible(bool visible)
        {
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.SetDisplayVisible(player, visible).VerifyOperation("ESPlayer SetDisplayVisible is failed.");
        }

        public void SetAudioMute(bool mute)
        {
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.SetAudioMute(player, mute).VerifyOperation("ESPlayer SetAudioMute is failed.");
        }

        public EsState GetState()
        {
            Log.Info(LogTag, "start");
            return NativeESPlusPlayer.GetState(player);
        }

        public void AddStream(AudioStreamInfo info)
        {
            IntPtr unmanagedBuffer = IntPtr.Zero;

            if (info.codecData != null)
            {
                unmanagedBuffer = Marshal.AllocHGlobal(info.codecData.Length * Marshal.SizeOf(info.codecData[0]));
                Marshal.Copy(info.codecData, 0, unmanagedBuffer, info.codecData.Length);
            }

            var nativeInfo = new NativeAudioStreamInfo
            {
                codecData = unmanagedBuffer,
                codecDataLength = info.codecData?.Length ?? 0,
                mimeType = info.mimeType,
                bitrate = info.bitrate,
                channels = info.channels,
                sampleRate = info.sampleRate
            };

            IntPtr param = Marshal.AllocHGlobal(Marshal.SizeOf(nativeInfo));

            try
            {
                Marshal.StructureToPtr(nativeInfo, param, false);
                NativeESPlusPlayer.AddAudioStreamInfo(player, param).VerifyOperation("ESPlayer AddStream is failed.");
            }
            finally
            {
                if (unmanagedBuffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(unmanagedBuffer);
                Marshal.FreeHGlobal(param);
            }
        }

        public void AddStream(VideoStreamInfo info)
        {
            IntPtr unmanagedBuffer = IntPtr.Zero;

            if (info.codecData != null)
            {
                unmanagedBuffer = Marshal.AllocHGlobal(info.codecData.Length * Marshal.SizeOf(info.codecData[0]));
                Marshal.Copy(info.codecData, 0, unmanagedBuffer, info.codecData.Length);
            }

            var nativeInfo = new NativeVideoStreamInfo
            {
                codecData = unmanagedBuffer,
                codecDataLength = info.codecData?.Length ?? 0,
                mimeType = info.mimeType,
                width = info.width,
                height = info.height,
                maxWidth = info.maxWidth,
                maxHeight = info.maxHeight,
                num = info.num,
                den = info.den
            };

            IntPtr param = Marshal.AllocHGlobal(Marshal.SizeOf(nativeInfo));

            try
            {
                Marshal.StructureToPtr(nativeInfo, param, false);
                NativeESPlusPlayer.AddVideoStreamInfo(player, param).VerifyOperation("ESPlayer AddStream is failed.");
            }
            finally
            {
                if (unmanagedBuffer != IntPtr.Zero)
                    Marshal.FreeHGlobal(unmanagedBuffer);
                Marshal.FreeHGlobal(param);
            }
        }

        public SubmitStatus SubmitPacket(ESPacket packet)
        {
            IntPtr unmanagedBuffer = Marshal.AllocHGlobal(packet.buffer.Length * Marshal.SizeOf(packet.buffer[0]));
            Marshal.Copy(packet.buffer, 0, unmanagedBuffer, packet.buffer.Length);

            var nativePacket = new ESNativePacket
            {
                type = packet.type,
                buffer = unmanagedBuffer,
                bufferSize = (uint)(packet.buffer?.Length ?? 0),
                pts = packet.pts,
                duration = packet.duration
            };

            IntPtr param = Marshal.AllocHGlobal(Marshal.SizeOf(nativePacket));

            try
            {
                Marshal.StructureToPtr(nativePacket, param, false);
                return NativeESPlusPlayer.SubmitPacket(player, param);
            }
            finally
            {
                Marshal.FreeHGlobal(unmanagedBuffer);
                Marshal.FreeHGlobal(param);
            }
        }

        public SubmitStatus SubmitPacket(ESHandlePacket packet)
        {
            IntPtr param = Marshal.AllocHGlobal(Marshal.SizeOf(packet));

            try
            {
                Marshal.StructureToPtr(packet, param, false);
                return NativeESPlusPlayer.SubmitTrustZonePacket(player, param);
            }
            finally
            {
                Marshal.FreeHGlobal(param);
            }
        }

        public SubmitStatus SubmitEosPacket(StreamType type)
        {
            Log.Info(LogTag, $"[{type}] submit eos packet");
            return NativeESPlusPlayer.SubmitEOSPacket(player, type);
        }
        public void GetPlayingTime(out TimeSpan timeInMilliseconds)
        {
            NativeESPlusPlayer.GetPlayingTime(player, out ulong ms).VerifyOperation("ESPlayer AddStream is failed.");
            timeInMilliseconds = TimeSpan.FromMilliseconds(ms);
        }

        private bool WaitFor(object locker, TimeSpan timeout)
        {
            lock (locker)
            {
                return Monitor.Wait(locker, timeout);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        ~ESPlayer()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {

                }

                try
                {
                    this.Stop();
                    this.Close();
                    Log.Info(LogTag, "this.Close() is called.");
                }
                catch(Exception ex)
                {
                    Log.Error(LogTag, $"ex : {ex.Message}");
                }
                finally
                {
                    this.player = IntPtr.Zero;
                    disposedValue = true;
                }
            }
        }

        public void Dispose()
        {
            Log.Info(LogTag, "Dispose() is called.");
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }

    internal class LockerObject
    {
        internal LockerObject()
        {
            Result = false;
        }

        internal bool Result { get; set; }
    }

    internal static class Utility
    {
        public static void VerifyOperation(this bool capiResult, string message)
        {
            if(!capiResult)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void VerifyHandle(this IntPtr ptr, string message)
        {
            if(ptr == IntPtr.Zero)
            {
                false.VerifyOperation(message);
            }
        }
    }
}