/*
 * Copyright (c) 2016 Samsung Electronics Co., Ltd All Rights Reserved
 *
 * Licensed under the Apache License, Version 2.0 (the License);
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an AS IS BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Runtime.InteropServices;
using Tizen.TV.Smplayer;

internal static partial class Interop
{
    internal static class NativeSMPlayer
    {

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SmpCurrentPositionCallback(System.UInt32 lCurrTime, IntPtr userParam);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int SmPlayerMessageCallback(int id, IntPtr pParam, IntPtr pUserParam);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SmPlayerAppSrcNeedDataCallback(System.UInt32 size, IntPtr userParam);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SmPlayerAppSrcDataEnoughCallback(IntPtr userParam);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool SmPlayerBufferSeekDataCallback(System.UInt64 offset, IntPtr userParam);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SmpSubtitleDataCallback(IntPtr param, IntPtr msg);     //SmpMessageParamType* I use IntPtr need to check if OK


        [DllImport(Libraries.SMPlayer, EntryPoint = "Initialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Initialize();

        [DllImport(Libraries.SMPlayer, EntryPoint = "PrepareURL", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PrepareURL(string url);

        [DllImport(Libraries.SMPlayer, EntryPoint = "PrepareES", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PrepareES();

        [DllImport(Libraries.SMPlayer, EntryPoint = "Play", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Play(int timePos);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetCurrentPositionCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetCurrentPositionCallback(SmpCurrentPositionCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetMessageCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetMessageCallback(SmPlayerMessageCallback cbFunction, IntPtr pUserParam);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetAppSrcAudioSeekCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcAudioSeekCallback(SmPlayerBufferSeekDataCallback cbFunction, IntPtr pUserParam);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetAppSrcVideoSeekCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcVideoSeekCallback(SmPlayerBufferSeekDataCallback cbFunction, IntPtr pUserParam);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetAppSrcAudioNeedDataCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcAudioNeedDataCallback(SmPlayerAppSrcNeedDataCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetAppSrcVideoNeedDataCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcVideoNeedDataCallback(SmPlayerAppSrcNeedDataCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetAppSrcAudioDataEnoughCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcAudioDataEnoughCallback(SmPlayerAppSrcDataEnoughCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetAppSrcVideoDataEnoughCallback", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcVideoDataEnoughCallback(SmPlayerAppSrcDataEnoughCallback cbFunction, IntPtr userParam);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SubmitEOSPacket", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SubmitEOSPacket(TrackType streamType);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetAppSrcDuration", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppSrcDuration(System.UInt32 duration);      //API use unsigned long, need to check if this type System.UInt32 is OK

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetVideoStreamInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetVideoStreamInfo(VideoStreamInfo videoStreamInfo);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetAudioStreamInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAudioStreamInfo(AudioStreamInfo audioStreamInfo);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SubmitPacket", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern bool SubmitPacket(IntPtr  buf, uint size, System.UInt64 PTS, TrackType streamType, IntPtr drmInfo);
        //unsigned char *pBuf I use IntPtr need to check if OK

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetAppInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetAppInfo(string desktopId, string appId, string widgetId);

        [DllImport(Libraries.SMPlayer, EntryPoint = "Pause", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Pause();

        [DllImport(Libraries.SMPlayer, EntryPoint = "Resume", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Resume();

        [DllImport(Libraries.SMPlayer, EntryPoint = "JumpForward", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool JumpForward(uint offset);

        [DllImport(Libraries.SMPlayer, EntryPoint = "JumpBackward", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool JumpBackward(uint offset);

        [DllImport(Libraries.SMPlayer, EntryPoint = "Seek", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Seek(int absoluteTimeinMS);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetPlaySpeed", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetPlaySpeed(float speed);

        [DllImport(Libraries.SMPlayer, EntryPoint = "Stop", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool Stop();

        [DllImport(Libraries.SMPlayer, EntryPoint = "DestroyHandler", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool DestroyHandler();

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetDisplayWin", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetDisplayWin(int winId, int x, int y, int width, int height);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetDisplay", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetDisplay(PlayerDisplayType type, IntPtr display);

        [DllImport(Libraries.SMPlayer, EntryPoint = "StartSubtitle", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool StartSubtitle(string filePath, SmpSubtitleDataCallback cbFunction);

        [DllImport(Libraries.SMPlayer, EntryPoint = "SetSubtitleSync", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SetSubtitleSync(int milliSec);


        [DllImport(Libraries.SMPlayer, EntryPoint = "testCallbackprint", CallingConvention = CallingConvention.Cdecl)]
        public static extern void testCallbackprint(string msg);

    }
    /*
    internal class SMPlayerHandle : SafeHandle
    {
        protected SMPlayerHandle() : base(IntPtr.Zero, true)
        {
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            var ret = NativeSMPlayer.DestroyHandler();
            if (ret != PlayerErrorCode.None)
            {
                Log.Debug(GetType().FullName, $"Failed to release native {GetType().Name}");
                return false;
            }

            return true;
        }
    }
    */
}
