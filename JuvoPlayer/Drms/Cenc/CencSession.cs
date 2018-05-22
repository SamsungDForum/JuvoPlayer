using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using Nito.AsyncEx;
using Tizen.TV.Security.DrmDecrypt;
using Tizen.TV.Security.DrmDecrypt.emeCDM;

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
        private Task initializationTask;
        private bool isDisposing;
        private CancellationTokenSource cancellationTokenSource;
        private TaskCompletionSource<byte[]> requestDataCompletionSource;

        private CencSession(DRMInitData initData, DRMDescription drmDescription)
        {
            if (string.IsNullOrEmpty(drmDescription?.LicenceUrl))
            {
                throw new NullReferenceException("Licence url is null");
            }
            this.initData = initData;
            this.drmDescription = drmDescription;
        }

        private void DestroyCDM()
        {
            if (CDMInstance == null)
                return;

            if (currentSessionId != null)
            {
                CDMInstance.session_close(currentSessionId);
                currentSessionId = null;
            }

            IEME.destroy(CDMInstance);
            CDMInstance = null;
        }

        public override void Dispose()
        {
            isDisposing = true;
            cancellationTokenSource?.Cancel();
            try
            {
                initializationTask?.Wait();
            }
            catch (Exception)
            {
                // ignored, client can be notified about failures by awaiting task returned in Initialize() 
            }

            thread.Factory.Run(() => DestroyCDM()).Wait(); //will do nothing on a disposed AsyncContextThread
            // thread.dispose is not waiting until thread ends. thread Join waits and calls dispose
            thread.Join();
            base.Dispose();

            GC.SuppressFinalize(this);
        }

        ~CencSession()
        {
            //Dispose() was never called, so we have no choice,
            //but to try to destroy CDM on the finalizer thread
            DestroyCDM();
        }

        private static string Encode(byte[] initData)
        {
            return Encoding.GetEncoding(437).GetString(initData);
        }

        public static CencSession Create(DRMInitData initData, DRMDescription drmDescription)
        {
            return new CencSession(initData, drmDescription);
        }

        public Task<Packet> DecryptPacket(EncryptedPacket packet)
        {
            CancelIfDisposing();

            return thread.Factory.Run(() => DecryptPacketOnIemeThread(packet));
        }

        private void CancelIfDisposing()
        {
            if (isDisposing)
                throw new TaskCanceledException();
        }

        private unsafe Packet DecryptPacketOnIemeThread(EncryptedPacket packet)
        {
            if (licenceInstalled == false)
                throw new DrmException("No licence installed");

            if (CDMInstance == null)
                throw new TaskCanceledException();

            HandleSize[] pHandleArray = new HandleSize[1];
            var numofparam = 1;

            sMsdCipherParam[] param = new sMsdCipherParam[1];
            param[0].algorithm = eMsdCipherAlgorithm.MSD_AES128_CTR;
            param[0].format = eMsdMediaFormat.MSD_FORMAT_FMP4;
            param[0].phase = eMsdCipherPhase.MSD_PHASE_NONE;
            param[0].buseoutbuf = false;

            fixed (byte* pdata = packet.Data, piv = packet.Iv, pkid = packet.KeyId)
            {
                param[0].pdata = pdata;
                param[0].udatalen = (uint)packet.Data.Length;
                param[0].poutbuf = null;
                param[0].uoutbuflen = 0;
                param[0].piv = piv;
                param[0].uivlen = (uint)packet.Iv.Length;
                param[0].pkid = pkid;
                param[0].ukidlen = (uint)packet.KeyId.Length;

                var subsamplePointer = IntPtr.Zero;

                MSD_FMP4_DATA subData;
                if (packet.Subsamples != null)
                {
                    var subsamples = packet.Subsamples.Select(o =>
                            new MSD_SUBSAMPLE_INFO { uBytesOfClearData = o.ClearData, uBytesOfEncryptedData = o.EncData })
                        .ToArray();

                    subsamplePointer = MarshalSubsampleArray(subsamples);

                    subData = new MSD_FMP4_DATA
                    {
                        uSubSampleCount = (uint)packet.Subsamples.Length,
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
                    var ret = API.EmeDecryptarray((eCDMReturnType)CDMInstance.getDecryptor(), ref param, numofparam, IntPtr.Zero, 0, ref pHandleArray);
                    if (ret == eCDMReturnType.E_SUCCESS)
                    {
                        return new DecryptedEMEPacket(thread)
                        {
                            Dts = packet.Dts,
                            Pts = packet.Pts,
                            StreamType = packet.StreamType,
                            IsEOS = packet.IsEOS,
                            IsKeyFrame = packet.IsKeyFrame,
                            HandleSize = pHandleArray[0]
                        };
                    }
                    else
                    {
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

        private static unsafe IntPtr MarshalSubsampleArray(MSD_SUBSAMPLE_INFO[] subsamples)
        {
            int sizeOfSubsample = Marshal.SizeOf(typeof(MSD_SUBSAMPLE_INFO));
            int totalSize = sizeOfSubsample * subsamples.Length;
            var resultPointer = Marshal.AllocHGlobal(totalSize);
            byte* subsamplePointer = (byte*)(resultPointer.ToPointer());

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
                    Logger.Info("unknown message");
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
            if (initializationTask != null)
                throw new InvalidOperationException("Initialize in progress");

            cancellationTokenSource = new CancellationTokenSource();
            initializationTask = thread.Factory.Run(DoLicenceChallengeOnIemeThread);
            return initializationTask;
        }

        private async Task DoLicenceChallengeOnIemeThread()
        {
            var cancellationToken = cancellationTokenSource.Token;

            CreateIeme();
            cancellationToken.ThrowIfCancellationRequested();

            currentSessionId = CreateSession();
            cancellationToken.ThrowIfCancellationRequested();

            var requestData = await GetRequestData();
            cancellationToken.ThrowIfCancellationRequested();

            var responseText = await AcquireLicenceFromServer(requestData);
            cancellationToken.ThrowIfCancellationRequested();

            InstallLicence(responseText);
            licenceInstalled = true;
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
         
        private Task<byte[]> GetRequestData()
        {
            if (initData.InitData == null)
                throw new DrmException(ErrorMessage.InvalidArgument);

            requestDataCompletionSource = new TaskCompletionSource<byte[]>();
            cancellationTokenSource.Token.Register(() => requestDataCompletionSource.TrySetCanceled());

            var status = CDMInstance.session_generateRequest(currentSessionId, InitDataType.kCenc, Encode(initData.InitData));
            if (status != Status.kSuccess)
                throw new DrmException(EmeStatusConverter.Convert(status));

            return requestDataCompletionSource.Task;
        }

        private async Task<string> AcquireLicenceFromServer(byte[] requestData)
        {
            HttpClient client = new HttpClient();
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

            Logger.Info("Response: " + responseTask);
            var receiveStream = responseTask.Content.ReadAsStreamAsync();
            var readStream = new StreamReader(receiveStream.Result, Encoding.GetEncoding(437));
            var responseText = readStream.ReadToEnd();
            if (responseText.IndexOf("<?xml", StringComparison.Ordinal) > 0)
                responseText = responseText.Substring(responseText.IndexOf("<?xml", StringComparison.Ordinal));
            return responseText;
        }

        private void InstallLicence(string responseText)
        {
            try
            {
                var status = CDMInstance.session_update(currentSessionId, responseText);
                if (status != Status.kSuccess)
                    throw new DrmException(EmeStatusConverter.Convert(status));
            }
            catch (Exception e)
            {
                //Something went wrong i.e. communication with the license server failed
                //TODO Show to the user as 'DRM license session error!' on the screen.
                throw new DrmException(EmeStatusConverter.Convert(Status.kUnexpectedError) + " - Exception message: " + e.Message );
            }
            
        }
    }
}
