using System;
using JuvoPlayer.Common;

namespace JuvoPlayer.DRM
{
    public interface IDRMSession : IDisposable
    {
        string CurrentDrmScheme { get; }
        StreamPacket DecryptPacket(StreamPacket packet);
        void SetDrmConfiguration(DRMDescription drmDescription);
        void Start();
    }
}
