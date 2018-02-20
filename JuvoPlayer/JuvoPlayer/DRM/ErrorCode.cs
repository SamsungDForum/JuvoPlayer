namespace JuvoPlayer.DRM
{
    public enum ErrorCode
    {
        Success = 0,
        NeedsDeviceCertificate,
        SessionNotFound,
        DecryptError,
        NoKey,
        InvalidAccess,
        TypeError,
        NotSupported,
        InvalidState,
        QuotaExceeded,
        InvalidHandle,
        RangeError,
        InvalidArgument,
        Generic,
    }
}
