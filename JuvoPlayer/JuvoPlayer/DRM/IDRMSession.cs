using System;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.DRM
{
    /// <summary>Represents a single DRM Session.</summary>
    public interface IDRMSession : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="T:JuvoPlayer.DRM.IDRMSession"></see> class.</summary>
        /// <returns>A task, which will complete when Session initialization finishes.</returns>
        /// <exception cref="T:JuvoPlayer.DRM.DRMException">Session couldn't be initialized.</exception>
        Task Initialize();

        /// <summary>Asynchronously decrypts a single <see cref="T:JuvoPlayer.Common.EncryptedStreamPacket"></see>.</summary>
        /// <param name="packet">Packet to decrypt.</param>
        /// <returns>A task, which will produce decrypted <see cref="T:JuvoPlayer.Common.StreamPacket"></see>.</returns>
        /// <exception cref="T:JuvoPlayer.DRM.DRMException">Session is not initialized or packet couldn't be decrypted.</exception>
        Task<StreamPacket> DecryptPacket(EncryptedStreamPacket packet);
    }
}
