using JuvoPlayer.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JuvoPlayer.DRM
{
    public class DRMManager : IDRMManager
    {
        private List<IDRMHandler> drmHandlers = new List<IDRMHandler>();
        private List<DRMDescription> clipDrmConfiguration = new List<DRMDescription>();
        public DRMManager()
        {
        }

        public void UpdateDrmConfiguration(DRMDescription drmDescription)
        {
            Tizen.Log.Info("JuvoPlayer", "UpdateDrmConfiguration");

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
            Tizen.Log.Info("JuvoPlayer", "Create Drmsession");
            var handler = drmHandlers.FirstOrDefault(o => o.SupportsSystemId(data.SystemId));
            if (handler == null)
            {
                Tizen.Log.Info("JuvoPlayer", "unknown drm init data");
                return null;
            }

            var session = handler.CreateDRMSession(data);
            var drmConfiguration = clipDrmConfiguration.FirstOrDefault(o => SchemeEquals(o.Scheme, session.CurrentDrmScheme));
            if (drmConfiguration == null)
            {
                Tizen.Log.Info("JuvoPlayer", "drm not configured");
                return null;
            }

            session.SetDrmConfiguration(drmConfiguration);
            return session;
        }

        private static bool SchemeEquals(string scheme1, string scheme2)
        {
            return string.Equals(scheme1, scheme2, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
