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
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using JuvoPlayer.Common;

namespace JuvoPlayer.Drms
{
    public class CdmContext : IDisposable
    {
        private bool _isDisposed;
        private readonly IList<DrmInitData> _drmInitDatas;
        private readonly IList<string> _sessionIds;
        private ICdmInstance _cdmInstance;
        private IDisposable _onMessageSubscription;
        private readonly IDrmSessionHandler _drmSessionHandler;

        public CdmContext(IDrmSessionHandler sessionHandler)
        {
            _drmSessionHandler = sessionHandler;
            _drmInitDatas = new List<DrmInitData>();
            _sessionIds = new List<string>();
        }

        public void InitCdmInstance(string keySystem)
        {
            var platform = Platform.Current;
            var capabilities = platform.Capabilities;
            if (!capabilities.SupportsKeySystem(keySystem))
            {
                throw new NotSupportedException(
                    $"{keySystem} is not supported");
            }

            if (_cdmInstance != null)
            {
                throw new InvalidOperationException(
                    "CdmInstance is already initialized");
            }

            _cdmInstance = platform.CreateCdmInstance(keySystem);
            _onMessageSubscription = _cdmInstance
                .OnSessionMessage()
                .ObserveOn(SynchronizationContext.Current)
                .Subscribe(OnCdmInstanceMessage);

            if (_drmInitDatas.Count <= 0)
                return;
            foreach (var drmInitData in _drmInitDatas)
                CreateSessionAndGenerateRequest(drmInitData);
        }

        public void AddDrmInitData(DrmInitData newData)
        {
            if (_drmInitDatas.Any(
                data => data.Data.SequenceEqual(newData.Data)))
                return;
            _drmInitDatas.Add(newData);
            if (_cdmInstance == null)
                return;
            CreateSessionAndGenerateRequest(newData);
        }

        public ICdmInstance GetCdmInstance()
        {
            return _cdmInstance;
        }

        private void CreateSessionAndGenerateRequest(DrmInitData drmInitData)
        {
            var sessionId = _cdmInstance.CreateSession();
            _sessionIds.Add(sessionId);
            _cdmInstance.GenerateRequest(
                sessionId,
                drmInitData.DataType,
                drmInitData.Data);
        }

        private void OnCdmInstanceMessage(Message message)
        {
            switch (message.Type)
            {
                case MessageType.LicenseRequest:
                case MessageType.IndividualizationRequest:
                    var sessionId = message.SessionId;
                    var data = message.Data;
                    AcquireLicenseAndUpdateSession(sessionId, data);
                    break;
                case MessageType.LicenseRenewal:
                    break;
                case MessageType.LicenseRelease:
                    break;
                case MessageType.AlreadyDone:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async void AcquireLicenseAndUpdateSession(
            string sessionId,
            byte[] data)
        {
            var response = await _drmSessionHandler.AcquireLicense(
                sessionId,
                data);
            if (_isDisposed)
                return;
            _cdmInstance.UpdateSession(
                sessionId,
                response);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                CloseAllSessions();
                _cdmInstance?.Dispose();
                _onMessageSubscription?.Dispose();
            }
            finally
            {
                _isDisposed = true;
            }
        }

        private void CloseAllSessions()
        {
            foreach (var sessionId in _sessionIds)
            {
                try
                {
                    _cdmInstance.CloseSession(sessionId);
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            _sessionIds.Clear();
        }
    }
}