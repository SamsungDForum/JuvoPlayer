using System;
using System.IO;
using Xamarin.Forms;
using XamarinPlayer.Services;
using XamarinPlayer.Tizen.TV.Services;

[assembly: Dependency(typeof(PathService))]
namespace XamarinPlayer.Tizen.TV.Services
{
    class PathService : IPathService, IDisposable
    {
        public string ApplicationPath
        {
            get
            {
                return Path.GetDirectoryName(Path.GetDirectoryName(global::Tizen.Applications.Application.Current.ApplicationInfo.ExecutablePath));
            }
        }

        public void Dispose()
        {
        }
    }
}
