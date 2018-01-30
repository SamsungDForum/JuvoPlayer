using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JuvoPlayer.Common
{
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
