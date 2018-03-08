using JuvoPlayer.Common;

namespace JuvoPlayer.Drms
{
    public interface IDRMManager
    {
        void RegisterDrmHandler(IDRMHandler handler);
        void UpdateDrmConfiguration(DRMDescription drmDescription);

        IDRMSession CreateDRMSession(DRMInitData data);
    }
}
