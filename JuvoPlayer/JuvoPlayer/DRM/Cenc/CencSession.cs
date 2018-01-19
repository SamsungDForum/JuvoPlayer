using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using JuvoPlayer.Common;
using Nito.AsyncEx;
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
            DispatchOnIEMEThread(() => StartOnIemeThread());
        }

        private void StartOnIemeThread()
        {
            var status = CDMInstance.session_generateRequest(currentSessionId, InitDataType.kCenc, initDataString);
            if (status != Status.kSuccess)
            {
                Tizen.Log.Info("JuvoPlayer", Thread.CurrentThread.ManagedThreadId + " Could not generate request: " + status.ToString());
            }
        }

        public StreamPacket DecryptPacket(StreamPacket packet)
        {
            throw new NotImplementedException();
        }

        public void SetDrmConfiguration(DRMDescription drmDescription)
        {
            drmConfiguration = drmDescription;
        }

        private void RequestLicenceOnIemeThread(string message)
        {
            HttpClient client = new HttpClient();
            var licenceUrl = new Uri(drmConfiguration.LicenceUrl);
            client.BaseAddress = licenceUrl;
            if (drmConfiguration.KeyRequestProperties != null)
            {
                foreach (var property in drmConfiguration.KeyRequestProperties)
                {
                    client.DefaultRequestHeaders.Add(property.Key, property.Value);
                }
            }

            var requestData = Encoding.GetEncoding(437).GetBytes(message);
            HttpContent content = new ByteArrayContent(requestData);
            content.Headers.ContentLength = requestData.Length;

            Tizen.Log.Info("JuvoPlayer", licenceUrl.AbsoluteUri);

            var responseTask = client.PostAsync(licenceUrl, content).Result;

            Tizen.Log.Info("JuvoPlayer", "Response: " + responseTask);
            var receiveStream = responseTask.Content.ReadAsStreamAsync();
            StreamReader readStream = new StreamReader(receiveStream.Result, Encoding.GetEncoding(437));
            var responseText = readStream.ReadToEnd();
            if (responseText.IndexOf("<?xml") > 0)
                responseText = responseText.Substring(responseText.IndexOf("<?xml"));

            var status = CDMInstance.session_update(currentSessionId, responseText);
            if (status != Status.kSuccess)
            {
                Tizen.Log.Info("JuvoPlayer", "Install licence error: " + status);
                return;
            }
            Tizen.Log.Info("JuvoPlayer", "Licence installed");
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
