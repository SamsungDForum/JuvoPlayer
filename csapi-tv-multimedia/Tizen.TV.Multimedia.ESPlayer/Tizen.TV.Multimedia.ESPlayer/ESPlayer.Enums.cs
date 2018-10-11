/// @file ESPlayer.Enums.cs 
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

using Tizen.Internals.Errors;

namespace Tizen.TV.Multimedia
{
    /// <summary>
    /// Enumerator for error type from <see cref="ESPlayer"/>
    /// </summary>
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
    /// <see cref="ESPlayer.ErrorOccurred"/>
    public enum ErrorType
    {
        /// <summary>
        /// Successful
        /// </summary>
        None = ESPlayerErrorCode.None,
        /// <summary>
        /// Out of memory
        /// </summary>
        OutOfMemory = ESPlayerErrorCode.OutOfMemory,
        /// <summary>
        /// Seek operation failure
        /// </summary>
        SeekFailed = ESPlayerErrorCode.PlusplayerErrorClass | 0x01,
        /// <summary>
        /// Invalid esplayer state
        /// </summary>
        InvalidState = ESPlayerErrorCode.PlusplayerErrorClass | 0x02,
        /// <summary>
        /// Resource limit
        /// </summary>
        ResourceLimit = ESPlayerErrorCode.PlusplayerErrorClass | 0x0c,
        /// <summary>
        /// Permission denied
        /// </summary>
        PermissionDenied = ESPlayerErrorCode.PermissionDenied,
        /// <summary>
        /// No buffer space available
        /// </summary>
        BufferSpace = ESPlayerErrorCode.BufferSpace,
        /// <summary>
        /// Not supported audio codec but video can be played
        /// </summary>
        NotSupportedAudioCodec = ESPlayerErrorCode.PlusplayerErrorClass | 0x0e,
        /// <summary>
        /// Not supported video codec but audio can be played
        /// </summary>
        NotSupportedVideoCodec = ESPlayerErrorCode.PlusplayerErrorClass | 0x0f,
    }

    /// <summary>
    /// Enumerator for stream type of es stream
    /// </summary>
    public enum StreamType
    {
        /// <summary>
        /// Audio
        /// </summary>
        Audio = 0,
        /// <summary>
        /// Video
        /// </summary>
        Video = 1
    }

    /// <summary>
    /// Enumerator for buffer status whether empty or full.
    /// </summary>
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
    /// <see cref="ESPlayer.BufferStatusChanged"/>
    public enum BufferStatus
    {
        /// <summary>
        /// Status of buffer queue in <see cref="ESPlayer"/> is underrun.
        /// </summary>
        /// <remarks>
        /// When status is <see cref="BufferStatus.Underrun"/>, application should push es packet sufficiently.
        /// </remarks>
        Underrun,
        /// <summary>
        /// Status of buffer queue in <see cref="ESPlayer"/> is overrun. 
        /// </summary>
        /// <remarks>
        /// When status is <see cref="BufferStatus.Overrun"/>, application should stop pushing es packet.
        /// </remarks>
        Overrun
    }

    /// <summary>
    /// Enumerator for es packet submit status 
    /// </summary>
    /// <code>
    /// public void Apps()
    /// {
    ///     var player = new ESPlayer();
    ///     // your logic
    ///     var submitStatus = player.SubmitPacket(yourPacket);
    ///     
    ///     switch(submitStatus)
    ///     {
    ///         case SubmitStatus.NotPrepared :
    ///         // your logic
    ///     }
    ///     // your logic
    /// }
    /// </code>
    /// <see cref="ESPlayer.SubmitPacket(ESPacket)"/>
    /// <see cref="ESPlayer.SubmitPacket(ESHandlePacket)"/>
    /// <see cref="ESPlayer.SubmitEosPacket(StreamType)"/>
    public enum SubmitStatus
    {
        /// <summary>
        /// Not prepared to get packet
        /// </summary>
        NotPrepared,
        /// <summary>
        /// Invalid packet
        /// </summary>
        InvalidPacket,
        /// <summary>
        /// Out of memory on device
        /// </summary>
        OutOfMemory,
        /// <summary>
        /// Buffer already full
        /// </summary>
        Full,
        /// <summary>
        /// Submit succeeded
        /// </summary>
        Success
    }

    /// <summary>
    /// Enumerator for state of <see cref="ESPlayer"/>
    /// </summary>
    /// <code>
    /// public void Apps()
    /// {
    ///     var player = new ESPlayer();
    ///     // your logic
    ///     ESPlayerState state = player.GetState();
    ///     
    ///     switch(state)
    ///     {
    ///         case ESPlayerState.None :
    ///         // your logic
    ///     }
    ///     // your logic
    /// }
    /// </code>
    /// <see cref="ESPlayer.Open"/>
    /// <see cref="ESPlayer.PrepareAsync(System.Action{StreamType})"/>
    /// <see cref="ESPlayer.Start"/>
    /// <see cref="ESPlayer.Resume"/>
    /// <see cref="ESPlayer.Pause"/>
    /// <see cref="ESPlayer.Stop"/>
    /// <see cref="ESPlayer.GetState"/>
    public enum ESPlayerState
    {
        /// <summary>
        /// <see cref="ESPlayer"/> is created, but not opened.
        /// </summary>
        None,
        /// <summary>
        /// <see cref="ESPlayer"/> is opened, but not prepared or player is stopped.
        /// </summary>
        Idle,
        /// <summary>
        /// <see cref="ESPlayer"/> is ready to play.
        /// </summary>
        Ready,
        /// <summary>
        /// <see cref="ESPlayer"/> is playing media.
        /// </summary>
        Playing,
        /// <summary>
        /// <see cref="ESPlayer"/> is paused while playing media.
        /// </summary>
        Paused
    }

    /// <summary>
    /// Enumerator for audio mime type
    /// </summary>
    /// <code>
    /// public void Apps()
    /// {
    ///     // your logic
    ///     var audioMimeType = AudioMimeType.Ac3;
    ///     // your logic
    /// }
    /// </code>
    public enum AudioMimeType
    {
        /// <summary>
        /// AAC
        /// </summary>
        Aac = 1,
        /// <summary>
        /// MP2
        /// </summary>
        Mp2,
        /// <summary>
        /// MP3
        /// </summary>
        Mp3,
        /// <summary>
        /// AC3
        /// </summary>
        Ac3,
        /// <summary>
        /// EAC3
        /// </summary>
        Eac3,
        /// <summary>
        /// VORBIS
        /// </summary>
        Vorbis,
        /// <summary>
        /// OPUS
        /// </summary>
        Opus = 8,
        /// <summary>
        /// PCM_S16LE
        /// </summary>
        PcmS16le,
        /// <summary>
        /// PCM_S16BE
        /// </summary>
        PcmS16be,
        /// <summary>
        /// PCM_U16LE
        /// </summary>
        PcmU16le,
        /// <summary>
        /// PCM_U16BE
        /// </summary>
        PcmU16be,
        /// <summary>
        /// PCM_S24LE
        /// </summary>
        PcmS24le,
        /// <summary>
        /// PCM_S24BE
        /// </summary>
        PcmS24be,
        /// <summary>
        /// PCM_U24LE
        /// </summary>
        PcmU24le,
        /// <summary>
        /// PCM_U24BE
        /// </summary>
        PcmU24be,
        /// <summary>
        /// PCM_S32LE
        /// </summary>
        PcmS32le,
        /// <summary>
        /// PCM_S32BE
        /// </summary>
        PcmS32be,
        /// <summary>
        /// PCM_U32LE
        /// </summary>
        PcmU32le,
        /// <summary>
        /// PCM_U32BE
        /// </summary>
        PcmU32be
    }

    /// <summary>
    /// Enumerator for video mime type
    /// </summary>
    /// <code>
    /// public void Apps()
    /// {
    ///     // your logic
    ///     var videoMimeType = AudioMimeType.Hevc;
    ///     // your logic
    /// }
    /// </code>
    public enum VideoMimeType
    {
        /// <summary>
        /// H.263
        /// </summary>
        H263 = 1,
        /// <summary>
        /// H.254
        /// </summary>
        H264,
        /// <summary>
        /// HEVC
        /// </summary>
        Hevc,
        /// <summary>
        /// MPEG-1
        /// </summary>
        Mpeg1,
        /// <summary>
        /// MPEG-2
        /// </summary>
        Mpeg2,
        /// <summary>
        /// MPEG-4
        /// </summary>
        Mpeg4,
        /// <summary>
        /// VP8
        /// </summary>
        Vp8,
        /// <summary>
        /// VP9
        /// </summary>
        Vp9,
        /// <summary>
        /// WMV3
        /// </summary>
        Wmv3
    }

    /// <summary>
    /// Enumerator for display mode
    /// </summary>
    public enum DisplayMode
    {
        /// <summary>
        /// Letter box
        /// </summary>
        LetterBox,
        /// <summary>
        /// Origin size
        /// </summary>
        OriginSize,
        /// <summary>
        /// Full screen
        /// </summary>
        FullScreen,
        /// <summary>
        /// Cropped full screen
        /// </summary>
        CroppedFull,
        /// <summary>
        /// Origin size (if surface size is larger than video size(width/height)) or Letter box (if video size(width/height) is larger than surface size) 
        /// </summary>
        OriginOrLetter,
    }

    /// <summary>
    /// Enumerator for display type
    /// </summary>
    public enum DisplayType
    {
        /// <summary>
        /// This disposes off buffers
        /// </summary>
        None,
        /// <summary>
        /// Overlay surface display
        /// </summary>
        Overlay,
        /// <summary>
        /// Evas image object surface display
        /// </summary>
        Evas
    }

    /*
    public enum DrmType
    {
        None,
        Playready,
        Marlin,
        Verimatrix,
        WidevineCdm,
        Max
    }
    */

    internal enum ESPlayerErrorCode
    {
        ErrorPlayer = -0x01940000,
        None = ErrorCode.None,
        OutOfMemory = ErrorCode.OutOfMemory,
        PermissionDenied = ErrorCode.PermissionDenied,
        PlusplayerErrorClass = ErrorPlayer | 0x20,
        PlusplayerCustomErrorClass = ErrorPlayer | 0x1000,
        BufferSpace = ErrorCode.BufferSpace,
    }
}