using System;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.Drms
{
    /// <summary>Represents a single DRM Session.</summary>
    public interface IDrmSession : IDisposable
    {
        /// <summary>
        /// Function returns an instance to self. 
        /// Provides reference counting mechanism for Dispose(). 
        /// GetInstance() increments reference counter, Dispose() decrements reference counter.
        /// Actual dispose is execute when reference counter reaches zero.
        /// </summary>
        /// <returns></returns>
        IDrmSession GetInstance();

        /// <summary>
        /// Function force an instance of the object to be Disposed, regardless of the number of locks 
        /// being currently held on it.
        /// </summary>
        void FreeInstance();

        /// <summary>
        /// Method marks session instance as removable when reference counter reaches zero.
        /// </summary>
        void AllowRemoval();

        /// <summary>Initializes a new instance of the <see cref="T:JuvoPlayer.Drms.IDRMSession"></see> class.</summary>
        /// <returns>A task, which will complete when Session initialization finishes.</returns>
        /// <exception cref="T:JuvoPlayer.Drms.DRMException">Session couldn't be initialized.</exception>
        Task Initialize();

        /// <summary>Asynchronously decrypts a single <see cref="T:JuvoPlayer.Common.EncryptedPacket"></see>.</summary>
        /// <param name="packet">Packet to decrypt.</param>
        /// <returns>A task, which will produce decrypted <see cref="T:JuvoPlayer.Common.Packet"></see>.</returns>
        /// <exception cref="T:JuvoPlayer.Drms.DRMException">Session is not initialized or packet couldn't be decrypted.</exception>
        Task<Packet> DecryptPacket(EncryptedPacket packet);
    }
}
