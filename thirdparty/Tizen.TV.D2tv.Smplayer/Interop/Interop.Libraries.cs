/// @file Interop.Libraries.cs
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

/// <summary>
/// Interop.Libraries class which stores the dynamic libs' path and name 
/// </summary>
/// <code>
/// class Sample
/// {
///     [DllImport(Libraries.Smplayer, EntryPoint = "Initialize", CallingConvention = CallingConvention.Cdecl)]
///     public static extern bool Initialize();
///     
///     [DllImport(Libraries.Smplayer, EntryPoint = "PrepareURL", CallingConvention = CallingConvention.Cdecl)]
///     public static extern bool PrepareURL(string url);
/// }
/// </code>
internal static partial class Interop
{
    internal static partial class Libraries
    {
        public const string Smplayer = "/usr/lib/libcapi-sm-player-tv.so";
    }
}
