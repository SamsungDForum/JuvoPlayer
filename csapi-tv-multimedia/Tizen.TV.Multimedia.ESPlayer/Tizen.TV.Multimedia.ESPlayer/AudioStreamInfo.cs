/// @file AudioStreamInfo.cs 
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

namespace Tizen.TV.Multimedia
{
    /// <summary>
    /// Represents stream information for audio stream which is demuxed from application, contains codec data, mime type, bitrate, channels, and sampleRate.
    /// </summary>
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
    ///     var yourStreamInfo = new AudioStreamInfo
    ///     {
    ///         codecData = yourCodecData,
    ///         mimeType = AudioMimeType.Ac3,
    ///         bitrate = 19200,
    ///         channels = 2,
    ///         sampleRate = 48000,
    ///     };
    ///     
    ///     return yourStreamInfo;
    /// }
    /// </code>
    public struct AudioStreamInfo
    {
        /// <summary>
        /// Codec data for the associated audio stream
        /// </summary>
        public byte[] codecData;
        /// <summary>
        /// Mime type for the associated audio stream
        /// </summary>
        public AudioMimeType mimeType;
        /// <summary>
        /// Bitrate for the associated audio stream
        /// </summary>
        public int bitrate;
        /// <summary>
        /// Channels for the associated audio stream
        /// </summary>
        public int channels;
        /// <summary>
        /// Sample rate for the associated audio stream
        /// </summary>
        public int sampleRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeAudioStreamInfo
    {
        public IntPtr codecData;
        public int codecDataLength;
        public AudioMimeType mimeType;
        public int bitrate;
        public int channels;
        public int sampleRate;
    }
}
