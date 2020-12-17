/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using Nito.AsyncEx;

namespace JuvoPlayer.Drms
{
    public class DrmManager : IDrmManager, IDisposable
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly List<DrmDescription> clipDrmConfigurations = new List<DrmDescription>();
        private readonly ConcurrentDictionary<string, CdmInstance> cdmInstances = new ConcurrentDictionary<string, CdmInstance>();
        private static readonly AsyncLock drmManagerLock = new AsyncLock();
        private static readonly AsyncLock clipDrmConfigurationsLock = new AsyncLock();

        public async Task UpdateDrmConfiguration(DrmDescription drmDescription)
        {
            Logger.Info("Updating DRM configuration.");

            using(await clipDrmConfigurationsLock.LockAsync())
            {
                var currentDescription = clipDrmConfigurations.FirstOrDefault(o => string.Equals(o.Scheme, drmDescription.Scheme, StringComparison.CurrentCultureIgnoreCase));
                if (currentDescription == null)
                {
                    clipDrmConfigurations.Add(drmDescription);
                    return;
                }

                if (currentDescription.IsImmutable)
                {
                    Logger.Warn($"{currentDescription.Scheme} is immutable - ignoring update request");
                    return;
                }

                if (drmDescription.KeyRequestProperties != null)
                    currentDescription.KeyRequestProperties = drmDescription.KeyRequestProperties;
                if (drmDescription.LicenceUrl != null)
                    currentDescription.LicenceUrl = drmDescription.LicenceUrl;
            }
        }

        public async Task<IDrmSession> GetDrmSession(DrmInitData data)
        {
            var keyIds = DrmInitDataTools.GetKeyIds(data);
            var useGenericKey = keyIds.Count == 0;
            if (useGenericKey)
            {
                Logger.Info("No keys found in initData - using entire DrmInitData with InitData as a generic key.");
                keyIds.Add(data.InitData);
            }
            Logger.Info("Keys found - getting DrmSession.");
            IDrmSession session;
            using (await clipDrmConfigurationsLock.LockAsync())
                    session = await GetCdmInstance(data).GetDrmSession(data, keyIds, clipDrmConfigurations);
            return session;
        }

        public ICdmInstance GetCdmInstance(DrmInitData data)
        {
            var keySystem = EmeUtils.GetKeySystemName(data.SystemId);
            lock (drmManagerLock)
            {
                try
                {
                    return TryGetCdmInstance(keySystem, out var cdmInstance)
                        ? cdmInstance
                        : CreateCdmInstance(keySystem);
                }
                catch (Exception e)
                {
                    Logger.Error($"Getting CDM instance failed: {e.Message}");
                    return null;
                }
            }
        }

        private bool TryGetCdmInstance(string keySystem, out CdmInstance cdmInstance)
        {
            lock (drmManagerLock)
                return cdmInstances.TryGetValue(keySystem, out cdmInstance);
        }

        private bool TryRemoveCdmInstance(string keySystem)
        {
            lock (drmManagerLock)
            {
                var removed = cdmInstances.TryRemove(keySystem, out var cdmInstance);
                cdmInstance?.Dispose();
                return removed;
            }
        }

        private CdmInstance CreateCdmInstance(string keySystem)
        {
            var cdmInstance = new CdmInstance(keySystem);
            lock(drmManagerLock)
                if (!cdmInstances.TryAdd(keySystem, cdmInstance))
                {
                    Logger.Info($"Failed to add CdmInstance for {keySystem}!");
                    throw new DrmException($"Failed to add CdmInstance for {keySystem}!");
                }

            return cdmInstance;
        }

        public void Clear()
        {
            lock (drmManagerLock)
            {
                foreach (var cdmInstance in cdmInstances.Values)
                    cdmInstance.Dispose(); // all sessions will be closed while disposing
                cdmInstances.Clear();
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
