using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JuvoPlayer.Common;
using Tizen.TV.Security.DrmDecrypt.emeCDM;

namespace JuvoPlayer.DRM.Cenc
{
    public class CencHandler : IDRMHandler
    {
        public CencHandler()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public IDRMSession CreateDRMSession(DRMInitData initData)
        {
            var iemeKeySystemName = GetKeySystemName(initData.SystemId);
            if (IEME.isKeySystemSupported(iemeKeySystemName) != Status.kSupported)
            {
                Tizen.Log.Info("JuvoPlayer", string.Format("Key System: {0} is not supported", iemeKeySystemName));
                return null;
            }
            return CencSession.Create(iemeKeySystemName, GetScheme(initData.SystemId), initData);
        }

        public bool SupportsSchemeIdUri(string uri)
        {
            return CencUtils.SupportsSchemeIdUri(uri);
        }

        public bool SupportsSystemId(byte[] uuid)
        {
            if (!CencUtils.SupportsSystemId(uuid))
                return false;

            var iemeKeySystemName = GetKeySystemName(uuid);
            return IEME.isKeySystemSupported(iemeKeySystemName) == Status.kSupported;
        }

        public bool SupportsType(string type)
        {
            return CencUtils.SupportsType(type);
        }

        private string GetKeySystemName(byte[] systemId)
        {
            if (systemId.SequenceEqual(CencUtils.PlayReadySystemId))
                return "com.microsoft.playready";
            if (systemId.SequenceEqual(CencUtils.WidevineSystemId))
                return "com.widevine.alpha";

            return null;
        }

        private string GetScheme(byte[] systemId)
        {
            if (systemId.SequenceEqual(CencUtils.PlayReadySystemId))
                return "playready";
            if (systemId.SequenceEqual(CencUtils.WidevineSystemId))
                return "widevine";

            return null;
        }
    }
}
