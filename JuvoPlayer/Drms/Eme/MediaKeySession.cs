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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using JuvoPlayer.Common.Utils.IReferenceCountable;

namespace JuvoPlayer.Drms
{
    internal sealed class MediaKeySession : IDrmSession
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly DrmInitData initData;
        private string sessionId;
        private bool licenceInstalled;
        private IEnumerable<byte[]> keys;

        private readonly DrmDescription drmDescription;

        private bool isDisposed;
        private readonly CancellationTokenSource internalCancellationTokenSource;

        private int counter;
        ref int IReferenceCountable.Count => ref counter;

        private readonly TaskCompletionSource<bool> initializationTcs = new TaskCompletionSource<bool>();

        public ICdmInstance CdmInstance { get; }

        public void SetSessionId(string sessionId)
        {
            this.sessionId = sessionId;
        }

        public void SetLicenceInstalled()
        {
            licenceInstalled = true;
            if(initializationTcs.TrySetResult(true))
                Logger.Info($"Licence for session {sessionId} marked as installed.");
        }

        public bool IsInitialized() => licenceInstalled;
        public DrmInitData GetDrmInitData() => initData;
        public DrmDescription GetDrmDescription() => drmDescription;
        public string GetSessionId() => sessionId;
        public IEnumerable<byte[]> GetKeys() => keys;

        public MediaKeySession(DrmInitData initData, DrmDescription drmDescription, IEnumerable<byte[]> keys, CdmInstance cdmInstance)
        {
            if (string.IsNullOrEmpty(drmDescription?.LicenceUrl))
            {
                Logger.Error("Licence url is null");
                throw new NullReferenceException("Licence url is null");
            }

            this.initData = initData;
            this.drmDescription = drmDescription;
            this.keys = keys;
            CdmInstance = cdmInstance;

            internalCancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            Logger.Info($"Disposing MediaKeySession: {sessionId}");
            if (isDisposed)
                return;
            isDisposed = true;

            internalCancellationTokenSource?.Cancel();
            CdmInstance.CloseSession(sessionId);
        }

        public async Task<bool> WaitForInitialization()
        {
            if (isDisposed)
                return false;
            return await initializationTcs.Task;
        }
    }
}
