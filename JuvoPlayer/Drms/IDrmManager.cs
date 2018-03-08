using JuvoPlayer.Common;

namespace JuvoPlayer.Drms
{
    public interface IDrmManager
    {
        void RegisterDrmHandler(IDrmHandler handler);
        void UpdateDrmConfiguration(DRMDescription drmDescription);

        IDrmSession CreateDRMSession(DRMInitData data);
    }
}
