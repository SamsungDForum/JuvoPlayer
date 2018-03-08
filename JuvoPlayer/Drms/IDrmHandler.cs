using JuvoPlayer.Common;

namespace JuvoPlayer.Drms
{
    public interface IDrmHandler
    {
        bool SupportsType(string type);
        bool SupportsSystemId(byte[] uuid);
        string GetScheme(byte[] uuid);
        IDrmSession CreateDRMSession(DRMInitData initData, DRMDescription drmDescription);
    }
}
