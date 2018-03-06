using JuvoPlayer.Common;

namespace JuvoPlayer.DRM
{
    public interface IDRMHandler
    {
        bool SupportsType(string type);
        bool SupportsSystemId(byte[] uuid);
        string GetScheme(byte[] uuid);
        IDRMSession CreateDRMSession(DRMInitData initData, DRMDescription drmDescription);
    }
}
