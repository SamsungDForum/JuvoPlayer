/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using JuvoPlayer.Common;
using JuvoLogger;

namespace JuvoPlayer.Drms
{
    public class DrmManager : IDrmManager
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly List<IDrmHandler> drmHandlers = new List<IDrmHandler>();
        private readonly List<DRMDescription> clipDrmConfiguration = new List<DRMDescription>();
        public DrmManager()
        {
        }

        public void UpdateDrmConfiguration(DRMDescription drmDescription)
        {
            Logger.Info("");

            lock (clipDrmConfiguration)
            {
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
        }

        public void RegisterDrmHandler(IDrmHandler handler)
        {
            lock (drmHandlers)
            {
                drmHandlers.Add(handler);
            }
        }

        public IDrmSession CreateDRMSession(DRMInitData data)
        {
            Logger.Info("Create DrmSession");

            lock (drmHandlers)
            {
                var handler = drmHandlers.FirstOrDefault(o => o.SupportsSystemId(data.SystemId));
                if (handler == null)
                {
                    Logger.Warn("unknown drm init data");
                    return null;
                }

                var scheme = handler.GetScheme(data.SystemId);

                lock (clipDrmConfiguration)
                {
                    var drmConfiguration = clipDrmConfiguration.FirstOrDefault(o => SchemeEquals(o.Scheme, scheme));
                    if (drmConfiguration == null)
                    {
                        Logger.Warn("drm not configured");
                        return null;
                    }

                    var session = handler.CreateDRMSession(data, drmConfiguration);
                    return session;
                }
            }
        }

        private static bool SchemeEquals(string scheme1, string scheme2)
        {
            return string.Equals(scheme1, scheme2, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
