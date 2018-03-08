using System;
using System.Collections.Generic;
using System.Linq;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Logging;

namespace JuvoPlayer.Drms
{
    public class DRMManager : IDRMManager
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly List<IDRMHandler> drmHandlers = new List<IDRMHandler>();
        private readonly List<DRMDescription> clipDrmConfiguration = new List<DRMDescription>();
        public DRMManager()
        {
        }

        public void UpdateDrmConfiguration(DRMDescription drmDescription)
        {
            Logger.Info("UpdateDrmConfiguration");

            var currentDescription = clipDrmConfiguration.FirstOrDefault(o => SchemeEquals(o.Scheme, drmDescription.Scheme));
            if (currentDescription == null)
            {
                clipDrmConfiguration.Add(drmDescription);
                return;
            }

            if (drmDescription.KeyRequestProperties != null)
                currentDescription.KeyRequestProperties = drmDescription.KeyRequestProperties;
            if (drmDescription.LicenceUrl != null)
                currentDescription.LicenceUrl = drmDescription.LicenceUrl;
        }

        public void RegisterDrmHandler(IDRMHandler handler)
        {
            drmHandlers.Add(handler);
        }

        public IDRMSession CreateDRMSession(DRMInitData data)
        {
            Logger.Info("Create Drmsession");
            var handler = drmHandlers.FirstOrDefault(o => o.SupportsSystemId(data.SystemId));
            if (handler == null)
            {
                Logger.Info("unknown drm init data");
                return null;
            }

            var scheme = handler.GetScheme(data.SystemId);
            var drmConfiguration = clipDrmConfiguration.FirstOrDefault(o => SchemeEquals(o.Scheme, scheme));
            if (drmConfiguration == null)
            {
                Logger.Info("drm not configured");
                return null;
            }

            var session = handler.CreateDRMSession(data, drmConfiguration);
            return session;
        }

        private static bool SchemeEquals(string scheme1, string scheme2)
        {
            return string.Equals(scheme1, scheme2, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
