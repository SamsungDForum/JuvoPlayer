/// @file ESPlayer.cs 
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
using System.Threading.Tasks;
using static Interop;

namespace Tizen.TV.Multimedia
{
    /// <summary>
    /// Provides API set for es contents playback.
    /// </summary>
    /// <remarks>
    /// ESPlayer API DO NOT guarantee thread-safe except pushing es packet to player. (e.g. <seealso cref="SubmitPacket(ESPacket)"/>, <seealso cref="SubmitPacket(ESHandlePacket)"/>, <seealso cref="SubmitEosPacket(StreamType)"/>.) Application should ensure its own thread safety without the above mentioned methods.
    /// </remarks>
    /// <code>
    /// using Tizen.TV.Multimedia;
    /// using System.Threading.Tasks;
    /// 
    /// class SampleApp
    /// {
    ///     ESPlayer player;
    ///     ElmSharp.Window window = // your logic;    
    ///     AudioStreamInfo audioStreamInfo = // your logic;
    ///     VideoStreamInfo videoStreamInfo = // your logic;
    /// 
    ///     public Sample()
    ///     {
    ///         player = new ESPlayer();
    ///         window = GetWindow();
    ///     }
    ///     
    ///     private void SetPlayer()
    ///     {
    ///         player.Open();
    ///         player.SetDisplay(window);
    ///         player.SetStream(audioStreamInfo);
    ///         player.SetStream(videoStreamInfo);
    ///         
    ///         player.EOSEmitted = (s, e) => { /* your logic */ };
    ///         player.ErrorOccurred = (s, e) => { /* your logic */ };
    ///         player.BufferStatusChanged = (s, e) => { /* your logic */ };
    ///         player.ResourceConflicted = (s, e) => { /* your logic */ };
    ///     }
    ///
    ///     public async Task PrepareAndPlay()
    ///     {
    ///         player.SetSource(new MediaUriSource("$FilePath"));
    ///         await player.PrepareAsync(async (stream) =>
    ///         {
    ///             await SubmitPacketThread(stream);
    ///         });
    ///         
    ///         player.Start();
    ///     }
    ///
    ///     public void StopAndDestroy()
    ///     {
    ///         player.Stop();
    ///         player.Dispose();
    ///     }
    ///     
    ///     private async void SubmitPacketThread(StreamType type)
    ///     {
    ///         await Task.Factory.StartNew(() =>
    ///         {
    ///             while(is_not_eos)
    ///             {
    ///                 var packet = GetPacket(type);
    ///                 player.SubmitPacket(packet);
    ///             }
    ///             
    ///             /* your logic */
    ///         };
    ///     }
    ///     
    ///     private ESPacket GetPacket(StreamType type)
    ///     {
    ///         return new ESPacket(type, /* your es packet data */);
    ///     }
    /// }
    /// </code>
    public partial class ESPlayer : IDisposable
    {
        internal static readonly string LogTag = "Tizen.Multimedia.ESPlayer";

        /// <summary>
        /// Initialize a new instance of the ESPlayer class.
        /// </summary>
        /// <remarks>The state of ESPlayer will be changed from to <see cref="ESPlayerState.None"/>.</remarks>
        /// <exception cref="InvalidOperationException">When ESPlayer fails to create ESPlayer handle.</exception>
        public ESPlayer()
        {
            Initialize();
        }

        /// <summary>
        /// Emit when esplayer encounters some error during preparing for esplayer pipeline or playback es packet.
        /// </summary>
        /// <remarks>
        /// Only one delegate for event handler can be registered for this event.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     player.ErrorOccurred = (s, e) =>
        ///     {
        ///         switch(e.ErrorType)
        ///         {
        ///             case ErrorType.None:
        ///             // your logic
        ///         }
        ///     };
        ///     // your logic
        /// }
        /// </code>
        /// <exception cref="InvalidOperationException">When application attemts to add the delegate for event handler as duplicate.</exception>
        public event EventHandler<ErrorEventArgs> ErrorOccurred
        {
            add
            {
                if (ErrorOccurred_ != null)
                {
                    throw new InvalidOperationException("ErrorOccurred is already added.");
                }

                ErrorOccurred_ += value;
            }
            remove
            {
                ErrorOccurred_ -= value;
            }
        }

        /// <summary>
        /// Emit when es packet queue is empty or full.
        /// </summary>
        /// ESPlayer will inform its buffer state to application as <seealso cref="BufferStatus.Underrun"/> and <seealso cref="BufferStatus.Overrun"/> status.
        /// <remarks>
        /// Only one delegate for event handler can be registered for this event.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     player.BufferStatusChanged = (s, e) =>
        ///     {
        ///         var streamType = e.StreamType;
        ///         var bufferStatus = e.BufferStatus;
        ///         // your logic
        ///     };
        ///     // your logic
        /// }
        /// </code>
        /// <seealso cref="BufferStatusEventArgs"/>
        /// <exception cref="InvalidOperationException">When application attemts to add the delegate for event handler as duplicate.</exception>
        public event EventHandler<BufferStatusEventArgs> BufferStatusChanged
        {
            add
            {
                if(BufferStatusEmitted_ != null)
                {
                    throw new InvalidOperationException("BufferStatusEmitted is already added.");
                }

                BufferStatusEmitted_ += value;
            }
            remove
            {
                BufferStatusEmitted_ -= value;
            }
        }

        /// <summary>
        /// Emit when resource conflicted event is emitted.
        /// </summary>
        /// <remarks>
        /// Only one delegate for event handler can be registered for this event.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     player.ResourceConflicted = (s, e) =>
        ///     {
        ///         // your logic
        ///     };
        /// }
        /// </code>
        /// <exception cref="InvalidOperationException">When application attemts to add the delegate for event handler as duplicate.</exception>
        public event EventHandler<ResourceConflictEventArgs> ResourceConflicted
        {
            add
            {
                if (ResourceConflicted_ != null)
                {
                    throw new InvalidOperationException("ResourceConflicted is already added.");
                }

                ResourceConflicted_ += value;
            }
            remove
            {
                ResourceConflicted_ -= value;
            }
        }

        /// <summary>
        /// Emit when es playback reaches end-of-stream.
        /// </summary>
        /// <remarks>
        /// Only one delegate for event handler can be registered for this event.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     player.EOSEmitted = (s, e) =>
        ///     {
        ///         // your logic
        ///     };
        /// }
        /// </code>
        /// <exception cref="InvalidOperationException">When application attemts to add the delegate for event handler as duplicate.</exception>
        public event EventHandler<EOSEventArgs> EOSEmitted
        {
            add
            {
                if (EOSEmitted_ != null)
                {
                    throw new InvalidOperationException("EOSEmitted is already added.");
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

        private ESDisplay display;

        private void Initialize()
        {
            player = NativeESPlusPlayer.Create();
            player.VerifyHandle("Player handle isn't valid.");

            CallbackInitialize();
        }

        /// <summary>
        /// Opens the ESPlayer handle.
        /// </summary>
        /// The state of ESPlayer will be changed from <see cref="ESPlayerState.None"/> to <see cref="ESPlayerState.Idle"/>.
        /// ESPlayer must be in the <see cref="ESPlayerState.None"/> state.
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.open();
        ///     // your logic
        /// }
        /// </code>
        /// <exception cref="InvalidOperationException">ESPlayer fails to open player handle.</exception>
        public void Open()
        {
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Open(player).VerifyOperation("ESPlayer Open is failed.");
        }

        /// <summary>
        /// Closes the ESPlayer handle.
        /// </summary>
        /// The all of resource for player will be reset and released in ESPlayer.
        /// Playback is no longer possible. If application want to use the ESPlayer again, application should reset a display, stream info, and call <see cref="PrepareAsync(Action{StreamType})"/> again.
        /// ESPlayer must be in the <see cref="ESPlayerState.None"/> state.
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.Stop();
        ///     player.Close();
        ///     // your logic
        /// }
        /// </code>
        /// <exception cref="InvalidOperationException">ESPlayer fails to close player handle.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public void Close()
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Close(player).VerifyOperation("ESPlayer Close is failed.");
        }

        /// <summary>
        /// Prepares ESPlayer instance for playback asynchronously.
        /// </summary>
        /// <param name="onReadyToPrepare">Called when the ESPlayer is ready to accept the first es packet of the point to be played for each stream during preparing player.</param>
        /// <returns>A task that represents the asynchronous prepare operation.</returns>
        /// <remarks>To prepare ESPlayer, ESPlayer must be in the <see cref="ESPlayerState.Idle"/> state, and application should set onReadyToPrepare callback for  submitting es packet.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     player.prepareAsync(OnReadyToPrepare);
        ///     // your logic
        /// }
        /// 
        /// private void OnReadyToPrepare(StreamType streamType)
        /// {
        ///     // your logic
        /// }
        /// </code>
        /// <seealso cref="SubmitPacket(ESPacket)"/>
        /// <seealso cref="SubmitPacket(ESHandlePacket)"/>
        /// <exception cref="InvalidOperationException">Fails preparing ESPlayer.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        /// <exception cref="NullReferenceException">onReadyToPrepare is null.</exception>
        public async Task PrepareAsync(Action<StreamType> onReadyToPrepare)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);

            if (onReadyToPrepare == null)
            {
                throw new NullReferenceException("You should set onReadyToPrepare for submitting es packet.");
            }

            Log.Info(LogTag, "start");

            ReadyToPrepare_ = (s, e) => 
            {
                onReadyToPrepare(e.StreamType);
            };

            NativeESPlusPlayer.PrepareAsync(player).VerifyOperation("ESPlayer PrepareAsync is failed.");

            var task = Task.Factory.StartNew(() =>
            {
                bool withinTimeout = Utility.WaitFor(lockerForPrepare, timeoutForPrepare);
                withinTimeout.VerifyOperation("Timeout occurred in PrepareAsync.");

                Log.Info(LogTag, $"prepare_async in native esplayer is done. result : {lockerForPrepare.Result}");

                lockerForPrepare.Result.VerifyOperation("ESPlayer PrepareAsync is failed.");
                lockerForPrepare.Result = false;
            });

            await task;
        }

        /// <summary>
        /// Starts the es contents playback.
        /// </summary>
        /// <remarks>
        /// ESPlayer must be in the <see cref="ESPlayerState.Ready"/> state.
        /// It has no effect if ESPlayer is already in the <see cref="PlayerState.Playing"/> state.
        /// DO NOT call <see cref="Resume"/> when you want to start the es contents playback.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.Start();
        ///     // your logic
        /// }
        /// </code>
        /// <exception cref="InvalidOperationException">ESPlayer is not in the valid state.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        /// <seealso cref="PrepareAsync"/>
        /// <seealso cref="Resume"/>
        /// <seealso cref="Stop"/>
        /// <seealso cref="Pause"/>
        /// <seealso cref="EOSEmitted"/>
        public void Start()
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Start(player).VerifyOperation($"ESPlayer Start is failed. Current ESPlayerState : {GetState()}");
        }

        /// <summary>
        /// Stops playing the es content.
        /// </summary>
        /// <remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.Stop();
        ///     // your logic
        /// }
        /// </code>
        /// ESPlayer must be in the <see cref="ESPlayerState.Playing"/> or <see cref="ESPlayerState.Paused"/> state.
        /// It has no effect if ESPlayer is already in the <see cref="ESPlayerState.Ready"/> state.
        /// </remarks>
        /// <exception cref="InvalidOperationException">ESPlayer is not in the valid state.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        /// <seealso cref="Start"/>
        /// <seealso cref="Pause"/>
        public void Stop()
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Stop(player).VerifyOperation("ESPlayer Stop is failed.");
        }

        /// <summary>
        /// Pauses playing the es contents.
        /// </summary>
        /// <remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.Pause();
        ///     // your logic
        /// }
        /// ESPlayer must be in the <see cref="ESPlayerState.Playing"/> state.
        /// It has no effect if ESPlayer is already in the <see cref="ESPlayerState.Paused"/> state.
        /// </remarks>
        /// <exception cref="InvalidOperationException">ESPlayer is not in the valid state.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        /// <seealso cref="Start"/>
        public void Pause()
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Pause(player).VerifyOperation("ESPlayer Pause is failed.");
        }

        /// <summary>
        /// Resumes the es contents playback.
        /// </summary>
        /// <remarks>
        /// ESPlayer must be in the <see cref="ESPlayerState.Paused"/> state.
        /// DO NOT call <see cref="Start"/> when you want to resume the es contents playback.
        /// It has no effect if ESPlayer is already in the <see cref="ESPlayerState.Playing"/> state.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.Resume();
        ///     // your logic
        /// }
        /// </code>
        /// <exception cref="InvalidOperationException">ESPlayer is not in the valid state.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        /// <seealso cref="Start"/>
        /// <seealso cref="Stop"/>
        /// <seealso cref="Pause"/>
        /// <seealso cref="EOSEmitted"/>
        public void Resume()
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.Resume(player).VerifyOperation("ESPlayer Resume is failed.");
        }

        /// <summary>
        /// Sets the seek position for playback asynchronously.
        /// </summary>
        /// <param name="timeInMilliseconds">The absolute value indicating a desired position in milliseconds.</param>
        /// <param name="onReadyToSeek">Called when the ESPlayer is ready to accept the first es packet of the point to be moved for each stream during seeking player.</param>
        /// <returns>A task that represents the asynchronous seek operation.</returns>
        /// <remarks>
        /// ESPlayer must be in the <see cref="ESPlayerState.Ready"/>, <see cref="ESPlayerState.Playing"/>, or <see cref="ESPlayerState.Paused"/> state.
        /// Application should set onReadyToSeek callback for submitting es packet.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SeekAsync(10 * 1000, OnReadyToSeek);
        ///     // your logic
        /// }
        /// 
        /// private void OnReadyToSeek(StreamType streamType, TimeSpan offset)
        /// {
        ///     // your logic
        /// }
        /// </code>
        /// <seealso cref="SubmitPacket(ESPacket)"/>
        /// <seealso cref="SubmitPacket(ESHandlePacket)"/>
        /// <exception cref="InvalidOperationException">ESPlayer is not in the valid state.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        /// <exception cref="NullReferenceException">onReadyToPrepare is null.</exception>
        public async Task SeekAsync(TimeSpan timeInMilliseconds, Action<StreamType, TimeSpan> onReadyToSeek)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);

            if (onReadyToSeek == null)
            {
                throw new NullReferenceException("You should set onReadyToSeek for submitting es packet.");
            }

            Log.Info(LogTag, "start");

            ReadyToSeek_ = (s, e) =>
            {
                onReadyToSeek(e.StreamType, e.Offset);
            };

            NativeESPlusPlayer.Seek(player, (ulong)timeInMilliseconds.TotalMilliseconds).VerifyOperation("ESPlayer Seek is failed.");

            var task = Task.Factory.StartNew(() =>
            {
                Log.Info(LogTag, $"before lock");

                var withinTimeout = Utility.WaitFor(lockerForSeek, timeoutForSeek);
                withinTimeout.VerifyOperation("Timeout occurred in SeekAsync");

                Log.Info(LogTag, $"seek in native esplayer is done.");
            });

            await task;
        }

        /// <summary>
        /// Sets the display.
        /// </summary>
        /// <param name="window">A <see cref="ElmSharp.Window"/> that specifies the display.</param>
        /// <remarks>
        /// ESPlayer must be in the <see cref="ESPlayerState.Idle"/> state. Application must call within the EFL main loop.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SetDisplay(GetWindow());
        ///     // your logic
        /// }
        /// 
        /// private ElmSharp.Window GetWindow()
        /// {
        ///     // your logic
        ///     return yourWindow;
        /// }
        /// </code>
        /// <seealso cref="SetDisplayMode(DisplayMode)"/>
        /// <seealso cref="SetDisplayRoi(int, int, int, int)"/>
        /// <exception cref="InvalidOperationException">ESPlayer is not in the valid state.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public void SetDisplay(ElmSharp.Window window)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            display = new ESDisplay(window);

            display.SetDisplay(player).VerifyOperation("ESPlayer SetDisplay is failed.");
        }

        /// <summary>
        /// Sets the <see cref="DisplayMode"/>.
        /// </summary>
        /// <param name="mode">Display mode to be set in the player</param>
        /// <remarks>
        /// ESPlayer must be in the <see cref="ESPlayerState.Idle"/> state.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SetDisplayMode(DisplayMode.FullScreen);
        ///     // your logic
        /// }
        /// </code>
        /// <seealso cref="SetDisplay(ElmSharp.Window)"/>
        /// <seealso cref="SetDisplayRoi(int, int, int, int)"/>
        /// <exception cref="InvalidOperationException">ESPlayer is not in the valid state.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public void SetDisplayMode(DisplayMode mode)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.SetDisplayMode(player, mode).VerifyOperation("ESPlayer SetDisplayMode is failed.");
        }

        /// <summary>
        /// Sets the roi(region of interest).
        /// </summary>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SetDisplayRoi(0, 0, 1280, 720);
        ///     // your logic
        /// }
        /// </code>
        /// <param name="x">The x coordinate of the upper left corner of the display.</param>
        /// <param name="y">The y coordinate of the upper left corner of the display.</param>
        /// <param name="width">The width of the display.</param>
        /// <param name="height">The height of the display.</param>
        /// <seealso cref="SetDisplay(ElmSharp.Window)"/>
        /// <seealso cref="SetDisplayMode(DisplayMode)"/>
        /// <exception cref="InvalidOperationException">ESPlayer is not in the valid state.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The width or the height is less than or equal to zero.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public void SetDisplayRoi(int x, int y, int width, int height)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);

            if(width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, $"The width of the roi can't be less than or equal to zero.");
            }
            else if(height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, $"The height of the roi can't be less than or equal to zero.");
            }

            Log.Info(LogTag, "start");
            // custom roi mode
            NativeESPlusPlayer.SetDisplayMode(player, 5).VerifyOperation("ESPlayer SetDisplayMode is failed.");
            NativeESPlusPlayer.SetDisplayRoi(player, x, y, width, height).VerifyOperation("ESPlayer SetDisplayRoi is failed.");
        }

        /// <summary>
        /// Sets the value indicating whether or not the display is visible.
        /// </summary>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SetDisplayVisible(false);    // Display will be invisible.
        ///     // your logic
        /// }
        /// </code>
        /// <param name="visible">true if the display is visible; otherwise false.</param>
        /// <exception cref="InvalidOperationException">ESPlayer is not in the valid state.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public void SetDisplayVisible(bool visible)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.SetDisplayVisible(player, visible).VerifyOperation("ESPlayer SetDisplayVisible is failed.");
        }

        /// <summary>
        /// Sets the value indicating whether or not the audio is mute.
        /// </summary>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SetAudioMute(true);    // Audio will be mute.
        ///     // your logic
        /// }
        /// </code>
        /// <param name="mute">true if the audio is mute; otherwise false.</param>
        /// <exception cref="InvalidOperationException">ESPlayer is not in the valid state.</exception>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public void SetAudioMute(bool mute)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.SetAudioMute(player, mute).VerifyOperation("ESPlayer SetAudioMute is failed.");
        }

        /// <summary>
        /// Gets current state of ESPlayer.
        /// </summary>
        /// <returns>
        /// Returns current state of ESPlayer.
        /// </returns>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     ESPlayerState state = player.GetState();
        ///     // your logic
        /// }
        /// </code>
        /// <seealso cref="Open"/>
        /// <seealso cref="PrepareAsync(Action{StreamType})"/>
        /// <seealso cref="Start"/>
        /// <seealso cref="Pause"/>
        /// <seealso cref="Resume"/>
        /// <seealso cref="Stop"/>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public ESPlayerState GetState()
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            return NativeESPlusPlayer.GetState(player);
        }

        /// <summary>
        /// Sets stream information for es contents of audio stream.
        /// </summary>
        /// <param name="info">Stream information for es contents of audio stream</param>
        /// <remarks>
        /// ESPlayer must be in the <see cref="ESPlayerState.Idle"/> state.
        /// ESPlayer does not support that it changes stream information during playing the es contents. If application wants to change stream information, application should call <see cref="Stop"/> and, set stream info again. 
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SetStream(GetAudioStreamInfo());
        ///     // your logic
        /// }
        /// 
        /// private AudioStreamInfo GetAudioStreamInfo()
        /// {
        ///     // your logic;
        ///     return yourStreamInfo;
        /// }
        /// </code>
        /// <seealso cref="SetStream(VideoStreamInfo)"/>
        /// <seealso cref="AudioStreamInfo"/>
        /// <seealso cref="VideoStreamInfo"/>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public void SetStream(AudioStreamInfo info)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
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

        /// <summary>
        /// Sets stream information for es contents of video stream.
        /// </summary>
        /// <remarks>
        /// ESPlayer must be in the <see cref="ESPlayerState.Idle"/> state.
        /// ESPlayer does not support that it changes stream information during playing the es contents. If application wants to change stream information, application should call <see cref="Stop"/> and, set stream info again. 
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SetStream(GetVideoStreamInfo());
        ///     // your logic
        /// }
        /// 
        /// private VideoStreamInfo GetVideoStreamInfo()
        /// {
        ///     // your logic;
        ///     return yourStreamInfo;
        /// }
        /// </code>
        /// <seealso cref="SetStream(AudioStreamInfo)"/>
        /// <seealso cref="AudioStreamInfo"/>
        /// <seealso cref="VideoStreamInfo"/>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public void SetStream(VideoStreamInfo info)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
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

        /// <summary>
        /// Pushes es packet to ESPlayer.
        /// </summary>
        /// <remarks>
        /// ESPlayer allows to accept es packets when it is ready to get es packet. (Refer to onReadyToPrepare, onReadyToSeek callback in <seealso cref="PrepareAsync(Action{StreamType})"/>, <seealso cref="SeekAsync(TimeSpan, Action{StreamType, TimeSpan})"/>.)
        /// ESPlayer always checks own buffer state that it is full or empty, emit BufferStatusChanged event with <seealso cref="BufferStatus.Underrun"/> and <seealso cref="BufferStatus.Overrun"/> when es buffer queue is empty or full. Application should register <seealso cref="BufferStatusChanged"/> and check its status, and push the appropriate es buffer at the appropriate time.
        /// </remarks>
        /// <param name="packet">Packet which contains the buffer for es slice of the es contents and information.</param>
        /// <returns>Result of submitting es packet.</returns>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.BufferStatusChanged = OnBufferStatusChanged;
        ///     player.prepareAsync(OnReadyToPrepare);
        ///     // your logic
        /// }
        /// 
        /// private void OnReadyToPrepare(StreamType streamType)
        /// {
        ///     // your logic
        ///     await SubmitPacketThread(streamType);
        ///     // your logic
        /// }
        /// 
        /// private void OnBufferStatusChanged(object sender, BufferStatusEventArgs e)
        /// {
        ///     // your logic
        /// }
        /// 
        /// private async void SubmitPacketThread(StreamType streamType)
        /// {
        ///     // your logic
        ///     var packet = GetPacket(streamType);
        ///     player.SubmitPacket(packet);
        ///     // your logic
        /// }
        /// 
        /// private ESPacket GetPacket(StreamType streamType)
        /// {
        ///     // your logic
        ///     return yourPacket;
        /// }
        /// </code>
        /// <see cref="PrepareAsync(Action{StreamType})"/>
        /// <see cref="SeekAsync(TimeSpan, Action{StreamType, TimeSpan})"/>
        /// <see cref="BufferStatusChanged"/>
        /// <see cref="BufferStatus"/>
        /// <see cref="SubmitStatus"/>
        /// <see cref="ESPacket"/>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public SubmitStatus SubmitPacket(ESPacket packet)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
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

        /// <summary>
        /// Pushes es packet which includes handle of data inside the trust zone to ESPlayer.
        /// </summary>
        /// <remarks>
        /// To play encrypted es contents, application should call <seealso cref="SetTrustZoneUse(bool)"/> in the <seealso cref="ESPlayerState.Idle"/> state. Please refer <seealso cref="SubmitPacket(ESPacket)"/>.
        /// </remarks>
        /// <param name="packet">Packet which contains the buffer for es slice of the encrypted es contents and information inside the trust zone.</param>
        /// <returns>Result of submitting es handle packet.</returns>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SetTrustZoneUse(true);
        ///     player.BufferStatusChanged = OnBufferStatusChanged;
        /// 
        ///     player.prepareAsync(OnReadyToPrepare);
        ///     // your logic
        /// }
        /// 
        /// private void OnReadyToPrepare(StreamType streamType)
        /// {
        ///     // your logic
        ///     await SubmitPacketThread(streamType);
        ///     // your logic
        /// }
        /// 
        /// private void OnBufferStatusChanged(object sender, BufferStatusEventArgs e)
        /// {
        ///     // your logic
        /// }
        /// 
        /// private async void SubmitPacketThread(StreamType streamType)
        /// {
        ///     // your logic
        ///     var packet = GetPacket(streamType);
        ///     player.SubmitPacket(packet);
        ///     // your logic
        /// }
        /// 
        /// private ESHandlePacket GetPacket(StreamType streamType)
        /// {
        ///     // your logic
        ///     return yourPacket;
        /// }
        /// </code>
        /// <see cref="PrepareAsync(Action{StreamType})"/>
        /// <see cref="SeekAsync(TimeSpan, Action{StreamType, TimeSpan})"/>
        /// <see cref="SetTrustZoneUse(bool)"/>
        /// <see cref="SubmitPacket(ESHandlePacket)"/>
        /// <see cref="BufferStatusChanged"/>
        /// <see cref="BufferStatus"/>
        /// <see cref="SubmitStatus"/>
        /// <see cref="ESHandlePacket"/>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public SubmitStatus SubmitPacket(ESHandlePacket packet)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
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

        /// <summary>
        /// Generates eos(end of stream) packet explicitly and pushes it to ESPlayer.
        /// </summary>
        /// <remarks>
        /// ESPlayer emits <seealso cref="EOSEmitted"/> event when the eos packet reaches for all type of stream.
        /// </remarks>
        /// <param name="type">Stream type which reaches eos.</param>
        /// <returns>Result of submitting eos packet.</returns>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SubmitEosPacket(StreamType.Audio);
        ///     player.SubmitEosPacket(StreamType.Video);
        ///     // your logic
        /// }
        /// </code>
        /// <see cref="SubmitPacket(ESPacket)"/>
        /// <see cref="SubmitPacket(ESHandlePacket)"/>
        /// <see cref="StreamType"/>
        /// <see cref="SubmitStatus"/>
        /// <see cref="EOSEmitted"/>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public SubmitStatus SubmitEosPacket(StreamType type)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, $"[{type}] submit eos packet");
            return NativeESPlusPlayer.SubmitEOSPacket(player, type);
        }

        /// <summary>
        /// Sets whether or not encrypted contents in the trust zone area plays.
        /// </summary>
        /// <remarks>
        /// Default setting is false. ESPlayer must be in the <see cref="ESPlayerState.None"/> state.
        /// </remarks>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     player.SetTrustZoneUse(true);
        ///     // your logic
        /// }
        /// </code>
        /// <param name="isUsing">true if the contents is ecrypted in the trust zone; otherwise false.</param>
        /// <see cref="SubmitPacket(ESHandlePacket)"/>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public void SetTrustZoneUse(bool isUsing)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");

            /*
            var state = GetState();
            if(state != ESPlayerState.Idle)
            {
                throw new InvalidOperationException($"Setting using trust zone mode is only allow in Idle state. Current state : {state}");
            }
            */

            NativeESPlusPlayer.SetTrustZoneUse(player, isUsing).VerifyOperation("ESPlayer SetTrustZoneUse is failed.");
        }
        
        /// <summary>
        /// Gets the current position in milliseconds.
        /// </summary>
        /// <param name="timeInMilliseconds">Stored the current position in milliseconds to the out parameter</param>
        /// <code>
        /// public void Apps()
        /// {
        ///     var player = new ESPlayer();
        ///     // your logic
        ///     TimeSpan currentTime;
        ///     player.GetPlayingTime(currentTime);
        ///     // your logic
        /// }
        /// </code>
        /// </code>
        /// <exception cref="ObjectDisposedException">ESPlayer instance is disposed or not created yet.</exception>
        public void GetPlayingTime(out TimeSpan timeInMilliseconds)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            NativeESPlusPlayer.GetPlayingTime(player, out ulong ms).VerifyOperation("ESPlayer AddStream is failed.");
            timeInMilliseconds = TimeSpan.FromMilliseconds(ms);
        }

        /*
        private void SetDrm(DrmType type)
        {
            Utility.ValidatePlayerObject(ref player, disposed_);
            Log.Info(LogTag, "start");
            NativeESPlusPlayer.SetDrm(player, type).VerifyOperation("ESPlayer SetDrm is failed.");
        }
        */

        #region IDisposable Support
        private bool disposed_ = false;

        /// <summary>
        /// Releases all resources used by the current ESPlayer instance.
        /// </summary>
        /// <code>
        /// public void Apps()
        /// {
        ///     /* <seealso cref="Dispose"/> will be called automatically when the using block has finished execution. */
        ///     using(var player = new ESPlayer())
        ///     {
        ///         // your logic
        ///     }
        /// }
        /// </code>
        public void Dispose()
        {
            Log.Info(LogTag, "Dispose() is called.");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ESPlayer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ESPlayer"/>.
        /// </summary>
        /// <remarks>Please refer https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/dispose-pattern </remarks>
        /// <param name="disposing">
        /// true to release both managed and unmanaged resources; false to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed_)
            {
                if (disposing)
                {
                    // dispose unmanaged resource.
                }

                try
                {
                    Stop();
                    Close();
                    Log.Info(LogTag, "this.Close() is called.");
                }
                catch (Exception ex)
                {
                    Log.Error(LogTag, $"ex : {ex.Message}");
                }
                finally
                {
                    player = IntPtr.Zero;
                    disposed_ = true;
                }
            }
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

        public static bool WaitFor(object locker, TimeSpan timeout)
        {
            lock (locker)
            {
                return Monitor.Wait(locker, timeout);
            }
        }

        public static void ValidatePlayerObject(ref IntPtr player, bool disposed)
        {
            if (player == IntPtr.Zero || disposed)
            {
                throw new ObjectDisposedException("ESPlayer instance is disposed or not created yet.");
            }
        }
    }
}