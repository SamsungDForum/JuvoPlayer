using JuvoPlayer.Common;

namespace JuvoPlayer.DRM
{
    public interface IDRMSession
    {
        string CurrentDrmScheme { get; }
        StreamPacket DecryptPacket(StreamPacket packet);
        void SetDrmConfiguration(DRMDescription drmDescription);
        void Start();
    }
}
