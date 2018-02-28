using System;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.DRM
{
    public interface IDRMSession : IDisposable
    {
        Task<StreamPacket> DecryptPacket(EncryptedStreamPacket packet);
        Task<ErrorCode> StartLicenceChallenge();
    }
}
