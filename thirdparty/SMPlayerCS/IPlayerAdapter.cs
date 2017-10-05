using System;
using System.Runtime.InteropServices;

namespace CSPlayer
{
    public enum TrackType_Samsung
    {
        TRACK_TYPE_AUDIO = 0,              /**< track type audio */
        TRACK_TYPE_VIDEO,                  /**< track type video */
        TRACK_TYPE_SUBTITLE,               /**< track type subtitle */
        TRACK_TYPE_MAX                     /**< MAX tag */
    };

    public enum PlayerState_Samsung
    {
        PLAYER_STATE_NONE,                  /**< Player is not created */
        PLAYER_STATE_NULL,                  /**< Player is created, not realize */
        PLAYER_STATE_IDLE,                  /**< Player is created, but not prepared */
        PLAYER_STATE_READY,                 /**< Player is ready to play media */
        PLAYER_STATE_PAUSED,                /**< Player is paused while playing media */
        PLAYER_STATE_PLAYING,               /**< Player is playing media */
        PLAYER_STATE_MAX                    /**< MAX tag */
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct AudioStreamInfo_Samsung
    {
        public IntPtr mime;                      /**< audio stream info: mime type */             //const char *  I use IntPtr need to check if OK
        public uint drm_type;                 /**< audio stream info: drm type */
        public uint channels;                 /**< audio stream info: channels */
        public uint sample_rate;              /**< audio stream info: sample rate */
        public uint bit_rate;                 /**< audio stream info: bit rate */
        public uint block_align;              /**< audio stream info: block align */
        public IntPtr codec_extradata;        /**< audio stream info: codec extra data */  //unsigned char * i use IntPtr need to check if OK
        public uint extradata_size;           /**< audio stream info: codec extra data length */
        public uint version;                  /**< audio stream info: version */
        public uint user_info;                /**< audio stream info: user infomation */
        public uint width;                    /**< audio stream info: width */
        public uint depth;                    /**< audio stream info: depth */
        public uint endianness;               /**< audio stream info: endianness */
        public bool signedness;                       /**< audio stream info: signedness */
        public uint buffer_type;              /**< audio stream info: buffer type */
        public bool is_preset;                        /**< audio stream info: whether use preset */

        // DRM Info
        public IntPtr property_type;                   /**< video stream info: drminfo property_type */
        public int type_len;                          /**< video stream info: drminfo property_type length */
        public IntPtr property_data;                   /**< video stream info: drminfo property_data */
        public int data_len;                          /**< video stream info: drminfo property_date length */
    };

    /**
     * @brief struct of video stream info used for external demux
     */
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct VideoStreamInfo_Samsung
    {
        public IntPtr mime;                    /**< video stream info: mime type */
        public uint drm_type;                  /**< video stream info: drm type */
        public uint framerate_num;         /**< video stream info: framerate num */
        public uint framerate_den;         /**< video stream info: framerate den */
        public uint width;                 /**< video stream info: width */
        public uint height;                /**< video stream info: height */
        public uint pixelAspectRatioX;     /**< video stream info: pixelAspectRatioX */
        public uint pixelAspectRatioY;     /**< video stream info: pixelAspectRatioY */
        public IntPtr codec_extradata;     /**< video stream info: codec extra data */
        public uint extradata_size;        /**< video stream info: codec extra data length */
        public uint version;                   /**< video stream info: version */
        public int format3D;                           /**< video stream info: 3D video format */
        public uint colordepth;            /**< video stream info: color depth */
        public uint max_width;             /**< video stream info: max width */
        public uint max_height;            /**< video stream info: max height */
        public uint buffer_type;               /**< video stream info: buffer type */
        public uint displayAspectRatioX;       /**< video stream info: displayAspectRatioX */
        public uint displayAspectRatioY;       /**< video stream info: displayAspectRatioY */
        public IntPtr matroska_colour_info;    /**< video stream info: matroska colour info */
        public bool is_framerate_changed;              /**< video stream info: frame changed */
        public bool is_preset;                      /** < video stream info:whether use preset */

        // DRM Info
        public IntPtr property_type;                   /**< video stream info: drminfo property_type */
        public int type_len;                           /**< video stream info: drminfo property_type length */
        public IntPtr property_data;                   /**< video stream info: drminfo property_data */
        public int data_len;

    };
    public interface IPlayerAdapter
    {
        // Player preparing
        bool Initialize();

        bool Reset();

        bool PrepareES();

        bool PrepareURL(string url);

        bool Unprepare();


        // Player controllers
        bool Play();

        bool Pause();

        bool Resume();

        //For API Seek ,we need to check if need set seek_completed_cb for each seek operation Seek(int iAbsoluteTimeinMS,cb_function seek_completed_cb)
        bool Seek(int iAbsoluteTimeinMS);   //10 seconds = 10 * 1000 

        //bool SelectTrack(TrackType_Samsung streamType, uint track_index);            //To be implement on Pepper, and we also need to confirm if we need such API

        bool SetPlaybackRate(float rate);

        bool SubmitEOSPacket(TrackType_Samsung track_type);

        bool SubmitPacket(IntPtr pBuf, uint iSize, System.UInt64 iPTS, TrackType_Samsung eStreamType, IntPtr drm_info);
        //bool SubmitPacket(const ppapi::ESPacket* packet,PP_ElementaryStream_Type_Samsung track_type) override;
        //bool SubmitEncryptedPacket(
        //         const ppapi::ESPacket* packet,
        //       PepperTizenTrustZoneHandle* handle,
        //       PP_ElementaryStream_Type_Samsung track_type) override;
        //For SubmitPacket, we need to confirm with CP side, how they send data.

        bool Stop();
        //void Stop(const base::Callback<void(int32_t)>&) override;  If stop need such callback

        bool DestroyHandler();


        // Player setters
        bool SetApplicationID(string application_id);

        bool SetDuration(System.UInt32 iDuration);      //C++ API use unsigned long, need to check if this type System.UInt32 is OK

        bool SetAudioStreamInfo(AudioStreamInfo_Samsung pAudioStreamInfo);

        bool SetVideoStreamInfo(VideoStreamInfo_Samsung pVideoStreamInfo);


        bool SetDisplay(int winId, int x, int y, int width, int height);    //Need to confirm with cp side, this winId is wayland window id, need to create by App
                                                                            //For Display below APIs need to confirm with cp / app side if they need
                                                                            //int32_t SetDisplay(void* display, bool is_windowless) override;
                                                                            //int32_t SetDisplayRect(const PP_Rect& display_rect,const PP_FloatRect& crop_ratio_rect) override;
                                                                            //int32_t SetDisplayMode(PP_MediaPlayerDisplayMode display_mode) override;
                                                                            //bool IsDisplayModeSupported(PP_MediaPlayerDisplayMode display_mode) override;                                                                     



        bool SetExternalSubtitlesPath(string file_path, string encoding);

        bool SetSubtitlesDelay(int iMilliSec);

        //The following two need to confirm which stream Property cp or app side need to set and get.
        //int32_t SetStreamingProperty(PP_StreamingProperty property,const std::string& data) override;
        //int32_t GetStreamingProperty(PP_StreamingProperty property, std::string* property_value) override;


        // Player getters

        //Need to confirm with cp & app side if they need below APIs, and vector can not be used on C#, so which data type they want to use.
        //int32_t GetAudioTracksList(std::vector<PP_AudioTrackInfo>* track_list) override;
        //int32_t GetTextTracksList(std::vector<PP_TextTrackInfo>* track_list) override;
        //int32_t GetVideoTracksList(std::vector<PP_VideoTrackInfo>* track_list) override;
        //string GetAvailableBitrates();

        uint GetCurrentTime();

        uint GetCurrentTrack(TrackType_Samsung stream_type);

        System.UInt32 GetDuration();

        PlayerState_Samsung GetPlayerState();


        bool RegisterPlayerEventListener(IPlayerEventListener eventListener);
        bool RemoverPlayerEventListener();

        //Added by me to check if one PlayerEventListener is better than below APIs for each event
        //int32_t SetErrorCallback(const base::Callback<void(PP_MediaPlayerError)>& callback) override;
        //void RegisterMediaCallbacks(PP_ElementaryStream_Type_Samsung type) override;
        // Listeners registering
        //virtual void SetListener(
        //  PP_ElementaryStream_Type_Samsung type,
        // base::WeakPtr<ElementaryStreamListenerPrivate> listener) = 0;
        //virtual void RemoveListener(PP_ElementaryStream_Type_Samsung type,
        //      ElementaryStreamListenerPrivate* listener) = 0;
        //virtual void SetMediaEventsListener(MediaEventsListenerPrivate* listener) = 0;
        //virtual void SetSubtitleListener(SubtitleListenerPrivate* listener) = 0;
        //virtual void SetBufferingListener(BufferingListenerPrivate* listener) = 0;

        //Below printLog() is not API, just used by me for internal test
        void printLog(string log);

    }
}
