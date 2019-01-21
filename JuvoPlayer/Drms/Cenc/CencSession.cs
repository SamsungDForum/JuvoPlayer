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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using Nito.AsyncEx;
using Tizen.TV.Security.DrmDecrypt;
using Tizen.TV.Security.DrmDecrypt.emeCDM;
using JuvoPlayer.Common.Utils.IReferenceCountable;

namespace JuvoPlayer.Drms.Cenc
{
    internal sealed class CencSession : IEventListener, IDrmSession
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private IEME CDMInstance;
        private readonly DRMInitData initData;
        private string currentSessionId;
        private bool licenceInstalled;

        private readonly DRMDescription drmDescription;
        private readonly AsyncContextThread thread = new AsyncContextThread();
        private readonly AsyncLock threadLock = new AsyncLock();

        // Cross CencSession license installation lock.
        private static readonly AsyncLock licenseLock = new AsyncLock();

        private bool isDisposed;
        private readonly CancellationTokenSource cancellationTokenSource;
        private TaskCompletionSource<byte[]> requestDataCompletionSource;

        private int counter;
        ref int IReferenceCountable.Count => ref counter;

        //Additional error code returned by drm_decrypt api when there is no space in TrustZone
        private const int E_DECRYPT_BUFFER_FULL = 2;
        private const int MaxDecryptRetries = 5;
        private static readonly TimeSpan DecryptBufferFullSleepTime = TimeSpan.FromMilliseconds(1000);

        private CencUtils.DrmType drmType;

        private CencSession(DRMInitData initData, DRMDescription drmDescription)
        {
            if (string.IsNullOrEmpty(drmDescription?.LicenceUrl))
            {
                Logger.Error("Licence url is null");
                throw new NullReferenceException("Licence url is null");
            }

            this.initData = initData;
            this.drmDescription = drmDescription;
            cancellationTokenSource = new CancellationTokenSource();
            drmType = CencUtils.GetDrmType(this.drmDescription.Scheme);
        }

        private void DestroyCDM()
        {
            if (CDMInstance == null)
                return;

            if (currentSessionId != null)
            {
                CDMInstance.session_close(currentSessionId);
                Logger.Info($"CencSession: {currentSessionId} closed");
                currentSessionId = null;
            }

            IEME.destroy(CDMInstance);
            CDMInstance = null;
        }

        public override void Dispose()
        {
            Logger.Info($"Disposing CencSession: {currentSessionId}");
            if (isDisposed)
                return;

            cancellationTokenSource?.Cancel();

            thread.Factory.Run(() => DestroyCDM()); //will do nothing on a disposed AsyncContextThread
            // thread.dispose is not waiting until thread ends. thread Join waits and calls dispose
            thread.Join();
            base.Dispose();

            GC.SuppressFinalize(this);

            isDisposed = true;
        }

        private static string Encode(byte[] initData)
        {
            return Encoding.GetEncoding(437).GetString(initData);
        }

        public static CencSession Create(DRMInitData initData, DRMDescription drmDescription)
        {
            return new CencSession(initData, drmDescription);
        }

        public Task<Packet> DecryptPacket(EncryptedPacket packet, CancellationToken token)
        {
            ThrowIfDisposed();
            return thread.Factory.Run(() => DecryptPacketOnIemeThread(packet, token));
        }

        private static byte[] PadIv(byte[] iv)
        {
            var paddedIv = new byte[16];
            Buffer.BlockCopy(iv, 0, paddedIv, 0, iv.Length);
            return paddedIv;
        }

        private async Task<Packet> DecryptPacketOnIemeThread(EncryptedPacket packet, CancellationToken token)
        {
            using (var linkedToken =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, token))
            using (await threadLock.LockAsync(linkedToken.Token))
                unsafe
                {
                    if (licenceInstalled == false)
                    {
                        Logger.Error("No licence installed");
                        throw new DrmException("No licence installed");
                    }

                    HandleSize[] pHandleArray = new HandleSize[1];
                    var numofparam = 1;

                    sMsdCipherParam[] param = new sMsdCipherParam[1];
                    param[0].algorithm = eMsdCipherAlgorithm.MSD_AES128_CTR;
                    param[0].format = eMsdMediaFormat.MSD_FORMAT_FMP4;
                    param[0].phase = eMsdCipherPhase.MSD_PHASE_NONE;
                    param[0].buseoutbuf = false;

                    // Do padding only for Widevine with keys shorter then 16 bytes
                    // Shorter keys need to be zero padded, not PCKS#7

                    if (drmType == CencUtils.DrmType.Widevine && packet.Iv.Length < 16)
                        packet.Iv = PadIv(packet.Iv);

                    fixed (byte* pdata = packet.Data, piv = packet.Iv, pkid = packet.KeyId)
                    {
                        param[0].pdata = pdata;
                        param[0].udatalen = (uint) packet.Data.Length;
                        param[0].poutbuf = null;
                        param[0].uoutbuflen = 0;
                        param[0].piv = piv;
                        param[0].uivlen = (uint) packet.Iv.Length;
                        param[0].pkid = pkid;
                        param[0].ukidlen = (uint) packet.KeyId.Length;

                        var subsamplePointer = IntPtr.Zero;

                        MSD_FMP4_DATA subData;
                        if (packet.Subsamples != null)
                        {
                            var subsamples = packet.Subsamples.Select(o =>
                                    new MSD_SUBSAMPLE_INFO
                                        {uBytesOfClearData = o.ClearData, uBytesOfEncryptedData = o.EncData})
                                .ToArray();

                            subsamplePointer = MarshalSubsampleArray(subsamples);

                            subData = new MSD_FMP4_DATA
                            {
                                uSubSampleCount = (uint) packet.Subsamples.Length,
                                pSubSampleInfo = subsamplePointer
                            };
                        }
                        else
                        {
                            subData = new MSD_FMP4_DATA
                            {
                                uSubSampleCount = 0,
                                pSubSampleInfo = IntPtr.Zero
                            };
                        }

                        var subdataPointer = Marshal.AllocHGlobal(Marshal.SizeOf(subData));
                        Marshal.StructureToPtr(subData, subdataPointer, false);
                        param[0].psubdata = subdataPointer;
                        param[0].psplitoffsets = IntPtr.Zero;

                        try
                        {
                            var ret = DecryptData(param, numofparam, ref pHandleArray, packet.StreamType,
                                linkedToken.Token);
                            if (ret == (int) eCDMReturnType.E_SUCCESS)
                            {
                                return new DecryptedEMEPacket(thread)
                                {
                                    Dts = packet.Dts,
                                    Pts = packet.Pts,
                                    StreamType = packet.StreamType,
                                    IsEOS = packet.IsEOS,
                                    IsKeyFrame = packet.IsKeyFrame,
                                    Duration = packet.Duration,
                                    HandleSize = pHandleArray[0]
                                };
                            }
                            else
                            {
                                Logger.Error($"Decryption failed: {packet.StreamType} - {ret}");
                                throw new DrmException($"Decryption failed: {packet.StreamType} - {ret}");
                            }
                        }
                        finally
                        {
                            if (subsamplePointer != IntPtr.Zero)
                                Marshal.FreeHGlobal(subsamplePointer);

                            Marshal.FreeHGlobal(subdataPointer);
                        }
                    }
                }
        }

        private eCDMReturnType DecryptData(sMsdCipherParam[] param, int numofparam, ref HandleSize[] pHandleArray,
            StreamType type, CancellationToken token)
        {
            var errorCount = 0;
            eCDMReturnType res;

            while (true)
            {
                token.ThrowIfCancellationRequested();

                res = API.EmeDecryptarray((eCDMReturnType) CDMInstance.getDecryptor(), ref param, numofparam,
                    IntPtr.Zero,
                    0, ref pHandleArray);

                if ((int) res == E_DECRYPT_BUFFER_FULL && errorCount < MaxDecryptRetries)
                {
                    Logger.Warn($"{type}: E_DECRYPT_BUFFER_FULL ({errorCount}/{MaxDecryptRetries})");

                    token.ThrowIfCancellationRequested();

                    ++errorCount;
                    Task.Delay(DecryptBufferFullSleepTime, token).Wait(token);

                    continue;
                }

                break;
            }

            return res;
        }

        private static unsafe IntPtr MarshalSubsampleArray(MSD_SUBSAMPLE_INFO[] subsamples)
        {
            int sizeOfSubsample = Marshal.SizeOf(typeof(MSD_SUBSAMPLE_INFO));
            int totalSize = sizeOfSubsample * subsamples.Length;
            var resultPointer = Marshal.AllocHGlobal(totalSize);
            byte* subsamplePointer = (byte*) (resultPointer.ToPointer());

            for (var i = 0; i < subsamples.Length; i++, subsamplePointer += (sizeOfSubsample))
            {
                IntPtr subsamplePointerIntPtr = new IntPtr(subsamplePointer);
                Marshal.StructureToPtr(subsamples[i], subsamplePointerIntPtr, false);
            }

            return resultPointer;
        }

        public override void onMessage(string sessionId, MessageType messageType, string message)
        {
            Logger.Info("Got Ieme message: " + sessionId);

            if (!sessionId.Equals(currentSessionId))
                return;

            switch (messageType)
            {
                case MessageType.kLicenseRequest:
                case MessageType.kIndividualizationRequest:
                {
                    requestDataCompletionSource?.TrySetResult(Encoding.GetEncoding(437).GetBytes(message));
                    break;
                }
                default:
                    Logger.Warn($"unknown message: {messageType}");
                    break;
            }
        }

        // There has been a change in the keys in the session or their status.
        public override void onKeyStatusesChange(string sessionId)
        {
        }

        // A remove() operation has been completed.
        public override void onRemoveComplete(string sessionId)
        {
        }

        public Task Initialize()
        {
            Logger.Info("");
            ThrowIfDisposed();
            return thread.Factory.Run(InitializeOnIemeThread);
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
                throw new ObjectDisposedException("CencSession is already disposed");
        }

        private async Task InitializeOnIemeThread()
        {
            var cancellationToken = cancellationTokenSource.Token;
            using (await threadLock.LockAsync(cancellationToken))
            {
                CreateIeme();
                cancellationToken.ThrowIfCancellationRequested();

                currentSessionId = CreateSession();
                Logger.Info($"CencSession ID {currentSessionId}");
                cancellationToken.ThrowIfCancellationRequested();

                var requestData = await GetRequestData();
                cancellationToken.ThrowIfCancellationRequested();

                var responseText = await AcquireLicenceFromServer(requestData);
                cancellationToken.ThrowIfCancellationRequested();

                await InstallLicence(responseText);
                licenceInstalled = true;
            }
        }

        private void CreateIeme()
        {
            var keySystem = CencUtils.GetKeySystemName(initData.SystemId);
            CDMInstance = IEME.create(this, keySystem, false, CDM_MODEL.E_CDM_MODEL_DEFAULT);
            if (CDMInstance == null)
                throw new DrmException(ErrorMessage.Generic);
        }

        private string CreateSession()
        {
            string sessionId = null;
            var status = CDMInstance.session_create(SessionType.kTemporary, ref sessionId);
            if (status != Status.kSuccess)
                throw new DrmException(EmeStatusConverter.Convert(status));
            Logger.Info("Created session: " + sessionId);
            return sessionId;
        }

        private async Task<byte[]> GetRequestData()
        {
            if (initData.InitData == null)
                throw new DrmException(ErrorMessage.InvalidArgument);

            requestDataCompletionSource = new TaskCompletionSource<byte[]>();

            var status =
                CDMInstance.session_generateRequest(currentSessionId, InitDataType.kCenc, Encode(initData.InitData));
            if (status != Status.kSuccess)
                throw new DrmException(EmeStatusConverter.Convert(status));

            var requestData = await requestDataCompletionSource.Task.WaitAsync(cancellationTokenSource.Token);
            requestDataCompletionSource = null;
            return requestData;
        }

        private async Task<string> AcquireLicenceFromServer(byte[] requestData)
        {
            using (var client = new HttpClient())
            {
                var licenceUrl = new Uri(drmDescription.LicenceUrl);

                client.BaseAddress = licenceUrl;
                Logger.Info(licenceUrl.AbsoluteUri);
                HttpContent content = new ByteArrayContent(requestData);
                content.Headers.ContentLength = requestData.Length;

                if (drmDescription.KeyRequestProperties != null)
                {
                    foreach (var property in drmDescription.KeyRequestProperties)
                    {
                        if (!property.Key.ToLowerInvariant().Equals("content-type"))
                            client.DefaultRequestHeaders.Add(property.Key, property.Value);
                        else if (MediaTypeHeaderValue.TryParse(property.Value, out var mediaType))
                            content.Headers.ContentType = mediaType;
                    }
                }

                var responseTask = await client.PostAsync(licenceUrl, content, cancellationTokenSource.Token);

                // TODO: Add retries. Net failures are expected

                Logger.Info("Response: " + responseTask);
                var receiveStream = responseTask.Content.ReadAsStreamAsync();
                var readStream = new StreamReader(await receiveStream, Encoding.GetEncoding(437));
                var responseText = await readStream.ReadToEndAsync();
                if (!responseText.StartsWith("GLS/1.0 0 OK"))
                    return responseText;

                const string headerMark = "\r\n\r\n";
                var headerMarkIndex = responseText.IndexOf(headerMark, StringComparison.Ordinal);
                return headerMarkIndex == -1
                    ? responseText
                    : responseText.Substring(headerMarkIndex + headerMark.Length);
            }
        }

        private async Task InstallLicence(string responseText)
        {
            Logger.Info($"Installing CencSession: {currentSessionId}");
            try
            {
                var cancellationToken = cancellationTokenSource.Token;
                Status status;
                using (await licenseLock.LockAsync(cancellationToken))
                {
                    status = CDMInstance.session_update(currentSessionId, responseText);
                }

                Logger.Info($"Install CencSession ${currentSessionId} result: {status}");

                if (status != Status.kSuccess)
                {
                    Logger.Error($"License Installation failure {EmeStatusConverter.Convert(status)}");
                    throw new DrmException(EmeStatusConverter.Convert(status));
                }
            }
            catch (Exception e)
            {
                //Something went wrong i.e. communication with the license server failed
                //TODO Show to the user as 'DRM license session error!' on the screen.
                throw new DrmException(EmeStatusConverter.Convert(Status.kUnexpectedError) + " - Exception message: " +
                                       e.Message);
            }
        }

        public override string ToString()
        {
            return currentSessionId;
        }
    }
}