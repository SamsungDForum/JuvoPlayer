using System.Linq;
using System.Text;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Logging;
using Tizen.TV.Security.DrmDecrypt.emeCDM;

namespace JuvoPlayer.Drms.Cenc
{
    public class CencHandler : IDRMHandler
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public CencHandler()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public IDRMSession CreateDRMSession(DRMInitData initData, DRMDescription drmDescription)
        {
            var iemeKeySystemName = CencUtils.GetKeySystemName(initData.SystemId);
            if (IEME.isKeySystemSupported(iemeKeySystemName) != Status.kSupported)
            {
                Logger.Info(string.Format("Key System: {0} is not supported", iemeKeySystemName));
                return null;
            }
            return CencSession.Create(initData, drmDescription);
        }

        public bool SupportsSystemId(byte[] uuid)
        {
            if (!CencUtils.SupportsSystemId(uuid))
                return false;

            var iemeKeySystemName = CencUtils.GetKeySystemName(uuid);
            return IEME.isKeySystemSupported(iemeKeySystemName) == Status.kSupported;
        }

        public bool SupportsType(string type)
        {
            return CencUtils.SupportsType(type);
        }

        public string GetScheme(byte[] uuid)
        {
            return CencUtils.GetScheme(uuid);
        }
    }
}
