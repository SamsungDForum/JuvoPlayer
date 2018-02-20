using System;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.DRM
{
    public interface IDRMSession : IDisposable
    {
        StreamPacket DecryptPacket(StreamPacket packet);
        Task<ErrorCode> StartLicenceChallenge();
    }
}
