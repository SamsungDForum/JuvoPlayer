using System;

namespace JuvoPlayer.Common
{
    [Serializable]
    class EncryptedStreamPacket : StreamPacket
    {
        public struct Subsample
        {
            public uint ClearData;
            public uint EncData;
        }

        public byte[] KeyId;
        public byte[] Iv;
        public Subsample[] Subsamples;
    }
}
