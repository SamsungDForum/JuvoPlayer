using System;
using System.Collections.Generic;

namespace XamarinPlayer.Services
{
    public interface IClipReaderService : IDisposable
    {
        List<Clip> ReadClips(string path);
    }
}
