/// @file SmplayerWrapper.cs
/// <published> N </published>
/// <privlevel> partner </privlevel>
/// <privilege> http://developer.samsung.com/privilege/drminfo </privilege>
/// <privacy> N </privacy>
/// <product> TV </product>
/// <version> 5.5.0 </version>
/// <SDK_Support> N </SDK_Support>
/// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved  
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
using System.IO;
using static Interop;
using System.Threading;
using System.Threading.Tasks;
using Tizen.TV.Security.Privilege;

namespace Tizen.TV.Multimedia.IPTV
{
    /// <summary>
    /// SmplayerWrapper class which implements the IPlayerAdapter for sm-player on Tizen TV. 
    /// </summary>
    /// <code>
    /// class Sample : IPlayerAdapter, IPlayerEventListener
    /// {
    ///     private SmplayerWrapper playerInstance;
    ///     
    ///     public Sample()
    ///     {
    ///          playerInstance = new SmplayerWrapper();
    ///          playerInstance.RegisterPlayerEventListener(this);
    ///          bool result = playerInstance.Initialize(true); 
    ///     }
    ///     
    ///     public bool PrepareES()
    ///     {
    ///          bool result = playerInstance.PrepareES();
    ///          return result;
    ///     }
    ///     .....
    /// }
    /// </code>
    public class SmplayerWrapper : IPlayerAdapter
    {
        /// <summary> property for its dlog tag </summary>
        private const string LogTag = "Tizen.TV.Multimedia.IPTV";

        /// <summary> property for whether the player is playing now </summary>
        public bool isPlaying;
        /// <summary> property for whether the player is the first init </summary>
        public bool isInit;
        /// <summary> property for player's current playback time </summary>
        public uint currentPosition;
        /// <summary> property for player's event listener </summary>
        public IPlayerEventListener smplayerEventListener;
        /// <summary> property for player's stop successful flag </summary>
        public bool isStopSuccess;

        /// <summary> property for player's current position callback function </summary>
        private NativeSmplayer.SmpCurrentPositionCallback currentPosCallback;
        /// <summary> property for player's message callback function </summary>
        private NativeSmplayer.SmplayerMessageCallback messageCallback;
        /// <summary> property for player's audio need data callback function </summary>
        private NativeSmplayer.SmplayerAppSrcNeedDataCallback audioNeedDataCallback;
        /// <summary> property for player's video need data callback function </summary>
        private NativeSmplayer.SmplayerAppSrcNeedDataCallback videoNeedDataCallback;
        /// <summary> property for player's audio data enough callback function </summary>
        private NativeSmplayer.SmplayerAppSrcDataEnoughCallback audioDataEnoughCallback;
        /// <summary> property for player's video data enough callback function </summary>
        private NativeSmplayer.SmplayerAppSrcDataEnoughCallback videoDataEnoughCallback;
        /// <summary> property for player's audio seek data callback function </summary>
        private NativeSmplayer.SmplayerBufferSeekDataCallback audioSeekDataCallback;
        /// <summary> property for player's video seek data callback function </summary>
        private NativeSmplayer.SmplayerBufferSeekDataCallback videoSeekDataCallback;

        /// <summary>
        /// type for setting player's message type
        /// </summary> 
        public enum SmpMsgType
        {
            /// <summary> unknown message type, it is default type </summary>
            UnKnown = 0x00,
            /// <summary> Init completed message type, it means that player is inited done </summary>
            InitComplete = 0X20,
            /// <summary> Pause completed message type, it means that player is paused done </summary>
            PauseComplete,
            /// <summary> Resume completed message type, it means that player is resumed done </summary>
            ResumeComplete,
            /// <summary> Seek completed message type, it means that player is seek completly </summary>
            SeekComplete,
            /// <summary> Stop successfully message type, it means that player is stopped done </summary>
            StopSuccess,
            /// <summary> Resource manager stop started message type, it means that Resource manager stop started </summary>
            RmStopStart,
            /// <summary> Resource manager stop successfully message type, it means that Resource manager stop successfully </summary>
            RmStopSuccess,
            /// <summary> Seek done message type, it means that player is seek done </summary>
            SeekDone,
            /// <summary> Init failed message type, it means that player's initialization is failed </summary>
            InitFailed = 0X40,
            /// <summary> Play failed message type, it means that player's play is failed </summary>
            PlayFailed,
            /// <summary> Pause failed message type, it means that player's pause is failed </summary>
            PauseFailed,
            /// <summary> Resume failed message type, it means that player's resume is failed </summary>
            ResumeFailed,
            /// <summary> Seek failed message type, it means that player's seek is failed </summary>
            SeekFailed,
            /// <summary> Trick failed message type, it means that player's trick is failed </summary>
            TrickFailed,
            /// <summary> Set play speed failed message type, it means that player's Setting play speed is failed </summary>
            SetSpeedFailed,
            /// <summary> Stop failed message type, it means that player's stop is failed </summary>
            StopFailed,
            /// <summary> Begin of stream message type, it means that player is play at the beginning of the stream </summary>
            BeginOfStream = 0X60,
            /// <summary> End of stream message type, it means that player is play at the end of the stream </summary>
            EndOfStream,
            /// <summary> Error message type, it means that player met error </summary>
            Error,
            /// <summary> Warning message type, it means that player met Warning </summary>
            Warning,
            /// <summary> State changed message type, it means that player's state is changed </summary>
            StateChanged,
            /// <summary> State interrupted message type, it means that player's state is changed by interrupt </summary>
            StateInterrupted,
            /// <summary> Ready to resume message type, it means that player is ready to resume message type </summary>
            ReadyToResume,

            /// <summary> Connecting message type, it means that Rtspsrc is connecting </summary>
            Connecting = 0X80,
            /// <summary> Connected message type, it means Rtspsrc has successed to connecting to server </summary>
            Connected,
            /// <summary> Connection timeout message type, it means Rtspsrc is connected timeout </summary>
            ConnectionTimeOut,
            /// <summary> Buffering message type, it means player is buffering </summary>
            Buffering,
            /// <summary> Update subtitle message type, it means player need to update subtitle </summary>
            UpdateSubtitle,
            /// <summary> File not supported message type, it means file is not supported by player </summary>
            FileNotSupported,
            /// <summary> File not found message type, it means file is not found by player </summary>
            FileNotFound,
            /// <summary> Subtite text message, it means this message is subtite text </summary>
            SubtitleText,
            /// <summary> Content duration message, it means this message is content duration </summary>
            Duration,
            /// <summary> Current position of playback message, it means this message is player's current position of playback </summary>
            CurrentPosition,
            /// <summary> Network down message, it means current network is down </summary>
            NetworkDown,
            /// <summary> Trick play down message, it means trick play is down </summary>
            TrickDown,
            /// <summary> Closed caption message, it means this message is about closed caption </summary>
            ClosedCaption,
            /// <summary> Buffer drop message, it means this message is about bffer drop </summary>
            BufferDrop,
            /// <summary> HdrVideo message, it means current content is HDR content </summary>
            HdrVideo,

            /// <summary> Render done message, it is code addded for render done event </summary>
            RenderDone = 0XB0,
            /// <summary> Bitrate changed message, it is code addded for bitrate change event </summary>
            BitrateChanged,
            /// <summary> Fragment download message, This event would be generated by adaptive source element and send fragment info. </summary>
            FragmentDownload,
            /// <summary> Sparse track detect message, This event would be generated by adaptive source element. </summary>
            SparseTrackDetect,
            /// <summary> Streaming event message, This event would be generated by adaptive source element. </summary>
            StreamingEvent,

            /// <summary> Drm chanllenge data message, This event is only for Canal+. </summary>
            DrmChallengeData = 0XC0,
            /// <summary> Unsupported container message, This event is only for Canal+. </summary>
            UnsupportedContainer,
            /// <summary> Unsupported video codec message, This event is only for Canal+. </summary>
            UnsupportedVideoCodec,
            /// <summary> Unsupported audio codec message, This event is only for Canal+. </summary>
            UnsupportedAudioCodec,
            /// <summary> Video resolution changed message, This event is only for Canal+. </summary>
            ResolutionChanged,
            /// <summary> Connection failed message, This event is only for Canal+. </summary>
            ConnectionFailed,
            /// <summary> Unauthorized message, This event is only for Canal+. </summary>
            Unauthorized,
            /// <summary> Pssh box update message, This event is only for Canal+. </summary>
            UpdatePsshData,
            /// <summary>The number of the messages </summary>
            MessageNum,
        };

        /// <summary>
        /// The SmplayerWrapper constructor.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown when sm-player api is used on the emulator.
        /// </exception>
        /// <exception cref="Cynara Initialization Fail!">
        /// Thrown when Cynara Initialization Fail.
        /// </exception>
        /// <exception cref="MethodAccessException">
        /// Thrown when CynaraCheck denied.
        /// </exception>
        public SmplayerWrapper()
        {

            /*
#if EMULATOR
            throw new PlatformNotSupportedException("NOT supported on emulator");
#else
#endif
            string smackLabel;
            string uid;
            string clientSession = "";
            string privilege = "http://developer.samsung.com/privilege/drminfo";

            // Cynara  structure init
            if (Cynara.CynaraInitialize(false) != 0)
            {
                Log.Error(LogTag, "Cynara Initialization Fail!");
                throw new Exception("Cynara Initialization Fail!");
            }

            smackLabel = File.ReadAllText("/proc/self/attr/current");
            uid = Cynara.GetUid();

            try
            {
                int retValue = Cynara.CynaraCheck(smackLabel, clientSession, uid, privilege);
                if (retValue == 2)
                {
                    Log.Info(LogTag, "Cynara Initialization Successfully!");
                    Tizen.TV.Security.Privilege.Cynara.CynaraFinish();
                }
                else
                {
                    Log.Error(LogTag, "CynaraCheck denied (return value:" + retValue + " /smakeLabel:" + smackLabel + " /privilege name:" + privilege + ")");
                    Tizen.TV.Security.Privilege.Cynara.CynaraFinish();
                    throw (new MethodAccessException(privilege));
                }
            }
            catch (ObjectDisposedException e)
            {
               
                Log.Error(LogTag, "Not Initialize Cynara!");
            }
        */
        }

        /// <summary>
        /// callback function, it will callback the current play position of player.
        /// </summary>
        /// <param name="currTime">the current play position of player </param>
        /// <param name="userParam">the userParam which is set when register this callback </param>
        public void OnCurrentPos(UInt32 currTime, IntPtr userParam)
        {
            //string msg = "Current PlaybackTime: " + currTime.ToString();
            //NativeSmplayer.TestCallbackPrint(msg);
            smplayerEventListener.OnCurrentPosition(currTime);
            //please implement your code here , currTime is current playback time
        }

        /// <summary>
        /// callback function, it will be called when player wants to report message to app side.
        /// </summary>
        /// <param name="id">the id which means the message type </param>
        /// <param name="param">the param info which is sent by the message </param>
        /// <param name="userParam">the userParam which is set when register this callback </param>
        /// <returns>The result of OnMessage callback </returns>
        public int OnMessage(int id, IntPtr param, IntPtr userParam)
        {
            string msg = "Receive MsgId: " + id.ToString();
            // NativeSmplayer.TestCallbackPrint(msg);
            Log.Info(LogTag, msg);

            SmpMsgType msgType = (SmpMsgType)id;
            switch (msgType)
            {
                /* General: */
                case SmpMsgType.BeginOfStream:
                    break;
                case SmpMsgType.EndOfStream:
                    if (isPlaying)
                    {
                        isPlaying = false;
                        smplayerEventListener.OnEndOfStream();
                    }
                    break;

                case SmpMsgType.Buffering:
                    // SMPlayer does not generate buffering callbacks
                    break;

                /* Complete notifications: */
                case SmpMsgType.SeekComplete:
                    // SeekComplete is send by SMPlayer when playback is
                    // in new place runing.
                    smplayerEventListener.OnSeekCompleted();
                    break;
                case SmpMsgType.SeekDone:
                    // SMP_MESSAGE_SEEK_DONE is send by SMPlayer when seek called on
                    // Gstreamer.
                    // For seek to actualy happen new packets must be buffered.
                    smplayerEventListener.OnSeekStartedBuffering();
                    break;
                case SmpMsgType.InitComplete:
                    if(smplayerEventListener != null)
                    {
                        smplayerEventListener.OnInitComplete();
                    }
                    break;
                case SmpMsgType.PauseComplete:
                    break;
                case SmpMsgType.ResumeComplete:
                    break;
                case SmpMsgType.StopSuccess:
                    //Log.Info(LogTag, " [HQ]SmpMsgType.StopSuccess!");
                    //isStopSuccess = true;
                    break;

                /* Subtitles: */
                case SmpMsgType.UpdateSubtitle:
                    //OnShowSubtitle(param);              //subtitle need to confirm with CP
                    break;
                case SmpMsgType.SubtitleText:
                    // Deprecated. Subtitles are sent now with UPDATE_SUBTITLE message
                    break;

                /* Errors: */
                case SmpMsgType.InitFailed:
                    smplayerEventListener.OnInitFailed();
                    break;
                case SmpMsgType.PlayFailed:
                    smplayerEventListener.OnError(PlayerErrorType.Unknown, "Play failed.");
                    break;
                case SmpMsgType.PauseFailed:
                    smplayerEventListener.OnError(PlayerErrorType.Unknown, "Pause failed.");
                    break;
                case SmpMsgType.ResumeFailed:
                    smplayerEventListener.OnError(PlayerErrorType.Unknown, "Resume Failed.");
                    break;
                case SmpMsgType.SeekFailed:
                    smplayerEventListener.OnError(PlayerErrorType.Unknown, "Seek failed.");
                    break;
                case SmpMsgType.StopFailed:
                    smplayerEventListener.OnError(PlayerErrorType.Unknown, "Stop failed.");
                    break;
                case SmpMsgType.UnsupportedContainer:
                    smplayerEventListener.OnError(PlayerErrorType.UnsupportedContainer,
                            "Unsupported container.");
                    break;
                case SmpMsgType.UnsupportedVideoCodec:
                    smplayerEventListener.OnError(PlayerErrorType.UnsupportedCodec,
                            "Unsupported video codec.");
                    break;
                case SmpMsgType.UnsupportedAudioCodec:
                    smplayerEventListener.OnError(PlayerErrorType.UnsupportedCodec,
                            "Unsupported audio codec.");
                    break;
                case SmpMsgType.ConnectionFailed:
                    smplayerEventListener.OnError(PlayerErrorType.Network, "Connection failed.");
                    break;
                case SmpMsgType.Unauthorized:
                    smplayerEventListener.OnError(PlayerErrorType.Unknown, "Unauthorized.");
                    break;

                default:
                    break;
            }


            return 1;
        }

        /// <summary>
        /// callback function, it will be called when player is doing seek operation.
        /// </summary>
        /// <param name="offset"> the offset the seek operation  </param>
        /// <param name="userParam">the userParam which is set when register this callback </param>
        /// <returns>The result of AudioSeekCB callback </returns>
        public bool AudioSeekCB(UInt64 offset, IntPtr userParam)
        {
            smplayerEventListener.OnSeekData(StreamType.Audio, offset);
            return true;
        }

        /// <summary>
        /// callback function, it will be called when player is doing seek operation.
        /// </summary>
        /// <param name="offset"> the offset the seek operation  </param>
        /// <param name="userParam">the userParam which is set when register this callback </param>
        /// <returns>The result of VideoSeekCB callback </returns>
        public bool VideoSeekCB(UInt64 offset, IntPtr userParam)
        {
            smplayerEventListener.OnSeekData(StreamType.Video, offset);
            return true;
        }

        /// <summary>
        /// callback function, it will be called when player need app side to send audio es data.
        /// </summary>
        /// <param name="size"> the size of audio es data needed by player  </param>
        /// <param name="userParam">the userParam which is set when register this callback </param>
        /// <returns>The result of AudioNeedDataCB callback </returns>
        public void AudioNeedDataCB(UInt32 size, IntPtr userParam)
        {
            smplayerEventListener.OnNeedData(StreamType.Audio, size);
        }

        /// <summary>
        /// callback function, it will be called when player need app side to send video es data.
        /// </summary>
        /// <param name="size"> the size of video es data needed by player  </param>
        /// <param name="userParam">the userParam which is set when register this callback </param>
        /// <returns>The result of VideoNeedDataCB callback </returns>
        public void VideoNeedDataCB(UInt32 size, IntPtr userParam)
        {
            smplayerEventListener.OnNeedData(StreamType.Video, size);
        }

        /// <summary>
        /// callback function, it will be called when player has enough audio es data.
        /// </summary>
        /// <param name="userParam">the userParam which is set when register this callback </param>
        /// <returns>The result of AudioDataEnoughCB callback </returns>
        public void AudioDataEnoughCB(IntPtr userParam)
        {
            smplayerEventListener.OnEnoughData(StreamType.Audio);
        }

        /// <summary>
        /// callback function, it will be called when player has enough video es data.
        /// </summary>
        /// <param name="userParam">the userParam which is set when register this callback </param>
        /// <returns>The result of VideoDataEnoughCB callback </returns>
        public void VideoDataEnoughCB(IntPtr userParam)
        {
            smplayerEventListener.OnEnoughData(StreamType.Video);
        }

        /// <summary>
        /// callback function, it will be called when player need to send back the subtitle info.
        /// </summary>
        /// <param name="param">the userParam which is set when register this callback </param>
        /// <param name="msg">the msg info of the subtitle </param>
        public void SmpSubtitleDataCB(IntPtr param, IntPtr msg)     
        {
            //No need to do until now
        }

        /// <summary>
        /// API of initializing player,it will create player's instance and set corresponding callbacks.
        /// </summary>
        /// <param name="isEsPlay">if the initialization is for es play </param>
        /// <returns>The result of initializing player </returns>
        public bool Initialize(bool isEsPlay)
        {
            return NativeSmplayer.Initialize() && SetPlayerCallbacks(isEsPlay);
        }

        /// <summary>
        /// API of resetting player, it will stop player and destroy its instance
        /// </summary>
        /// <returns>The result of resetting player </returns>
        public bool Reset()
        {
            Stop();
            bool result = DestroyHandler();
            return result;
        }

        /// <summary>
        /// Method of registering player callbacks to player.
        /// </summary>
        /// <param name="isEsPlay">if the initialization is for es play </param>
        /// <returns>The result of registering player callbacks to player </returns>
        bool SetPlayerCallbacks(bool isEsPlay)
        {
            bool result = false;

            currentPosCallback = OnCurrentPos;
            messageCallback = OnMessage;
            audioNeedDataCallback = AudioNeedDataCB;
            videoNeedDataCallback = VideoNeedDataCB;
            audioDataEnoughCallback = AudioDataEnoughCB;
            videoDataEnoughCallback = VideoDataEnoughCB;
            audioSeekDataCallback = AudioSeekCB;
            videoSeekDataCallback = VideoSeekCB;

            result = NativeSmplayer.SetCurrentPositionCallback(currentPosCallback, (IntPtr)0);
            if (result == false)
            {
                return result;
            }

            result = NativeSmplayer.SetMessageCallback(messageCallback, (IntPtr)0);
            if (result == false)
            {
                return result;
            }
			if (!isEsPlay)
            {
                return result;
            }
            result = NativeSmplayer.SetAppSrcAudioSeekCallback(audioSeekDataCallback, (IntPtr)0);
            if (result == false)
            {
                return result;
            }

            result = NativeSmplayer.SetAppSrcVideoSeekCallback(videoSeekDataCallback, (IntPtr)0);
            if (result == false)
            {
                return result;
            }

            result = NativeSmplayer.SetAppSrcAudioNeedDataCallback(audioNeedDataCallback, (IntPtr)0);
            if (result == false)
            {
                return result;
            }

            result = NativeSmplayer.SetAppSrcVideoNeedDataCallback(videoNeedDataCallback, (IntPtr)0);
            if (result == false)
            {
                return result;
            }

            result = NativeSmplayer.SetAppSrcAudioDataEnoughCallback(audioDataEnoughCallback, (IntPtr)0);
            if (result == false)
            {
                return result;
            }

            result = NativeSmplayer.SetAppSrcVideoDataEnoughCallback(videoDataEnoughCallback, (IntPtr)0);
            if (result == false)
            {
                return result;
            }

            return result;
        }


        /// <summary>
        /// API of preparing player, it will prepare player for es play mode.
        /// </summary>
        /// <returns>The result of preparing player </returns>
        public bool PrepareES()
        {
            bool result = NativeSmplayer.PrepareES();
            // if (result && isInit)
            // {
            //     result = SetPlayerCallbacks(true);
            //     isInit = false;
            // }
            return result;
        }

        /// <summary>
        /// API of preparing player, it will prepare player for url play mode.
        /// </summary>
        /// <param name="url">url for the movie which need to be played by player</param>
        /// <returns>The result of preparing player </returns>
        public bool PrepareURL(string url)
        {
            bool result = NativeSmplayer.PrepareURL(url);
            //  if (result && isInit)
            // {
            //     result = SetPlayerCallbacks(false);
            //     isInit = false;
            // }
            return result;
        }

        /// <summary>
        /// API of unpreparing player, it will stop the player.
        /// </summary>
        /// <returns>The result of unpreparing player </returns>
        public bool Unprepare()
        {
            bool result = Stop();
            return result;
        }

        /// <summary>
        /// Player controller API, it will call the player to play.
        /// </summary>
        /// <returns>The result of calling player to play </returns>
        public bool Play()
        {
            bool result = NativeSmplayer.Play(0);
            if (result)
            {
                isPlaying = true;
                currentPosition = 0;
            }
            return result;
        }

        /// <summary>
        /// Player controller API, it will call the player to pause.
        /// </summary>
        /// <returns>The result of calling player to pause </returns>
        public bool Pause()
        {
            bool result = NativeSmplayer.Pause();
            if (result)
            {
                isPlaying = false;
            }
            return result;
        }

        /// <summary>
        /// Player controller API, it will call the player to resume.
        /// </summary>
        /// <returns>The result of calling player to resume </returns>
        public bool Resume()
        {
            bool result = NativeSmplayer.Resume();
            if (result)
            {
                isPlaying = true;
            }
            return result;
        }

        /// <summary>
        /// Player controller API, it will call the player to seek forward.
        /// </summary>
        /// <param name="absoluteTimeinMS">jump forward position for playback in milliseconds </param>
        /// <returns>The result of calling player to seek </returns>
        public bool Seek(int absoluteTimeinMS)
        {
            bool result = NativeSmplayer.Seek(absoluteTimeinMS);
            if (result)
            {
                currentPosition = (uint)(absoluteTimeinMS / 1000);
            }
            return result;
        }

        /// <summary>
        /// Player controller API, it will set the player's play speed.
        /// </summary>
        /// <param name="rate">play speed which is set to player </param>
        /// <returns>The result of calling player to setting speed </returns>
        public bool SetPlaySpeed(float rate)
        {
            bool result = NativeSmplayer.SetPlaySpeed(rate);
            return result;
        }

        /// <summary>
        /// Player controller API, it will notify player that corresponding stream is end of stream.
        /// </summary>
        /// <param name="trackType">Track Type, the type of stream </param>
        /// <returns>The result of calling player that corresponding stream is end of stream </returns>
        public bool SubmitEOSPacket(TrackType trackType)
        {
            bool result = NativeSmplayer.SubmitEOSPacket(trackType);
            return result;
        }

        /// <summary>
        /// Player controller API, it will send es data to player.
        /// </summary>
        /// <param name="buf">The pointer which point to the es data buffer </param>
        /// <param name="size">The size of the es data </param>
        /// <param name="pts">The pts of the es data </param>
        /// <param name="streamType">The stream type of the es data </param>
        /// <param name="drmInfo">The pointer which point to the drmInfo </param>
        /// <returns>The result of sending es data to player </returns>
        public bool SubmitPacket(IntPtr buf, uint size, UInt64 pts, TrackType streamType, IntPtr drmInfo)
        {
            bool result = NativeSmplayer.SubmitPacket(buf, size, pts, streamType, drmInfo);
            return result;
        }

        /// <summary>
        /// Player controller API, it will set the player to stop.
        /// </summary>
        /// <returns>The result of calling player to stop </returns>
        public bool Stop()
        {
            //isStopSuccess = false;
            bool result = NativeSmplayer.Stop();
           // if (result)
           // {
            isPlaying = false;
          //  }
          //  else
          //  {
          //      while (!isStopSuccess)
          //      {
          //          Thread.Sleep(3);
          //      }
           //     isPlaying = false;
           // }
            return result;
        }

        /// <summary>
        /// Player controller API, it will destroy the player's instance.
        /// </summary>
        /// <returns>The result of calling player to destroy its instance </returns>
        public bool DestroyHandler()
        {
            bool result = NativeSmplayer.DestroyHandler();
            return result;
        }

        /// <summary>
        /// Player's setter API, it will set the application id to player.
        /// </summary>
        /// <param name="applicationId">the application's id, format is string </param>
        /// <returns>The result of setting the application id to player </returns>
        public bool SetApplicationID(string applicationId)
        {
            string desktop_id = "";
            string app_id = applicationId;
            string widget_id = "";
            bool result = NativeSmplayer.SetAppInfo(desktop_id, app_id, widget_id);
            return result;
        }

        /// <summary>
        /// Player controller API, it will set the contents duration for external feeder case.
        /// </summary>
        /// <param name="iDuration"> content duration </param>
        /// <returns> Ture if succeed </returns>
        public bool SetDuration(ulong iDuration)
        {
            bool result = NativeSmplayer.SetDuration(iDuration);
            return result;
        }


        /// <summary>
        /// Player's setter API, it will set the movie's audio streamInfo to player.
        /// </summary>
        /// <param name="audioStreamInfo">the movie's audio streamInfo </param>
        /// <returns>The result of setting the audio streamInfo to player </returns>
        public bool SetAudioStreamInfo(AudioStreamInfo audioStreamInfo)
        {
            bool result = NativeSmplayer.SetAudioStreamInfo(audioStreamInfo);
            return result;
        }

        /// <summary>
        /// Player's setter API, it will set the movie's video streamInfo to player.
        /// </summary>
        /// <param name="videoStreamInfo">the movie's video streamInfo </param>
        /// <returns>The result of setting the video streamInfo to player </returns>
        public bool SetVideoStreamInfo(VideoStreamInfo videoStreamInfo)
        {
            bool result = NativeSmplayer.SetVideoStreamInfo(videoStreamInfo);
            return result;
        }

        /// <summary>
        /// Player's setter API, it will set the movie's display options to player.
        /// </summary>
        /// <param name="type">the movie's display type </param>
        /// <param name="display">the movie's display window handler </param>
        /// <returns>The result of setting movie's display options to player </returns>
        public bool SetDisplay(PlayerDisplayType type, IntPtr display)
        {
            bool result = NativeSmplayer.SetDisplay(type, display);
            return result;
        }

        /// <summary>
        /// Player's setter API, it will set the movie's external subtitles path to player.
        /// </summary>
        /// <param name="filePath">the movie's external subtitle's file path </param>
        /// <param name="encoding">the movie's external subtitle's encoding </param>
        /// <returns>The result of the movie's external subtitles path to player </returns>
        public bool SetExternalSubtitlesPath(string filePath, string encoding)
        {
            bool result = NativeSmplayer.StartSubtitle(filePath, encoding);
            return result;
        }

        /// <summary>
        /// Player's setter API, it will set the movie's subtitles delay time to player.
        /// </summary>
        /// <param name="milliSec">the movie's subtitles delay time </param>
        /// <returns>The result of setting movie's subtitles delay time to player </returns>
        public bool SetSubtitlesDelay(int milliSec)
        {
            bool result = NativeSmplayer.SetSubtitlesDelay(milliSec);
            return result;
        }

        /// <summary>
        /// Player's getter API, it will get the duration of the playback.
        /// </summary>
        /// <returns> The duration of the movie which is played </returns>
        public ulong GetDuration()
        {
            ulong result = 0;
            result = NativeSmplayer.GetDuration();
            return result;
        }

        /// <summary>
        /// Player's getter API, it will get the player's current play state.
        /// </summary>
        /// <returns>The player state of current player </returns>
        public PlayerState GetPlayerState()
        {
            PlayerState result = NativeSmplayer.GetPlayerState();
            return result;
        }

        /// <summary>
        /// Player's API, it will register player's eventListener to player.
        /// </summary>
        /// <param name="eventListener">the player's eventListener </param>
        /// <returns>The setting result of registering eventListener </returns>
        public bool RegisterPlayerEventListener(IPlayerEventListener eventListener)
        {
            smplayerEventListener = eventListener;
            return true;
        }

        /// <summary>
        /// Player's API, it will remove player's eventListener from player.
        /// </summary>
        /// <returns>The result of removing eventListener </returns>
        public bool RemoverPlayerEventListener()
        {
            smplayerEventListener = null;
            return true;
        }
    }
}
