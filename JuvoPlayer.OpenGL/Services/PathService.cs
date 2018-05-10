using System;
using System.IO;

namespace JuvoPlayer.OpenGL.Services
{
    class PathService : IDisposable
    {
        public string ApplicationPath
        {
            get
            {
                return Path.GetDirectoryName(Path.GetDirectoryName(global::Tizen.Applications.Application.Current
                    .ApplicationInfo.ExecutablePath));
            }
        }

        public void Dispose()
        {
        }
    }
}