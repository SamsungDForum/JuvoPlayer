/// @file Interop.ESPlayer.cs 
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
using Tizen.TV.Multimedia;

internal static partial class Interop
{
    internal static class NativeESPlusPlayer
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnError(ErrorType errCode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnBufferStatus(StreamType type, BufferStatus status);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnResourceConflicted();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnEos();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnReadyToPrepare(StreamType type);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnPrepareAsyncDone(bool result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnSeekDone();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnReadyToSeek(StreamType type, ulong offset);


        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_create")]
        internal static extern IntPtr Create();

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_open")]
        internal static extern bool Open(IntPtr player);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_close")]
        internal static extern bool Close(IntPtr player);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_prepare_async")]
        internal static extern bool PrepareAsync(IntPtr player);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_start")]
        internal static extern bool Start(IntPtr player);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_stop")]
        internal static extern bool Stop(IntPtr player);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_pause")]
        internal static extern bool Pause(IntPtr player);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_resume")]
        internal static extern bool Resume(IntPtr player);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_seek")]
        internal static extern bool Seek(IntPtr player, ulong ms);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_display")]
        internal static extern bool SetDisplay(IntPtr player, DisplayType type, IntPtr window);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_ecore_display")]
        internal static extern bool SetDisplay(IntPtr player, DisplayType type, IntPtr window, int x, int y, int w, int h);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_display_mode")]
        internal static extern bool SetDisplayMode(IntPtr player, DisplayMode mode);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_display_mode")]
        internal static extern bool SetDisplayMode(IntPtr player, int mode);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_display_roi")]
        internal static extern bool SetDisplayRoi(IntPtr player, int x, int y, int width, int height);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_display_visible")]
        internal static extern bool SetDisplayVisible(IntPtr player, bool visible);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_audio_mute")]
        internal static extern bool SetAudioMute(IntPtr player, bool mute);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_get_state")]
        internal static extern ESPlayerState GetState(IntPtr player);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_submit_packet")]
        internal static extern SubmitStatus SubmitPacket(IntPtr player, IntPtr packet);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_submit_trust_zone_packet")]
        internal static extern SubmitStatus SubmitTrustZonePacket(IntPtr player, IntPtr packet);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_submit_eos_packet")]
        internal static extern SubmitStatus SubmitEOSPacket(IntPtr player, StreamType type);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_tz_use")]
        internal static extern bool SetTrustZoneUse(IntPtr player, bool isUsing);

        /*
        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_drm")]
        internal static extern bool SetDrm(IntPtr player, DrmType type);
        */

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_add_audio_stream_info")]
        internal static extern bool AddAudioStreamInfo(IntPtr player, IntPtr stream);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_add_video_stream_info")]
        internal static extern bool AddVideoStreamInfo(IntPtr player, IntPtr stream);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_get_playing_time")]
        internal static extern bool GetPlayingTime(IntPtr player, out ulong timeInMilliseconds);


        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_error_cb")]
        internal static extern void SetOnErrorCallback(IntPtr player, IntPtr errorCallback);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_buffer_status_cb")]
        internal static extern void SetOnBufferStatusCallback(IntPtr player, IntPtr bufferStatusCallback);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_resource_conflicted_cb")]
        internal static extern void SetOnResourceConflictedCallback(IntPtr player, IntPtr resourceConflictedCallback);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_eos_cb")]
        internal static extern void SetOnEosCallback(IntPtr player, IntPtr eosCallback);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_ready_to_prepare_cb")]
        internal static extern void SetOnReadyToPrepareCallback(IntPtr player, IntPtr readyToPrepareCallback);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_prepare_async_done_cb")]
        internal static extern void SetOnPrepareAsyncDoneCallback(IntPtr player, IntPtr prepareAsyncDoneCallback);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_seek_done_cb")]
        internal static extern void SetOnSeekDoneCallback(IntPtr player, IntPtr seekDoneCallback);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_ready_to_seek_cb")]
        internal static extern void SetOnReadyToSeekCallback(IntPtr player, IntPtr readyToSeekCallback);
    }
}