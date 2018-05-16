using System;
using System.Collections.Generic;

namespace XamarinPlayer.Services
{
    public interface IClipReaderService
    {
        List<Clip> ReadClips(string path);
    }
}
