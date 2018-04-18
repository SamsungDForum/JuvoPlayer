/// @file IPlayerEventListener.cs
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
using System.Runtime.InteropServices;

namespace Tizen.TV.Multimedia.IPTV
{
    /// <summary>
    /// type for setting player's stream type
    /// </summary> 
    public enum StreamType
    {
        /// <summary> Audio stream type </summary>
        Audio = 0,
        /// <summary> Video stream type </summary>
        Video
    };

    /// <summary>
    /// type for player's eror message type
    /// </summary> 
    public enum PlayerErrorType
    {
        /// <summary> unknown type, it is default type </summary>
        Unknown = 0,
        /// <summary> unsupported container type, which shows error in the gstreamer </summary>
        UnsupportedContainer,
        /// <summary> unsupported codec, which shows the movie's codec type is not supported by player </summary>
        UnsupportedCodec,
        /// <summary> network error, which shows the network needed by player is not met </summary>
        Network,
        /// <summary> init failed, which shows initialization of player is failed </summary>
        InitFailed
    };

    /// <summary>
    /// type for player's message type
    /// </summary> 
    public enum PlayerMsgType
    {
        /// <summary> unknown type, it is default type </summary>
        Unknown = 0,
        /// <summary> init complete, which shows initialization of player is complete </summary>
        InitComplete,
        /// <summary> seek done, which shows seek operation of player is done </summary>
        SeekDone,
        /// <summary> seek completed, which shows seek operation of player is completed </summary>
        SeekCompleted,
        /// <summary> end of stream, which shows the es data stream is end </summary>
        EndOfStream,
        /// <summary> update subtilte, which shows that the subtitle need to be updated </summary>
        UpdateSubtitle
    };

    /// <summary>
    /// Interface which should be implemented by all player wrappers
    /// It will register and callback player related events.
    /// </summary>
    public interface IPlayerEventListener
    {



        /// <summary>
        /// callback function, it will be called when player gets enough es data for type streamType.
        /// Then you need to stop submitting es data and wait for callback OnNeedData(streamType).
        /// </summary>
        /// <param name="streamType">the stream type, Audio or Video </param>
        void OnEnoughData(StreamType streamType);

        /// <summary>
        /// callback function, it will be called when player need es data for type streamType.
        /// Then you need to submit corresponding es data to player.
        /// </summary>
        /// <param name="streamType">the stream type, Audio or Video </param>
        /// <param name="size">the size of data needed by player </param>
        void OnNeedData(StreamType streamType, uint size);

        /// <summary>
        /// callback function, it will be called when player is doing seek operation.
        /// </summary>
        /// <param name="streamType">the stream type, Audio or Video </param>
        /// <param name="offset">the offset the seek operation </param>
        void OnSeekData(StreamType streamType, UInt64 offset);

        /// <summary>
        /// callback function, it will be called when player wants to report error to app side.
        /// OnError means that there is error need to check, please refer to PlayerErrorType and msg.
        /// For example, Network means network error, APP should stop play operation and show pop such as 'network error' etc
        /// </summary>
        /// <param name="errorType">the eror type defined by player </param>
        /// <param name="msg">the msg info of error message, format string </param>
        void OnError(PlayerErrorType errorType, string msg);

        /// <summary>
        /// callback function, it will be called when player wants to report message to app side.
        /// </summary>
        /// <param name="msgType">the message type defined by player </param>
        void OnMessage(PlayerMsgType msgType);

        /// <summary>
        /// callback function, it will be called when player's initialization is done.
        /// </summary>
        void OnInitComplete();

        /// <summary>
        /// callback function, it will be called when player's initialization is failed.
        /// </summary>
        void OnInitFailed();

        /// <summary>
        /// callback function, it will be called when player's play is at the end of the stream.
        /// </summary>
        void OnEndOfStream();

        /// <summary>
        /// callback function, it will be called when player's seek is cpmpleted.
        /// </summary>
        void OnSeekCompleted();

        /// <summary>
        /// callback function, it will be called when player's seek is started to buffering.
        /// then you can submit new es data after seek
        /// </summary>
        void OnSeekStartedBuffering();

        /// <summary>
        /// callback function, it will callback the current play position of player.
        /// </summary>
        /// <param name="currTime">the current play position of player </param>
        void OnCurrentPosition(UInt32 currTime);
    }
}
