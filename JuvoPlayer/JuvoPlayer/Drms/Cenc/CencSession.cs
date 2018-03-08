using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Logging;
using Nito.AsyncEx;
using Tizen.TV.Security.DrmDecrypt;
using Tizen.TV.Security.DrmDecrypt.emeCDM;

namespace JuvoPlayer.Drms.Cenc
{
    public class CencSession : IEventListener, IDrmSession
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private IEME CDMInstance;
        private readonly DRMInitData initData;
        private string currentSessionId;
        private byte[] requestData;
        private bool licenceInstalled;

        private readonly DRMDescription drmDescription;
        private readonly AsyncContextThread thread = new AsyncContextThread();

        private CencSession(DRMInitData initData, DRMDescription drmDescription)
        {
            if (string.IsNullOrEmpty(drmDescription?.LicenceUrl))
            {
                throw new NullReferenceException("Licence url is null");
            }

            this.initData = initData;
            this.drmDescription = drmDescription;
        }

        private void ReleaseUnmanagedResources()
        {
            if (CDMInstance != null)
                thread.Factory.Run(() => IEME.destroy(CDMInstance));
            CDMInstance = null;
        }

        public override void Dispose()
        {
            ReleaseUnmanagedResources();
            base.Dispose();
            thread?.Dispose();

            GC.SuppressFinalize(this);
        }

        ~CencSession()
        {
            ReleaseUnmanagedResources();
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
            return thread.Factory.Run(() => DecryptPacketOnIemeThread(packet));
        }

        private unsafe Packet DecryptPacketOnIemeThread(EncryptedPacket packet)
        {
            if (licenceInstalled == false)
                return null;

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
                            new MSD_SUBSAMPLE_INFO {uBytesOfClearData = o.ClearData, uBytesOfEncryptedData = o.EncData})
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
                        Logger.Error("Decryption failed: " + packet.StreamType + " - " + ret);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error("exception: " + e.Message);
                }
                finally
                {
                    if (subsamplePointer != IntPtr.Zero)
                        Marshal.FreeHGlobal(subsamplePointer);

                    Marshal.FreeHGlobal(subdataPointer);
                }
            }

            return null;
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
                    requestData = Encoding.GetEncoding(437).GetBytes(message);
                    break;
                }
                default:
                    Logger.Info("unknown message");
                    break;
            }
        }

        // There has been a change in the keys in the session or their status.
        public override void onKeyStatusesChange(string session_id)
        {
        }

        // A remove() operation has been completed.
        public override void onRemoveComplete(string session_id)
        {
        }

        public Task Initialize()
        {
            return thread.Factory.Run(() => StartLicenceChallengeOnIemeThread());
        }

        private void StartLicenceChallengeOnIemeThread()
        {
            CreateIeme();
            currentSessionId = CreateSession();
            Logger.Info("Created session: " + currentSessionId);
            GenerateRequest();
            var responseText = AcquireLicenceFromServer();
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
            return sessionId;
        }

        private void GenerateRequest()
        {
            if (initData.InitData == null)
                throw new DrmException(ErrorMessage.InvalidArgument);
            var status = CDMInstance.session_generateRequest(currentSessionId, InitDataType.kCenc, Encode(initData.InitData));
            if (status != Status.kSuccess)
                throw new DrmException(EmeStatusConverter.Convert(status));
            // During session_generateRequest, we should got called back synchronously via onMessage and we should receive
            // requestData.
            if (requestData == null)
                throw new NotImplementedException("requestData is null. It will be probably delivered asynchronously. Implement this case.");
        }

        private string AcquireLicenceFromServer()
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

            var responseTask = client.PostAsync(licenceUrl, content).Result;

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
            var status = CDMInstance.session_update(currentSessionId, responseText);
            if (status != Status.kSuccess)
                throw new DrmException(EmeStatusConverter.Convert(status));
        }
    }
}
