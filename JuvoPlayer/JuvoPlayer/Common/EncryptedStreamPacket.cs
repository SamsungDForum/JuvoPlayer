using System;

namespace JuvoPlayer.Common
{
    [Serializable]
    public class EncryptedStreamPacket : StreamPacket
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
    }
}
