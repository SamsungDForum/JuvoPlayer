using JuvoPlayer.Drms;
using System;
using System.Xml.Serialization;

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

            return DrmSession.DecryptPacket(this).Result;
        }
    }
}
