using JuvoPlayer.Common;

namespace JuvoPlayer.DRM
{
    public interface IDRMManager
    {
        void RegisterDrmHandler(IDRMHandler handler);
        void UpdateDrmConfiguration(DRMDescription drmDescription);

        IDRMSession CreateDRMSession(DRMInitData data);
    }
}
