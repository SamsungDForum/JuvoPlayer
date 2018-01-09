using JuvoPlayer.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JuvoPlayer.DRM
{
    public interface IDRMManager
    {
        void RegisterDrmHandler(IDRMHandler handler);
        void UpdateDrmConfiguration(DRMDescription drmDescription);

        IDRMSession CreateDRMSession(DRMInitData data);
    }
}
