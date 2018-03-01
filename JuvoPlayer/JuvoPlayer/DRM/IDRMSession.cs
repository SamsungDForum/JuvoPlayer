using System;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.DRM
{
    public interface IDRMSession : IDisposable
    {
        Task<ErrorCode> Initialize();
        Task<StreamPacket> DecryptPacket(EncryptedStreamPacket packet);
    }
}
