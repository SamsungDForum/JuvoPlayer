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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;
using JuvoPlayer.Utils;
using Nito.AsyncEx;
using Tizen.TV.Security.DrmDecrypt;
using Tizen.TV.Security.DrmDecrypt.emeCDM;
using static Configuration.CdmInstance;
using Exception = System.Exception;

namespace JuvoPlayer.Drms
{
    public class CdmInstance : IEventListener, ICdmInstance
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public string KeySystem { get; }
        public EmeUtils.DrmType DrmType { get; }

        private IEME cdmInstance;
        private readonly ConcurrentDictionary<string, MediaKeySession> sessionsByIds = new ConcurrentDictionary<string, MediaKeySession>();
        private int sessionsDuringInitializationCount = 0;
        private TaskCompletionSource<bool> sessionsDuringInitializationTcs = new TaskCompletionSource<bool>();
        private readonly object cdmInstanceLock = new object();

        private bool isDisposed;
        
        //Additional error code returned by drm_decrypt api when there is no space in TrustZone
        private const int E_DECRYPT_BUFFER_FULL = 2;
        private readonly AsyncContextThread thread = new AsyncContextThread();

        public CdmInstance(string keySystem)
        {
            cdmInstance = IEME.create(this, keySystem, false, CDM_MODEL.E_CDM_MODEL_DEFAULT);
            if (cdmInstance == null)
            {
                Logger.Error($"Cannot create CDM instance for key system ${keySystem}!");
                throw new DrmException($"Cannot create CDM instance for key system ${keySystem}!");
            }

            KeySystem = keySystem;
            DrmType = EmeUtils.GetDrmTypeFromKeySystemName(KeySystem);
            sessionsDuringInitializationTcs.TrySetResult(true);
        }

        public async Task<IDrmSession> GetDrmSession(DrmInitData data, IEnumerable<byte[]> keys, List<DrmDescription> clipDrmConfigurations)
        {
            try
            {
                ThrowIfDisposed();
                return GetCachedDrmSession(keys) ??
                       await CreateDrmSession(data, keys, clipDrmConfigurations).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return null;
            }
        }

        private async Task<IDrmSession> CreateDrmSession(DrmInitData initData, IEnumerable<byte[]> keys, List<DrmDescription> clipDrmConfigurations)
        {
            Logger.Warn("Creating new DRM session.");
            var scheme = EmeUtils.GetScheme(initData.SystemId);
            var drmDescription = clipDrmConfigurations.FirstOrDefault(o => SchemeEquals(o.Scheme, scheme));
            if (drmDescription == null)
            {
                Logger.Warn("DRM not configured.");
                throw new DrmException("DRM not configured.");
            }

            var iemeKeySystemName = EmeUtils.GetKeySystemName(initData.SystemId);
            if (IEME.isKeySystemSupported(iemeKeySystemName) != Status.kSupported)
            {
                Logger.Warn($"Key System: {iemeKeySystemName} is not supported");
                throw new DrmException($"Key System: {iemeKeySystemName} is not supported");
            }

            var session = new MediaKeySession(initData, drmDescription, keys, this);
            SessionInitializing();

            try
            {
                await thread.Factory.Run(() => InitializeSessionOnIemeThread(session, initData)).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error($"EME session creation fail: {e}");
                SessionInitializingDone();
                CloseSession(session.GetSessionId());
                session.Release();
                return null;
            }

            return session;
        }

        private void InitializeSessionOnIemeThread(MediaKeySession session, DrmInitData initData)
        {
            var sessionId = CreateSession();
            session.SetSessionId(sessionId);

            lock(cdmInstanceLock)
                if (!sessionsByIds.TryAdd(sessionId, session))
                {
                    session.Release();
                    throw new Exception("Duplicate sessionId.");
                }

            GenerateRequest(sessionId, initData);
        }

        public string CreateSession()
        {
            string sessionId = null;
            var status = cdmInstance.session_create(SessionType.kTemporary, ref sessionId);
            if (status != Status.kSuccess)
                throw new DrmException(EmeStatusConverter.Convert(status));
            Logger.Info($"Created session: {sessionId}");
            return sessionId;
        }

        private MediaKeySession GetCachedDrmSession(IEnumerable<byte[]> keys)
        {
            lock (cdmInstanceLock)
                foreach (var session in sessionsByIds.Values)
                    if (keys.Any(key => session.GetKeys().Contains(key, new SessionKeyComparer())))
                    {
                        Logger.Info("Cached session found.");
                        return session;
                    }
            Logger.Info("Cached session not found.");
            return null;
        }

        private bool TryGetSession(string sessionId, out MediaKeySession session)
        {
            lock (cdmInstanceLock)
                return sessionsByIds.TryGetValue(sessionId, out session);
        }

        public override void onMessage(string sessionId, MessageType messageType, string message)
        {
            Logger.Info($"Got IEME message for session {sessionId}: {messageType}");
            switch (messageType)
            {
                // From EME spec: A message of type "license-request" or "individualization-request" will always be queued if the generateRequest algorithm succeeds and the promise is resolved.
                case MessageType.kLicenseRequest:
                case MessageType.kIndividualizationRequest:
                    if (!isDisposed)
                        _ = RunContinueSessionInitializationOnIemeThread(sessionId, message);
                    break;
                case MessageType.kLicenseAlreadyDone:
                    Logger.Warn($"Licence already installed for session {sessionId}");
                    if(!TryGetSession(sessionId, out var session) || session == null)
                        Logger.Info("Cannot find session for already installed licence - cannot mark it as initialized!");
                    session?.SetLicenceInstalled();
                    SessionInitializingDone();
                    break;
                default:
                    Logger.Warn($"[!] Unknown IEME message: {messageType}");
                    break;
            }
        }

        private void GenerateRequest(string sessionId, DrmInitData initData)
        {
            if (initData.InitData == null)
                throw new DrmException(ErrorMessage.InvalidArgument);
            lock(cdmInstanceLock)
            {
                if (!sessionsByIds.ContainsKey(sessionId))
                    throw new DrmException($"Cannot generate request for session {sessionId}.");

                var status = cdmInstance.session_generateRequest(sessionId, InitDataType.kCenc,
                    Encoding.GetEncoding(437).GetString(initData.InitData));
                if (status != Status.kSuccess)
                    throw new DrmException(EmeStatusConverter.Convert(status));
            }
        }

        private async Task RunContinueSessionInitializationOnIemeThread(string sessionId, string message)
        {
            try
            {
                await thread.Factory.Run(async () => await ContinueSessionInitializationOnIemeThread(sessionId, message)).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error($"{e}");
            }
        }

        private async Task ContinueSessionInitializationOnIemeThread(string sessionId, string requestResponseMessage)
        {
            TryGetSession(sessionId, out var session);
            var responseText = await AcquireLicenceFromServer(session,
                Encoding.GetEncoding(437).GetBytes(requestResponseMessage));
            Logger.Info($"Acquired license request response for session {sessionId}.");
            InstallLicence(session, responseText);
            SessionInitializingDone();
        }

        private async Task<string> AcquireLicenceFromServer(IDrmSession session, byte[] requestData)
        {
            var drmDescription = session.GetDrmDescription();
            var licenceUrl = new Uri(drmDescription.LicenceUrl);
            HttpClient httpClient = new HttpClient(new RetryHandler(new HttpClientHandler()));
            httpClient.BaseAddress = licenceUrl;
            Logger.Info(licenceUrl.AbsoluteUri);
            HttpContent content = new ByteArrayContent(requestData);
            content.Headers.ContentLength = requestData.Length;
            if (drmDescription.KeyRequestProperties != null)
            {
                foreach (var property in drmDescription.KeyRequestProperties)
                {
                    if (!property.Key.ToLowerInvariant().Equals("content-type"))
                        httpClient.DefaultRequestHeaders.Add(property.Key, property.Value);
                    else if (MediaTypeHeaderValue.TryParse(property.Value, out var mediaType))
                        content.Headers.ContentType = mediaType;
                }
            }
            var responseTask = await httpClient.PostAsync(licenceUrl, content);
            var receiveStream = responseTask.Content.ReadAsStreamAsync();
            var readStream = new StreamReader(await receiveStream, Encoding.GetEncoding(437));
            var responseText = await readStream.ReadToEndAsync();
            if (!responseText.StartsWith("GLS/1.0 0 OK"))
                return responseText;
            const string headerMark = "\r\n\r\n";
            var headerMarkIndex = responseText.IndexOf(headerMark, StringComparison.Ordinal);
            return headerMarkIndex == -1 ? responseText : responseText.Substring(headerMarkIndex + headerMark.Length);
       }

        private void InstallLicence(IDrmSession session, string responseText)
        {
            var sessionId = session.GetSessionId();
            SessionUpdate(sessionId, responseText);
            session.SetLicenceInstalled();
        }

        public override void onKeyStatusesChange(string sessionId)
        {
            Logger.Info($"Got IEME KeyStatusesChange for {sessionId}");
        }

        public override void onRemoveComplete(string sessionId)
        {
            Logger.Info($"Got IEME RemoveComplete for {sessionId}");
        }

        private void SessionInitializing()
        {
            lock (cdmInstanceLock)
            {
                if (sessionsDuringInitializationCount == 0)
                    sessionsDuringInitializationTcs = new TaskCompletionSource<bool>();
                ++sessionsDuringInitializationCount;
            }
        }

        private void SessionInitializingDone()
        {
            lock (cdmInstanceLock)
            {
                sessionsDuringInitializationCount = Math.Max(sessionsDuringInitializationCount - 1, 0);
                if (sessionsDuringInitializationCount == 0)
                    sessionsDuringInitializationTcs.TrySetResult(true);
            }
        }

        public async Task WaitForAllSessionsInitializations(CancellationToken cancellationToken)
        {
            await sessionsDuringInitializationTcs.Task.WithCancellation(cancellationToken);
        }

        public void SessionUpdate(string sessionId, string responseText)
        {
            Status status;
            lock(cdmInstanceLock)
            {
                if (!sessionsByIds.ContainsKey(sessionId))
                    throw new DrmException($"Cannot update session {sessionId}.");
                try
                {
                    status = cdmInstance.session_update(sessionId, responseText);
                }
                catch (Exception e)
                {
                    throw new DrmException($"{EmeStatusConverter.Convert(Status.kUnexpectedError)}  - Exception message: {e.Message}");
                }
            }

            Logger.Info($"Install MediaKeySession {sessionId} result: {status}");
            if (status != Status.kSuccess)
            {
                Logger.Error($"License Installation failure {EmeStatusConverter.Convert(status)}");
                throw new DrmException(EmeStatusConverter.Convert(status));
            }
        }

        public ulong GetDecryptor()
        {
            return cdmInstance.getDecryptor();
        }

        private static byte[] PadIv(byte[] iv)
        {
            var paddedIv = new byte[16];
            Buffer.BlockCopy(iv, 0, paddedIv, 0, iv.Length);
            return paddedIv;
        }

        public async Task<Packet> DecryptPacket(EncryptedPacket packet, CancellationToken token)
        {
            ThrowIfDisposed();
            return await thread.Factory.Run(() => DecryptPacketOnIemeThread(packet, token));
        }

        private Packet DecryptPacketOnIemeThread(EncryptedPacket packet, CancellationToken token)
        {
            // Do padding only for Widevine with keys shorter then 16 bytes
            // Shorter keys need to be zero padded, not PCKS#7
            if (DrmType == EmeUtils.DrmType.Widevine && packet.Iv.Length < 16)
                packet.Iv = PadIv(packet.Iv);

            var pHandleArray = new HandleSize[1];
            var ret = CreateParamsAndDecryptData(packet, ref pHandleArray, token);
            if (ret == eCDMReturnType.E_SUCCESS)
            {
                return new DecryptedEMEPacket(thread)
                {
                    Dts = packet.Dts,
                    Pts = packet.Pts,
                    StreamType = packet.StreamType,
                    IsKeyFrame = packet.IsKeyFrame,
                    Duration = packet.Duration,
                    HandleSize = pHandleArray[0]
                };
            }

            Logger.Error($"Decryption failed: {packet.StreamType} - {ret}");
            throw new DrmException($"Decryption failed: {packet.StreamType} - {ret}");
        }

        private unsafe eCDMReturnType CreateParamsAndDecryptData(EncryptedPacket packet, ref HandleSize[] pHandleArray, CancellationToken token)
        {
            switch (packet.Storage)
            {
                case IManagedDataStorage managedStorage:
                    fixed (byte* data = managedStorage.Data)
                        return CreateParamsAndDecryptData(packet, data, managedStorage.Data.Length, ref pHandleArray, token);
                case INativeDataStorage nativeStorage:
                    return CreateParamsAndDecryptData(packet, nativeStorage.Data, nativeStorage.Length, ref pHandleArray, token);
                default:
                    throw new DrmException($"Unsupported packet storage: {packet.Storage?.GetType()}");
            }
        }

        private unsafe eCDMReturnType CreateParamsAndDecryptData(EncryptedPacket packet, byte* data, int dataLen, ref HandleSize[] pHandleArray, CancellationToken token)
        {
            fixed (byte* iv = packet.Iv, kId = packet.KeyId)
            {
                var subdataPointer = MarshalSubsampleArray(packet);
                var subsampleInfoPointer = ((MSD_FMP4_DATA*)subdataPointer.ToPointer())->pSubSampleInfo;
                try
                {
                    var param = new sMsdCipherParam
                    {
                        algorithm = eMsdCipherAlgorithm.MSD_AES128_CTR,
                        format = eMsdMediaFormat.MSD_FORMAT_FMP4,
                        pdata = data,
                        udatalen = (uint)dataLen,
                        piv = iv,
                        uivlen = (uint)packet.Iv.Length,
                        pkid = kId,
                        ukidlen = (uint)packet.KeyId.Length,
                        psubdata = subdataPointer
                    };
                    return DecryptData(new[] { param }, ref pHandleArray, packet.StreamType, token);
                }
                finally
                {
                    if (subsampleInfoPointer != IntPtr.Zero)
                        Marshal.FreeHGlobal(subsampleInfoPointer);
                    Marshal.FreeHGlobal(subdataPointer);
                }
            }
        }

        private eCDMReturnType DecryptData(sMsdCipherParam[] param, ref HandleSize[] pHandleArray, StreamType type, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var res = API.EmeDecryptarray((eCDMReturnType)GetDecryptor(), ref param, param.Length, IntPtr.Zero, 0, ref pHandleArray);
            for (var errorCount = 0; (int)res == E_DECRYPT_BUFFER_FULL && errorCount < MaxDecryptRetries; ++errorCount)
            {
                token.ThrowIfCancellationRequested();
                Logger.Warn($"{type}: E_DECRYPT_BUFFER_FULL ({errorCount}/{MaxDecryptRetries})");
                Task.Delay(DecryptBufferFullSleepTime, token).Wait(token);
                res = API.EmeDecryptarray((eCDMReturnType)GetDecryptor(), ref param, param.Length, IntPtr.Zero, 0, ref pHandleArray);
            }
            return res;
        }

        private static unsafe IntPtr MarshalSubsampleArray(EncryptedPacket packet)
        {
            var subsamplePointer = IntPtr.Zero;
            if (packet.Subsamples != null)
            {
                var subsamples = packet.Subsamples.Select(o => new MSD_SUBSAMPLE_INFO { uBytesOfClearData = o.ClearData, uBytesOfEncryptedData = o.EncData }).ToArray();
                var totalSize = Marshal.SizeOf(typeof(MSD_SUBSAMPLE_INFO)) * subsamples.Length;
                var array = Marshal.AllocHGlobal(totalSize);
                var pointer = (MSD_SUBSAMPLE_INFO*)array.ToPointer();
                for (var i = 0; i < subsamples.Length; i++)
                    pointer[i] = subsamples[i];
                subsamplePointer = (IntPtr)pointer;
            }

            var subData = new MSD_FMP4_DATA
            {
                uSubSampleCount = (uint)(packet.Subsamples?.Length ?? 0),
                pSubSampleInfo = subsamplePointer
            };
            var subdataPointer = Marshal.AllocHGlobal(Marshal.SizeOf(subData));
            Marshal.StructureToPtr(subData, subdataPointer, false);
            return subdataPointer;
        }

        public void CloseSession(string sessionId)
        {
            lock (cdmInstanceLock)
            {
                Logger.Info($"Closing session {sessionId}.");
                if (cdmInstance == null || sessionId == null || !sessionsByIds.ContainsKey(sessionId))
                    return;
                try
                {
                    if(sessionsByIds.TryGetValue(sessionId, out _))
                        cdmInstance.session_close(sessionId);
                }
                catch (Exception e)
                {
                    Logger.Error(e.ToString());
                }
                finally
                {
                    sessionsByIds.TryRemove(sessionId, out var session);
                    session?.Release();
                }
            }
        }

        private void CloseAllSessions()
        {
            Logger.Info($"Closing all sessions for CDM {KeySystem}.");
            if (cdmInstance == null)
                return;

            lock(cdmInstanceLock)
                foreach (var sessionId in sessionsByIds.Keys)
                    CloseSession(sessionId);
        }

        private void DestroyCdm()
        {
            IEME.destroy(cdmInstance);
            cdmInstance = null;
        }

        public override void Dispose()
        {
            if (isDisposed)
                return;
            lock(cdmInstanceLock)
            {
                CloseAllSessions();
                DestroyCdm();
                thread.Dispose();
                base.Dispose();
                isDisposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                Logger.Error("CdmInstance is already disposed!");
                throw new ObjectDisposedException("CdmInstance is already disposed!");
            }
        }

        private static bool SchemeEquals(string scheme1, string scheme2)
        {
            return string.Equals(scheme1, scheme2, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    internal class SessionKeyComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y)
        {
            if (x == null || y == null)
                return x == null && y == null;

            return x.Length == y.Length && x.SequenceEqual(y);
        }

        public int GetHashCode(byte[] obj)
        {
            var len = obj.Length;
            var hash = 2058005163 ^ len;
            if (len == 0)
                return hash;

            // Use start/middle/end byte for hash value.
            hash ^= obj[0];
            hash ^= obj[len >> 1];
            hash ^= obj[len - 1];
            return hash;
        }
    }

    internal class RetryHandler : DelegatingHandler
    {
        private const int MaxRetries = 3;

        public RetryHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            for (var i = 0; i < MaxRetries; i++)
            {
                response = await base.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return response;
            }
            return response;
        }
    }
}
