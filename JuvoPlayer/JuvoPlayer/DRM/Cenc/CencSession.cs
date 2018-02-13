using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using JuvoPlayer.Common;
using Nito.AsyncEx;
using Tizen.TV.Security.DrmDecrypt;
using Tizen.TV.Security.DrmDecrypt.emeCDM;

namespace JuvoPlayer.DRM.Cenc
{
    class CencSession : IEventListener, IDRMSession
    {
        private IEME CDMInstance;
        private string currentSessionId;
        private string initDataString;

        private DRMDescription drmConfiguration;
        private AsyncContextThread thread;

        private readonly object locker = new object();

        private readonly object InitializationLocker = new object();
        bool IsInitialized = false;

        public string CurrentDrmScheme { get; }

        private CencSession(string keySystemName, string scheme)
        {
            Tizen.Log.Info("JuvoPlayer", scheme);

            CurrentDrmScheme = scheme;

            thread = new AsyncContextThread();

            DispatchOnIEMEThread(() => CreateIemeOnIemeThread(keySystemName));
        }

        private void DispatchOnIEMEThread(Action action)
        {
            thread.Factory.Run(action);
        }

        private void CreateIemeOnIemeThread(string keySystemName)
        {
            CDMInstance = IEME.create(this, keySystemName, false, CDM_MODEL.E_CDM_MODEL_DEFAULT);
        }

        ~CencSession()
        {
            if (CDMInstance != null)
                IEME.destroy(CDMInstance);

            thread.Dispose();
        }

        private bool Initialize(byte[] initData)
        {
            DispatchOnIEMEThread(() => InitializeOnIemeThread(initData));

            return true;
        }

        private void InitializeOnIemeThread(byte[] initData)
        {
            Tizen.Log.Info("JuvoPlayer", "Initialize DRM");

            string sessionId = "";
            var status = CDMInstance.session_create(SessionType.kTemporary, ref sessionId);
            if (status != Status.kSuccess)
            {
                Tizen.Log.Info("JuvoPlayer", "Could not create IEME session");
            }
            currentSessionId = sessionId;
            initDataString = EncodeInitData(initData);

            Tizen.Log.Info("JuvoPlayer", "Created session: " + currentSessionId); 
        }

        private static string EncodeInitData(byte[] initData)
        {
            return Encoding.GetEncoding(437).GetString(initData);
        }

        public static CencSession Create(string keySystemName, string scheme, DRMInitData initData)
        {
            var session = new CencSession(keySystemName, scheme);
            if (!session.Initialize(initData.InitData))
                return null;

            return session;
        }

        public void Start()
        {
            DispatchOnIEMEThread(StartOnIemeThread);
        }

        private void StartOnIemeThread()
        {
            var status = CDMInstance.session_generateRequest(currentSessionId, InitDataType.kCenc, initDataString);
            if (status != Status.kSuccess)
            {
                Tizen.Log.Info("JuvoPlayer", Thread.CurrentThread.ManagedThreadId + " Could not generate request: " + status.ToString());
            }
        }

        public unsafe StreamPacket DecryptPacket(StreamPacket packet)
        {
            if (!(packet is EncryptedStreamPacket))
            {
                return packet;
            }

            lock (InitializationLocker)
            {
                if (!IsInitialized)
                    Monitor.Wait(InitializationLocker);
            }

            var decryptedPacket = thread.Factory.Run(() => DecryptPacketOnIemeThread(packet)).Result;
            return decryptedPacket;
        }

        private unsafe DecryptedEMEPacket DecryptPacketOnIemeThread(StreamPacket packet)
        {
            var data = packet as EncryptedStreamPacket;

            HandleSize[] pHandleArray = new HandleSize[1];
            var numofparam = 1;

            sMsdCipherParam[] param = new sMsdCipherParam[1];
            param[0].algorithm = eMsdCipherAlgorithm.MSD_AES128_CTR;
            param[0].format = eMsdMediaFormat.MSD_FORMAT_FMP4;
            param[0].phase = eMsdCipherPhase.MSD_PHASE_NONE;
            param[0].buseoutbuf = false;

            fixed (byte* pdata = data.Data, piv = data.Iv, pkid = data.KeyId)
            {
                param[0].pdata = pdata;
                param[0].udatalen = (uint)data.Data.Length;
                param[0].poutbuf = null;
                param[0].uoutbuflen = 0;
                param[0].piv = piv;
                param[0].uivlen = (uint)data.Iv.Length;
                param[0].pkid = pkid;
                param[0].ukidlen = (uint)data.KeyId.Length;

                var subsamplePointer = IntPtr.Zero; ;

                MSD_FMP4_DATA subData;
                if (data.Subsamples != null)
                {
                    var subsamples = data.Subsamples.Select(o =>
                            new MSD_SUBSAMPLE_INFO {uBytesOfClearData = o.ClearData, uBytesOfEncryptedData = o.EncData})
                        .ToArray();

                    subsamplePointer = MarshalSubsampleArray(subsamples);

                    subData = new MSD_FMP4_DATA
                    {
                        uSubSampleCount = (uint)data.Subsamples.Length,
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
                        Tizen.Log.Error("JuvoPlayer", "Decryption failed: " + packet.StreamType + " - " + ret);
                    }
                }
                catch (Exception e)
                {
                    Tizen.Log.Error("JuvoPlayer", "exception: " + e.Message);
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

        public void SetDrmConfiguration(DRMDescription drmDescription)
        {
            drmConfiguration = drmDescription;
        }

        private void RequestLicenceOnIemeThread(string message)
        {
            if (string.IsNullOrEmpty(drmConfiguration?.LicenceUrl))
            {
                Tizen.Log.Error("JuvoPlayer", "Not configured drm");
                return;
            }

            HttpClient client = new HttpClient();
            var licenceUrl = new Uri(drmConfiguration.LicenceUrl);

            client.BaseAddress = licenceUrl;
            Tizen.Log.Info("JuvoPlayer", licenceUrl.AbsoluteUri);

            var requestData = Encoding.GetEncoding(437).GetBytes(message);
            HttpContent content = new ByteArrayContent(requestData);
            content.Headers.ContentLength = requestData.Length;

            if (drmConfiguration.KeyRequestProperties != null)
            {
                foreach (var property in drmConfiguration.KeyRequestProperties)
                {
                    if (!property.Key.ToLowerInvariant().Equals("content-type"))
                        client.DefaultRequestHeaders.Add(property.Key, property.Value);
                    else if (MediaTypeHeaderValue.TryParse(property.Value, out var mediaType))
                        content.Headers.ContentType = mediaType;
                }
            }

            var responseTask = client.PostAsync(licenceUrl, content).Result;

            Tizen.Log.Info("JuvoPlayer", "Response: " + responseTask);
            var receiveStream = responseTask.Content.ReadAsStreamAsync();
            var readStream = new StreamReader(receiveStream.Result, Encoding.GetEncoding(437));
            var responseText = readStream.ReadToEnd();
            if (responseText.IndexOf("<?xml", StringComparison.Ordinal) > 0)
                responseText = responseText.Substring(responseText.IndexOf("<?xml", StringComparison.Ordinal));

            var status = CDMInstance.session_update(currentSessionId, responseText);
            if (status != Status.kSuccess)
            {
                Tizen.Log.Info("JuvoPlayer", "Install licence error: " + status);
                return;
            }

            lock (InitializationLocker)
            {
                Tizen.Log.Info("JuvoPlayer", "Licence installed");

                IsInitialized = true;
                Thread.Sleep(1000);
                Monitor.PulseAll(InitializationLocker);
            }
        }

        public override void onMessage(string sessionId, MessageType messageType, string message)
        {
            Tizen.Log.Info("JuvoPlayer", "Got Ieme message: " + sessionId);

            if (!sessionId.Equals(currentSessionId))
                return;

            switch (messageType)
            {
                case MessageType.kLicenseRequest:
                case MessageType.kIndividualizationRequest:
                    {
                        DispatchOnIEMEThread(() => RequestLicenceOnIemeThread(message));
                        break;
                    }
                default:
                    Tizen.Log.Info("JuvoPlayer", "unknown message");
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
    }
}
