using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JuvoPlayer.Common;

namespace JuvoPlayer.DRM.Cenc
{
    public class CencHandler : IDRMHandler
    {
        public IDRMSession CreateDRMSession(DRMInitData initData)
        {
            return new CencSession();
        }

        public bool SupportsSchemeIdUri(string uri)
        {
            return CencUtils.SupportsSchemeIdUri(uri);
        }

        public bool SupportsSystemId(byte[] uuid)
        {
            return CencUtils.SupportsSystemId(uuid);
        }

        public bool SupportsType(string type)
        {
            return CencUtils.SupportsType(type);
        }
    }
}
