namespace Tizen.TV.Multimedia.ESPlayer
{
    public enum ErrorType
    {

    }

    public enum StreamType
    {
        Audio = 0,
        Video = 1
    }

    public enum BufferStatus
    {
        Underrun,
        Overrun
    }

    public enum SubmitStatus
    {
        NotPrepared,
        InvalidPacket,
        OutOfMemory,
        Full,
        Success
    }

    public enum EsState
    {
        None, /**< Player is created, but not opened */
        Idle, /**< Player is opened, but not prepared or player is stopped */
        Ready,              /**< Player is ready to play(start) */
        Playing,            /**< Player is playing media */
        Paused              /**< Player is paused while playing media */
    }

    public enum AudioMimeType
    {
        UnKnown,
        Aac,
        Mp2,
        Mp3,
        Ac3,
        Eac3,
        Vorbis,
        Dts,
        Opus,
        PcmS16le,
        PcmS16be,
        PcmU16le,
        PcmU16be,
        PcmS24le,
        PcmS24be,
        PcmU24le,
        PcmU24be,
        PcmS32le,
        PcmS32be,
        PcmU32le,
        PcmU32be
    }

    public enum VideoMimeType
    {
        UnKnown,
        H263,
        H264,
        Hevc,
        Mpeg1,
        Mpeg2,
        Mpeg4,
        Vp8,
        Vp9,
        Wmv3
    }

    public enum DisplayMode
    {
        LetterBox,
        OriginSize,
        FullScreen,
        CroppedFull,
        OriginOrLetter,
        DstRoi
    }
    public enum DisplayType
    {
        None,
        Overlay,
        Evas
    }

    public enum DrmType
    {
        None,
        Playready,
        Marlin,
        Verimatrix,
        WidevineCdm,
        Max
    }
}
