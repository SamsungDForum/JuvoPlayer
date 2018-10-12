/// @file ESHandlePacket.cs 
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

using System.Runtime.InteropServices;

namespace Tizen.TV.Multimedia
{
    /// <summary>
    /// Represents one of es packet which includes handle of data inside the trust zone, contains stream type, handle for encrypted es data packet, handle size, pts and duration.
    /// </summary>
    /// <code>
    /// public void Apps()
    /// {
    ///     var player = new ESPlayer();
    ///     // your logic
    ///     player.SubmitPacket(GetPacket());
    ///     // your logic
    /// }
    /// 
    /// private ESHandlePacket GetPacket(StreamType streamType)
    /// {
    ///     // your logic
    ///     var yourHandlePacket = new ESHandlePacket
    ///     {
    ///         type = streamType,
    ///         handle = yourHandle,
    ///         handleSize = yourHandleSize,
    ///         pts = yourPts,
    ///         duration = yourDuration,
    ///     };
    ///     
    ///     return yourPacket;
    /// }
    /// </code>
    /// <see cref="ESPlayer.SubmitPacket(ESHandlePacket)"/>
    [StructLayout(LayoutKind.Sequential)]
    public struct ESHandlePacket
    {
        /// <summary>
        /// Stream type of ESPacket
        /// </summary>
        public StreamType type;
        /// <summary>
        /// Handle for encrypted es packet inside the trust zone
        /// </summary>
        public uint handle;
        /// <summary>
        /// Handle size for <see cref="ESHandlePacket.handle"/>
        /// </summary>
        public uint handleSize;
        /// <summary>
        /// PTS(Presentation Time Stamp) of es packet
        /// </summary>
        public ulong pts;
        /// <summary>
        /// DUration of es packet
        /// </summary>
        public ulong duration;
    }
}
