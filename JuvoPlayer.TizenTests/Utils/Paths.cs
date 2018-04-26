using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JuvoPlayer.TizenTests.Utils
{
    class Paths
    {
        public static string ApplicationPath => Path.GetDirectoryName(
            Path.GetDirectoryName(Tizen.Applications.Application.Current.ApplicationInfo.ExecutablePath));
    }
}
