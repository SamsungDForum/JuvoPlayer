using System;
using System.Net.Http;
using JuvoLogger;
using MpdParser;
using System.Threading.Tasks;
using System.Threading;

namespace JuvoPlayer.DataProviders.Dash
{

    internal class DashManifest : IManifest
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);
        private bool reloadsRequired = true;
        private bool reloadInfoPrinted = false;

        /// <summary>
        /// Notification event. Will be called when MPD is updated.
        /// </summary>
        public event ManifestChanged ManifestChanged;
        public Uri Uri { get; set; }

        /// <summary>
        /// Task as returned by TimerEvent responsible for 
        /// MPD download and parsing.
        /// Update activity is a Task responsible for calling update.
        /// This is required - as update may call request for update so we need to "complete it"
        /// </summary>

        public Task reloadActivity;
        private Task updateActivity;
        private static readonly int ReloadIdle = 0;
        private static readonly int ReloadRunning = 1;
        private int reloadState = ReloadIdle;

        public Task GetReloadManifestActivity
        {
            get { return reloadActivity; }
        }

        public bool IsReloadInProgress
        {
            get { return (reloadActivity?.Status < TaskStatus.RanToCompletion || updateActivity?.Status < TaskStatus.RanToCompletion); }
        }
        public DashManifest(string url)
        {
            Uri = new Uri(
                url ?? throw new ArgumentNullException(
                    nameof(url), "Dash manifest url is empty."));

        }

        private string DownloadManifest()
        {
            Logger.Info($"Downloading Manifest {Uri}");

            using (var client = new HttpClient())
            {
                try
                {
                    var startTime = DateTime.Now;

                    HttpResponseMessage response = client.GetAsync(
                        Uri,
                        HttpCompletionOption.ResponseHeadersRead).Result;
                    response.EnsureSuccessStatusCode();
                    Logger.Info($"Downloading Manifest Done in {DateTime.Now - startTime} {Uri}");
                    return response.Content.ReadAsStringAsync().Result;
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        "Cannot download manifest file. Error: " + ex.Message);
                    return null;
                }
            }
        }
        private Document ParseManifest(string aManifest)
        {
            Logger.Info($"Parsing Manifest {Uri}");
            try
            {
                var startTime = DateTime.Now;
                var document = Document.FromText(
                    aManifest ?? throw new InvalidOperationException(
                        "Xml manifest is empty."),
                    Uri.ToString());

                Logger.Info($"Parsing Manifest Done in {DateTime.Now - startTime} {Uri}");
                return document;
            }
            catch (Exception ex)
            {
                Logger.Error(
                    "Cannot parse manifest file. Error: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        ///  Async method which works "as timer" for downloading MPD.
        /// </summary>
        /// <param name="reloadSchedule">delay which is to be before a download</param>
        /// <returns></returns>
        private async Task TimerEvent(DateTime reloadSchedule)
        {
            // If reloadTime is "now" or behing "now", do immediate schedule,
            // otherwise use provided value. Trigger is "delay from now" so get different from now to 
            // requested time in ms. Let's just hope different classes do not run in different 
            // time zones :D
            var reloadDelay = reloadSchedule - DateTime.UtcNow;

            //TaskScheduler.
            Logger.Info($"Manifest {Uri} reload in {reloadDelay}");

            if (reloadDelay < TimeSpan.Zero)
                reloadDelay = TimeSpan.Zero;


            // Wait specified time
            await Task.Delay(reloadDelay);

            var requestTime = DateTime.UtcNow;
            var XmlManifest = DownloadManifest();
            var downloadTime = DateTime.UtcNow;

            if (XmlManifest == null)
            {
                Logger.Info($"Manifest download failure {Uri}");
                OnManifestChanged(null);
                return;
            }


            var newDoc = ParseManifest(XmlManifest);
            var parseTime = DateTime.UtcNow;

            // Should we reschedule in case of parse failure?
            if (newDoc == null)
            {
                Logger.Info($"Manifest parse error {Uri}");
                OnManifestChanged(null);
                return;
            }

            newDoc.DownloadRequestTime = requestTime;
            newDoc.DownloadCompleteTime = downloadTime;
            newDoc.ParseCompleteTime = parseTime;
            reloadsRequired = newDoc.IsDynamic;

            if (updateActivity?.Status < TaskStatus.RanToCompletion)
            {
                Logger.Info($"Waiting for previous Manifest update to complete");
                await updateActivity;
            }

            OnManifestChanged(newDoc);

            // Reload schedule flag is released after manifest change notification
            // to block any manifest updates during this process
            reloadInfoPrinted = false;

            Interlocked.Exchange(ref reloadState, ReloadIdle);

        }

        /// <summary>
        /// Reloads manifest at provided DateTime in UTC form.
        /// Internally, method checks for a difference between UtcNow and provided 
        /// reload time. Difference is used as a timer. Caller HAS to assure reloadTime is
        /// provided in UTC.
        /// </summary>
        /// <param name="reloadTime">Manifest Reload Time in UTC</param>
        /// <returns>Task - Current awaitable reload task, NULL, update has been scheduled</returns>
        public bool ReloadManifest(DateTime reloadTime)
        {

            if (reloadsRequired == false)
            {
                if (reloadInfoPrinted == false)
                    Logger.Warn("Document Reload is disabled. Last Document loaded was static");

                reloadInfoPrinted = true;
                return false;
            }

            var reloaderState = Interlocked.CompareExchange(ref reloadState, ReloadRunning, ReloadIdle);
            if (reloaderState != ReloadIdle)
            {
                if (reloadInfoPrinted == false)
                    Logger.Info("Document Reload in progress. Ignoring request");

                reloadInfoPrinted = true;
                return false;
            }

            // There is no expectation of a scenario where
            // timer event is fired & someone plays with ReloadManifest.
            // as such no protections for now...
            reloadActivity = Task.Run(() => TimerEvent(reloadTime));

            return true;
        }

        private void OnManifestChanged(Object newDoc)
        {
            // OnManifestChanged is called from TimerEvent.
            // Handler may call ReloadManifest which in turn checks if update
            // has completed. As such, handler has to run "separately"
            // 
            // Caller may retrieve reload activity task and wait for completion before
            // calling ReloadManifest
            //
            if (ManifestChanged != null)
                updateActivity = Task.Run(() => ManifestChanged.Invoke(newDoc));
        }
    }
}

