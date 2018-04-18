/// @file IPlayerAdapter.cs
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
    /// type for setting player screen's display type
    /// </summary> 
    public enum PlayerDisplayType
    {
        /// <summary> Overlay surface display </summary>
        Overlay = 0,
        /// <summary>  Evas image object surface display </summary>
        Evas,
        /// <summary> No display </summary>
        None
    };

    /// <summary>
    /// type for checking stream's track type
    /// </summary> 
    public enum TrackType
    {
        /// <summary> track type audio </summary>
        Audio = 0,
        /// <summary> track type video </summary>
        Video,
        /// <summary> track type subtitle </summary>
        Subtitle,
        /// <summary> track type MAX tag </summary>
        Max
    };

    /// <summary>
    /// type for checking player's state
    /// </summary> 
    public enum PlayerState
    {
        /// <summary> Player is not created </summary>
        None,
        /// <summary> Player is created, not realize </summary>
        Null,
        /// <summary> Player is created, but not prepared </summary>
        Idle,
        /// <summary> Player is ready to play media </summary>
        Ready,
        /// <summary> Player is paused while playing media </summary>
        Paused,
        /// <summary> Player is playing media </summary>
        Playing,
        /// <summary> MAX status tag </summary>
        Max
    };


    /// <summary>
    /// Struct of audio stream info used for external demux
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct AudioStreamInfo
    {
        /// <summary> audio stream info: mime type </summary>
        public IntPtr mime;
        /// <summary> audio stream info: drm type </summary>
        public uint drmType;
        /// <summary> audio stream info: channels num </summary>
        public uint channels;
        /// <summary> audio stream info: sample rate </summary>
        public uint sampleRate;
        /// <summary> audio stream info: bit rate </summary>
        public uint bitrate;
        /// <summary> audio stream info: block align </summary>
        public uint blockAlign;
        /// <summary> audio stream info: codec extra data </summary>
        public IntPtr codecExtraData;
        /// <summary> audio stream info: codec extra data length </summary>
        public uint extraDataSize;
        /// <summary> audio stream info: version </summary>
        public uint version;
        /// <summary> audio stream info: user infomation </summary>
        public uint userInfo;
        /// <summary> audio stream info: width </summary>
        public uint width;
        /// <summary> audio stream info: depth </summary>
        public uint depth;
        /// <summary> audio stream info: endianness </summary>
        public uint endianness;
        /// <summary> audio stream info: signedness </summary>
        public bool signedness;
        /// <summary> audio stream info: buffer type </summary>
        public uint bufferType;
        /// <summary> audio stream info: whether use preset </summary>
        public bool isPreset;

        /// <summary> audio stream info: drminfo propertyType </summary>
        public IntPtr propertyType;
        /// <summary> audio stream info: drminfo propertyType length </summary>
        public int typeLen;
        /// <summary> audio stream info: drminfo propertyData </summary>
        public IntPtr propertyData;
        /// <summary> audio stream info: drminfo property_date length </summary>
        public int dataLen;
    };

    /// <summary>
    /// Struct of video stream info used for external demux
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct VideoStreamInfo
    {
        /// <summary> video stream info: mime type </summary>
        public IntPtr mime;
        /// <summary> video stream info: drm type </summary>
        public uint drmType;
        /// <summary> video stream info: framerate num </summary>
        public uint framerateNum;
        /// <summary> video stream info: framerate den </summary>
        public uint framerateDen;
        /// <summary> video stream info: width </summary>
        public uint width;
        /// <summary> video stream info: height </summary>
        public uint height;
        /// <summary> video stream info: pixelAspectRatioX </summary>
        public uint pixelAspectRatioX;
        /// <summary> video stream info: pixelAspectRatioY </summary>
        public uint pixelAspectRatioY;
        /// <summary> video stream info: codec extra data </summary>
        public IntPtr codecExtraData;
        /// <summary> video stream info: codec extra data length </summary>
        public uint extraDataSize;
        /// <summary> video stream info: version </summary>
        public uint version;
        /// <summary> video stream info: 3D video format </summary>
        public int format3D;
        /// <summary> video stream info: color depth </summary>
        public uint colorDepth;
        /// <summary> video stream info: max width </summary>
        public uint maxWidth;
        /// <summary> video stream info: max height </summary>
        public uint maxHeight;
        /// <summary> video stream info: buffer type </summary>
        public uint bufferType;
        /// <summary> video stream info: displayAspectRatioX </summary>
        public uint displayAspectRatioX;
        /// <summary> video stream info: displayAspectRatioY </summary>
        public uint displayAspectRatioY;

        /// <summary> video stream info: matroska colour info </summary>
        public IntPtr matroskaColourInfo;
        /// <summary> video stream info: frame changed </summary>
        public bool isFramerateChanged;
        /// <summary> video stream info:whether use preset </summary>
        public bool isPreset;

        /// <summary> video stream info: drminfo propertyType </summary>
        public IntPtr propertyType;
        /// <summary> video stream info: drminfo propertyType length </summary>
        public int typeLen;
        /// <summary> video stream info: drminfo propertyData </summary>
        public IntPtr propertyData;
        /// <summary> video stream info: drminfo property_date length </summary>
        public int dataLen;

    };
	
	/// <summary>
    /// Struct of es data stream's drm info used for decoder
    /// </summary>
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct EsPlayerDrmInfo
    {
	    /// <summary> es data stream's drm info : drm type </summary>
        public uint drmType;
		/// <summary> es data stream's drm info : algorithm paramete </summary>
        public uint algorithm;
		/// <summary> es data stream's drm info : media format </summary>
        public uint format;
		/// <summary> es data stream's drm info : cipher phase </summary>
        public uint phase;

        /// <summary> es data stream's drm info : KID </summary>
        public IntPtr KID;
		/// <summary> es data stream's drm info : KID length </summary>
        public uint KIDLen;
		/// <summary> es data stream's drm info : initializetion vector </summary>
        public IntPtr IV;
		/// <summary> es data stream's drm info : initializetion vector length </summary>
        public uint IVLen;
		/// <summary> es data stream's drm info : for clearkey </summary>
        public IntPtr key; 
		/// <summary> es data stream's drm info : key len </summary>
        public uint keyLen;

        /// <summary> es data stream's drm info : subData's pointer </summary>
        public IntPtr subData;
		/// <summary> es data stream's drm info : drm handle </summary>
        public uint drmHandle;
		/// <summary> es data stream's drm info : trust zone handle </summary>
        public uint tzHandle;
    };

    /// <summary>
    /// Interface which should be implemented by all player wrappers
    /// </summary>
    public interface IPlayerAdapter
    {
        /// <summary>
        /// Method of initializing player,it will create player's instance and set corresponding callbacks.
        /// </summary>
        /// <param name="isEsPlay">if the initialization is for es play </param>
        /// <returns>The result of initializing player </returns>
        bool Initialize(bool isEsPlay);

        /// <summary>
        /// Method of resetting player, it will stop player and destroy its instance
        /// </summary>
        /// <returns>The result of resetting player </returns>
        bool Reset();

        /// <summary>
        /// Method of preparing player, it will prepare player for es play mode.
        /// </summary>
        /// <returns>The result of preparing player </returns>
        bool PrepareES();

        /// <summary>
        /// Method of preparing player, it will prepare player for url play mode.
        /// </summary>
        /// <param name="url">url for the movie which need to be played by player</param>
        /// <returns>The result of preparing player </returns>
        bool PrepareURL(string url);

        /// <summary>
        /// Method of unpreparing player, it will stop the player.
        /// </summary>
        /// <returns>The result of unpreparing player </returns>
        bool Unprepare();

        /// <summary>
        /// Player controller API, it will call the player to play.
        /// </summary>
        /// <returns>The result of calling player to play </returns>
        bool Play();

        /// <summary>
        /// Player controller API, it will call the player to pause.
        /// </summary>
        /// <returns>The result of calling player to pause </returns>
        bool Pause();

        /// <summary>
        /// Player controller API, it will call the player to resume.
        /// </summary>
        /// <returns>The result of calling player to resume </returns>
        bool Resume();

        /// <summary>
        /// Player controller API, it will call the player to seek forward.
        /// </summary>
        /// <param name="absoluteTimeinMS">jump forward position for playback in milliseconds </param>
        /// <returns>The result of calling player to seek </returns>
        bool Seek(int absoluteTimeinMS);


        /// <summary>
        /// Player controller API, it will set the player's play speed.
        /// </summary>
        /// <param name="rate">play speed which is set to player </param>
        /// <returns>The result of calling player to setting speed </returns>
        bool SetPlaySpeed(float rate);

        /// <summary>
        /// Player controller API, it will set the contents duration for external feeder case.
        /// </summary>
        /// <param name="iDuration"> content duration </param>
        /// <returns> Ture if succeed </returns>
        bool SetDuration(ulong iDuration);

        /// <summary>
        /// Player controller API, it will notify player that corresponding stream is end of stream.
        /// </summary>
        /// <param name="trackType">Track Type, the type of stream </param>
        /// <returns>The result of calling player that corresponding stream is end of stream </returns>
        bool SubmitEOSPacket(TrackType trackType);


        /// <summary>
        /// Player controller API, it will send es data to player.
        /// </summary>
        /// <param name="buf">The pointer which point to the es data buffer </param>
        /// <param name="size">The size of the es data </param>
        /// <param name="pts">The pts of the es data </param>
        /// <param name="streamType">The stream type of the es data </param>
        /// <param name="drmInfo">The pointer which point to the drmInfo </param>
        /// <returns>The result of sending es data to player </returns>
        bool SubmitPacket(IntPtr buf, uint size, UInt64 pts, TrackType streamType, IntPtr drmInfo);

        /// <summary>
        /// Player controller API, it will set the player to stop.
        /// </summary>
        /// <returns>The result of calling player to stop </returns>
        bool Stop();

        /// <summary>
        /// Player controller API, it will destroy the player's instance.
        /// </summary>
        /// <returns>The result of calling player to destroy its instance </returns>
        bool DestroyHandler();


        /// <summary>
        /// Player's setter API, it will set the application id to player.
        /// </summary>
        /// <param name="applicationId">the application's id, format is string </param>
        /// <returns>The result of setting the application id to player </returns>
        bool SetApplicationID(string applicationId);

        /// <summary>
        /// Player's setter API, it will set the movie's audio streamInfo to player.
        /// </summary>
        /// <param name="audioStreamInfo">the movie's audio streamInfo </param>
        /// <returns>The result of setting the audio streamInfo to player </returns>
        bool SetAudioStreamInfo(AudioStreamInfo audioStreamInfo);

        /// <summary>
        /// Player's setter API, it will set the movie's video streamInfo to player.
        /// </summary>
        /// <param name="videoStreamInfo">the movie's video streamInfo </param>
        /// <returns>The result of setting the video streamInfo to player </returns>
        bool SetVideoStreamInfo(VideoStreamInfo videoStreamInfo);

        /// <summary>
        /// Player's setter API, it will set the movie's display options to player.
        /// </summary>
        /// <param name="type">the movie's display type </param>
        /// <param name="display">the movie's display window handler </param>
        /// <returns>The result of setting movie's display options to player </returns>
        bool SetDisplay(PlayerDisplayType type, IntPtr display);


        /// <summary>
        /// Player's setter API, it will set the movie's external subtitles path to player.
        /// </summary>
        /// <param name="filePath">the movie's external subtitle's file path </param>
        /// <param name="encoding">the movie's external subtitle's encoding </param>
        /// <returns>The result of the movie's external subtitles path to player </returns>
        bool SetExternalSubtitlesPath(string filePath, string encoding);

        /// <summary>
        /// Player's setter API, it will set the movie's subtitles delay time to player.
        /// </summary>
        /// <param name="milliSec">the movie's subtitles delay time </param>
        /// <returns>The result of setting movie's subtitles delay time to player </returns>
        bool SetSubtitlesDelay(int milliSec);

        /// <summary>
        /// Player's getter API, it will get the player's current play state.
        /// </summary>
        /// <returns>The player state of current player </returns>
        PlayerState GetPlayerState();

        /// <summary>
        /// Player's getter API, it will get the duration of the playback.
        /// </summary>
        /// <returns> The duration of the movie which is played </returns>
        ulong GetDuration();

        /// <summary>
        /// Player's API, it will register player's eventListener to player.
        /// </summary>
        /// <param name="eventListener">the player's eventListener </param>
        /// <returns>The setting result of registering eventListener </returns>
        bool RegisterPlayerEventListener(IPlayerEventListener eventListener);

        /// <summary>
        /// Player's API, it will remove player's eventListener from player.
        /// </summary>
        /// <returns>The result of removing eventListener </returns>
        bool RemoverPlayerEventListener();

    }
}
