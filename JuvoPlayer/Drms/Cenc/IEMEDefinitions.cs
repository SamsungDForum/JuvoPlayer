using System;
using System.Runtime.InteropServices;

namespace JuvoPlayer.Drms.Cenc
{
    internal unsafe struct DecryptionData
    {
        public bool IsEncrypted;
        public bool IsSecure;
        public byte[] KeyId;
        public byte[] EncryptData;
        public byte[] Iv;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct MSD_SUBSAMPLE_INFO
    {
        public uint uBytesOfClearData;
        public uint uBytesOfEncryptedData;
    };


    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct MSD_FMP4_DATA
    {
        public uint uSubSampleCount;
        public IntPtr pSubSampleInfo;
    };
}
