using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Tizen.TV.Multimedia.ESPlayer
{
    public struct AudioStreamInfo
    {
        public byte[] codecData;
        public AudioMimeType mimeType;
        public int bitrate;
        public int channels;
        public int sampleRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeAudioStreamInfo
    {
        public IntPtr codecData;
        public int codecDataLength;
        public AudioMimeType mimeType;
        public int bitrate;
        public int channels;
        public int sampleRate;
    }
}
