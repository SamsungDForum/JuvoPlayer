using System;
using System.Runtime.InteropServices;

namespace Tizen.TV.Smplayer
{
    public enum PlayerDisplayType
    {
        Overlay = 0,    /**< Overlay surface display */
        Evas,           /**< Evas image object surface display */
        None,           /**< This disposes off buffers */
    };

    public enum TrackType
    {
        Audio = 0,              /**< track type audio */
        Video,                  /**< track type video */
        Subtitle,               /**< track type subtitle */
        Max                     /**< MAX tag */
    };

    public enum PlayerState
    {
        None,                  /**< Player is not created */
        Null,                  /**< Player is created, not realize */
        Idle,                  /**< Player is created, but not prepared */
        Ready,                 /**< Player is ready to play media */
        Paused,                /**< Player is paused while playing media */
        Playing,               /**< Player is playing media */
        Max                    /**< MAX tag */
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct AudioStreamInfo
    {
        public IntPtr mime;                      /**< audio stream info: mime type */             //const char *  I use IntPtr need to check if OK
        public uint drmType;                 /**< audio stream info: drm type */
        public uint channels;                 /**< audio stream info: channels */
        public uint sampleRate;              /**< audio stream info: sample rate */
        public uint bitRate;                 /**< audio stream info: bit rate */
        public uint blockAlign;              /**< audio stream info: block align */
        public IntPtr codecExtraAata;        /**< audio stream info: codec extra data */  //unsigned char * i use IntPtr need to check if OK
        public uint extraDataSize;           /**< audio stream info: codec extra data length */
        public uint version;                  /**< audio stream info: version */
        public uint userInfo;                /**< audio stream info: user infomation */
        public uint width;                    /**< audio stream info: width */
        public uint depth;                    /**< audio stream info: depth */
        public uint endianness;               /**< audio stream info: endianness */
        public bool signedness;                       /**< audio stream info: signedness */
        public uint bufferType;              /**< audio stream info: buffer type */
        public bool isPreset;                        /**< audio stream info: whether use preset */

        // DRM Info
        public IntPtr propertyType;                   /**< video stream info: drminfo propertyType */
        public int typeLen;                          /**< video stream info: drminfo propertyType length */
        public IntPtr propertyData;                   /**< video stream info: drminfo propertyData */
        public int dataLen;                          /**< video stream info: drminfo property_date length */
    };

    /**
     * @brief struct of video stream info used for external demux
     */
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct VideoStreamInfo
    {
        public IntPtr mime;                    /**< video stream info: mime type */
        public uint drmType;                  /**< video stream info: drm type */
        public uint framerateNum;         /**< video stream info: framerate num */
        public uint framerateDen;         /**< video stream info: framerate den */
        public uint width;                 /**< video stream info: width */
        public uint height;                /**< video stream info: height */
        public uint pixelAspectRatioX;     /**< video stream info: pixelAspectRatioX */
        public uint pixelAspectRatioY;     /**< video stream info: pixelAspectRatioY */
        public IntPtr codecExtraData;     /**< video stream info: codec extra data */
        public uint extraDataSize;        /**< video stream info: codec extra data length */
        public uint version;                   /**< video stream info: version */
        public int format3D;                           /**< video stream info: 3D video format */
        public uint colordepth;            /**< video stream info: color depth */
        public uint maxWidth;             /**< video stream info: max width */
        public uint maxHeight;            /**< video stream info: max height */
        public uint bufferType;               /**< video stream info: buffer type */
        public uint displayAspectRatioX;       /**< video stream info: displayAspectRatioX */
        public uint displayAspectRatioY;       /**< video stream info: displayAspectRatioY */
        public IntPtr matroskaColourInfo;    /**< video stream info: matroska colour info */
        public bool isFramerateChanged;              /**< video stream info: frame changed */
        public bool isPreset;                      /** < video stream info:whether use preset */

        // DRM Info
        public IntPtr propertyType;                   /**< video stream info: drminfo propertyType */
        public int typeLen;                           /**< video stream info: drminfo propertyType length */
        public IntPtr propertyData;                   /**< video stream info: drminfo propertyData */
        public int dataLen;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct EsPlayerDrmInfo
    {
        public uint drmType;                     //drm type
        public uint algorithm;      /**<algorithm parameter*/
        public uint format;             /**<media format*/
        public uint phase;              /**<cipher phase*/

        // In GBytes data size will be written before the data.
        public IntPtr KID;                     /**< KID*/
        public uint KIDLen;                    /**<KID lenght*/
        public IntPtr IV;                      /**<initializetion vector*/
        public uint IVLen;                     /**<initializetion vector length*/
        public IntPtr key;                     //for clearkey
        public uint keyLen;                    // key len

        //SubData Size insert for subData in GBytes structure
        public IntPtr subData;
        public uint drmHandle;
        public uint tzHandle;
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

        //For API Seek ,we need to check if need set seek_completed_cb for each seek operation Seek(int absoluteTimeinMS,cb_function seek_completed_cb)
        bool Seek(int absoluteTimeinMS);   //10 seconds = 10 * 1000 

        //bool SelectTrack(TrackType streamType, uint track_index);            //To be implement on Pepper, and we also need to confirm if we need such API

        bool SetPlaybackRate(float rate);

        bool SubmitEOSPacket(TrackType trackType);

        bool SubmitPacket(IntPtr buf, uint size, System.UInt64 PTS, TrackType streamType, IntPtr drmInfo);

        bool Stop();
        //void Stop(const base::Callback<void(int32_t)>&) override;  If stop need such callback

        bool DestroyHandler();

        // Player setters
        bool SetApplicationID(string applicationId);

        bool SetDuration(System.UInt32 duration);      //C++ API use unsigned long, need to check if this type System.UInt32 is OK

        bool SetAudioStreamInfo(AudioStreamInfo audioStreamInfo);

        bool SetVideoStreamInfo(VideoStreamInfo videoStreamInfo);


        //bool SetDisplay(int winId, int x, int y, int width, int height);    //Need to confirm with cp side, this winId is wayland window id, need to create by App
                                                                            //For Display below APIs need to confirm with cp / app side if they need
                                                                            //int32_t SetDisplay(void* display, bool is_windowless) override;
                                                                            //int32_t SetDisplayRect(const PP_Rect& display_rect,const PP_FloatRect& crop_ratio_rect) override;
                                                                            //int32_t SetDisplayMode(PP_MediaPlayerDisplayMode display_mode) override;
                                                                            //bool IsDisplayModeSupported(PP_MediaPlayerDisplayMode display_mode) override;
        bool SetDisplay(PlayerDisplayType type, IntPtr display);              //New API, display should be a evas handler, its type should be elm_win.



        bool SetExternalSubtitlesPath(string filePath, string encoding);

        bool SetSubtitlesDelay(int milliSec);

        //The following two need to confirm which stream Property cp or app side need to set and get.
        //int32_t SetStreamingProperty(PP_StreamingProperty property,const std::string& data) override;
        //int32_t GetStreamingProperty(PP_StreamingProperty property, std::string* property_value) override;


        // Player getters

        //Need to confirm with cp & app side if they need below APIs, and vector can not be used on C#, so which data type they want to use.
        //int32_t GetAudioTracksList(std::vector<PP_AudioTrackInfo>* track_list) override;
        //int32_t GetTextTracksList(std::vector<PP_TextTrackInfo>* track_list) override;
        //int32_t GetVideoTracksList(std::vector<PP_VideoTrackInfo>* track_list) override;
        //string GetAvailableBitrates();

        uint GetCurrentTrack(TrackType streamType);

        System.UInt32 GetDuration();

        PlayerState GetPlayerState();


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

        //Below PrintLog() is not API, just used by me for internal test
        void PrintLog(string log);

    }
}
