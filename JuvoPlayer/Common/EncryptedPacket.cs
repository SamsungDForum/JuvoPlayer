using JuvoPlayer.Drms;
using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Xml.Serialization;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;

namespace JuvoPlayer.Common
{
    [Serializable]
    public class EncryptedPacket : Packet
    {
        [Serializable]
        public struct Subsample
        {
            public uint ClearData;
            public uint EncData;
        }

        public byte[] KeyId;
        public byte[] Iv;
        public Subsample[] Subsamples;
        [XmlIgnore]
        public IDrmSession DrmSession;

        public Packet Decrypt()
        {
            if (DrmSession == null)
                throw new InvalidOperationException("Decrypt called without DrmSession");

            try
            {
                return DrmSession.DecryptPacket(this).Result;
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // wont be executed as above method always throws
            }
        }

        #region Disposable support
        // Use override to assure base class object references
        // of type EncryptedPacket will call this Dispose, not the base class
        //
        protected override void Dispose(bool disposing)
        {
            if (IsDisposed || !disposing)
                return;

            DrmSession?.Release();

            IsDisposed = true;
        }
        #endregion
    }
}
