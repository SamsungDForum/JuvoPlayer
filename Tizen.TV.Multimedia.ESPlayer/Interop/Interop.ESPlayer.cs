using System;
using System.Runtime.InteropServices;
using Tizen.TV.Multimedia.ESPlayer;

internal static partial class Interop
{
    internal static class NativeESPlusPlayer
    {
        /*
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void AudioIsDualmonoCallback(bool is_dualmono, IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void MissedPluginTypeCallback(PlayerMissedPluginType type, string message, IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SeekCompletedCallback(IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SpectrumAnalysisCallback(IntPtr userData, IntPtr bands, int size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OtherEventCallback(int event_type, IntPtr message, IntPtr userData);
        */

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnError(ErrorType errCode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnBufferStatus(StreamType type, BufferStatus status);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnResourceConflicted();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnEos();

        /// for test
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnReadyToPrepare(StreamType type);
        /// /// ///

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void OnPrepareDone(bool result);

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

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_display_roi")]
        internal static extern bool SetDisplayRoi(IntPtr player, int x, int y, int width, int height);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_display_visible")]
        internal static extern bool SetDisplayVisible(IntPtr player, bool visible);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_set_audio_mute")]
        internal static extern bool SetAudioMute(IntPtr player, bool mute);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_get_state")]
        internal static extern EsState GetState(IntPtr player);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_submit_packet")]
        internal static extern SubmitStatus SubmitPacket(IntPtr player, IntPtr packet);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_submit_trust_zone_packet")]
        internal static extern SubmitStatus SubmitTrustZonePacket(IntPtr player, IntPtr packet);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_submit_eos_packet")]
        internal static extern SubmitStatus SubmitEOSPacket(IntPtr player, StreamType type);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_add_audio_stream_info")]
        internal static extern bool AddAudioStreamInfo(IntPtr player, IntPtr stream);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_add_video_stream_info")]
        internal static extern bool AddVideoStreamInfo(IntPtr player, IntPtr stream);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_get_playing_time")]
        internal static extern bool GetPlayingTime(IntPtr player, out ulong timeInMilliseconds);


        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_register_on_error_listener")]
        internal static extern void RegisterOnErrorListener(IntPtr player, IntPtr onError);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_register_on_buffer_status_listener")]
        internal static extern void RegisteronBufferStatusListener(IntPtr player, IntPtr onBUfferStatus);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_register_on_resource_conflicted")]
        internal static extern void RegisterOnResourceConflicted(IntPtr player, IntPtr onResourceConflicted);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_register_on_eos_listener")]
        internal static extern void RegisterOnEosListener(IntPtr player, IntPtr onEos);

        /// temp ///
        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_register_on_ready_to_prepare_listener")]
        internal static extern void RegisterOnReadyToPrepareListener(IntPtr player, IntPtr onReadyToPrepare);
        ////////////

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_register_on_prepare_async_done_listener")]
        internal static extern void RegisterOnPrepareAsyncDoneListener(IntPtr player, IntPtr onPrepareAsyncDone);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_register_on_seek_done_listener")]
        internal static extern void RegisterOnSeekDoneListener(IntPtr player, IntPtr onSeekDone);

        [DllImport(Libraries.ESPlayer, EntryPoint = "esplusplayer_register_on_ready_to_seek_listener")]
        internal static extern void RegisterOnReadyToSeekListener(IntPtr player, IntPtr onReadyToSeek);
    }
}