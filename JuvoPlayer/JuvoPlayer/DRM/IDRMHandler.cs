using JuvoPlayer.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JuvoPlayer.DRM
{
    public interface IDRMHandler
    {
        bool SupportsType(string type);
        bool SupportsSchemeIdUri(string uri);
        bool SupportsSystemId(byte[] uuid);

        IDRMSession CreateDRMSession(DRMInitData initData);
    }
}
