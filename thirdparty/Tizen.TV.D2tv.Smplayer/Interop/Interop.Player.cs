/// @file Interop.Player.cs
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
using Tizen.TV.Multimedia.IPTV;

/// <summary>
/// Interop.NativeSmplayer is static class which dllimports the API method from dynamic libs and defines their C# style API. 
/// </summary>
/// <code>
/// class Sample
/// {
///     public bool PrepareES()
///     {
///          bool result = NativeSmplayer.PrepareES();
///          return result;
///     }
///     
///     public bool PrepareURL(string url)
///     {
///          bool result = NativeSmplayer.PrepareURL(url);
///          return result;
///     }
///     .....
/// }
/// </code>
internal static partial class Interop
{
    internal static partial class NativeSmplayer
    {

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SmpCurrentPositionCallback(System.UInt32 currTime, IntPtr userParam);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int SmplayerMessageCallback(int id, IntPtr param, IntPtr userParam);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SmplayerAppSrcNeedDataCallback(System.UInt32 size, IntPtr userParam);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SmplayerAppSrcDataEnoughCallback(IntPtr userParam);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool SmplayerBufferSeekDataCallback(System.UInt64 offset, IntPtr userParam);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SmpSubtitleDataCallback(IntPtr param, IntPtr msg);


        [DllImport(Libraries.Smplayer, EntryPoint = "Initialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Initialize();

        [DllImport(Libraries.Smplayer, EntryPoint = "PrepareURL", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PrepareURL(string url);

        [DllImport(Libraries.Smplayer, EntryPoint = "PrepareES", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PrepareES();

        [DllImport(Libraries.Smplayer, EntryPoint = "Play", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Play(int timePos);

        [DllImport(Libraries.Smplayer, EntryPoint = "GetPlayerState", CallingConvention = CallingConvention.Cdecl)]
        public static extern PlayerState GetPlayerState();

        [DllImport(Libraries.Smplayer, EntryPoint = "SetCurrentPositionCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetCurrentPositionCallback(SmpCurrentPositionCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetMessageCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetMessageCallback(SmplayerMessageCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetAppSrcAudioSeekCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcAudioSeekCallback(SmplayerBufferSeekDataCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetAppSrcVideoSeekCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcVideoSeekCallback(SmplayerBufferSeekDataCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetAppSrcAudioNeedDataCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcAudioNeedDataCallback(SmplayerAppSrcNeedDataCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetAppSrcVideoNeedDataCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcVideoNeedDataCallback(SmplayerAppSrcNeedDataCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetAppSrcAudioDataEnoughCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcAudioDataEnoughCallback(SmplayerAppSrcDataEnoughCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetAppSrcVideoDataEnoughCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcVideoDataEnoughCallback(SmplayerAppSrcDataEnoughCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.Smplayer, EntryPoint = "SubmitEOSPacket", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SubmitEOSPacket(TrackType streamType);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetVideoStreamInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetVideoStreamInfo(VideoStreamInfo videoStreamInfo);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetAudioStreamInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAudioStreamInfo(AudioStreamInfo audioStreamInfo);

        [DllImport(Libraries.Smplayer, EntryPoint = "SubmitPacket", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool SubmitPacket(IntPtr buf, uint size, System.UInt64 pts, TrackType streamType, IntPtr drmInfo);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetAppInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppInfo(string desktopId, string appId, string widgetId);

        [DllImport(Libraries.Smplayer, EntryPoint = "Pause", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Pause();

        [DllImport(Libraries.Smplayer, EntryPoint = "Resume", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Resume();

        [DllImport(Libraries.Smplayer, EntryPoint = "JumpForward", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool JumpForward(uint offset);

        [DllImport(Libraries.Smplayer, EntryPoint = "JumpBackward", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool JumpBackward(uint offset);

        [DllImport(Libraries.Smplayer, EntryPoint = "Seek", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Seek(int absoluteTimeinMS);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetPlaySpeed", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetPlaySpeed(float speed);

        [DllImport(Libraries.Smplayer, EntryPoint = "Stop", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Stop();

        [DllImport(Libraries.Smplayer, EntryPoint = "DestroyHandler", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool DestroyHandler();

        [DllImport(Libraries.Smplayer, EntryPoint = "SetDisplayWin", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetDisplayWin(int winId, int x, int y, int width, int height);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetDisplay", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetDisplay(PlayerDisplayType type, IntPtr display);

        [DllImport(Libraries.Smplayer, EntryPoint = "StartSubtitle", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool StartSubtitle(string filePath, string encoding);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetSubtitlesDelay", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetSubtitlesDelay(int milliSec);

        [DllImport(Libraries.Smplayer, EntryPoint = "SetAppSrcDuration", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetDuration(ulong iDuration);

        [DllImport(Libraries.Smplayer, EntryPoint = "GetDuration", CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong GetDuration();

    }

}
