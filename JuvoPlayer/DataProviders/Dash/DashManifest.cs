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

        private Task updateActivity;

        // int because Interlocked.CompareExchange doesn't support Enums
        private const int ReloadIdle = 0;
        private const int ReloadRunning = 1;
        private int reloadState = ReloadIdle;

        public Task GetReloadManifestActivity { get; private set; }

        public bool IsReloadInProgress => GetReloadManifestActivity?.IsCompleted == false || updateActivity?.IsCompleted == false;

        public DashManifest(string url)
        {
            Uri = new Uri(
                url ?? throw new ArgumentNullException(
                    nameof(url), "Dash manifest url is empty."));

        }

        private async Task<string> DownloadManifest()
        {
            Logger.Info($"Downloading Manifest {Uri}");

            using (var client = new HttpClient())
            {
                try
                {
                    var startTime = DateTime.Now;

                    var response = await client.GetAsync(Uri, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    Logger.Info($"Downloading Manifest Done in {DateTime.Now - startTime} {Uri}");

                    var result = await response.Content.ReadAsStringAsync();
                    return result;
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
        /// <returns></returns>
        private async void ReloadManifestEvent()
        {
            var requestTime = DateTime.UtcNow;
            var xmlManifest = await DownloadManifest();
            var downloadTime = DateTime.UtcNow;

            if (xmlManifest == null)
            {
                Logger.Info($"Manifest download failure {Uri}");
                OnManifestChanged(null);
                return;
            }

            var newDoc = ParseManifest(xmlManifest);
            var parseTime = DateTime.UtcNow;

            // Should we reschedule in case of parse failure?
            if (newDoc == null)
            {
                Logger.Error($"Manifest parse error {Uri}");
                OnManifestChanged(null);
                return;
            }

            newDoc.DownloadRequestTime = requestTime;
            newDoc.DownloadCompleteTime = downloadTime;
            newDoc.ParseCompleteTime = parseTime;
            reloadsRequired = newDoc.IsDynamic;

            if (updateActivity?.IsCompleted == false)
            {
                Logger.Info("Waiting for previous Manifest update to complete");
                await updateActivity;
            }

            OnManifestChanged(newDoc);

            // Reload schedule flag is released after manifest change notification
            // to block any manifest updates during this process
            reloadInfoPrinted = false;

            Interlocked.Exchange(ref reloadState, ReloadIdle);
        }

        /// <inheritdoc />
        /// <summary>
        /// Reloads manifest at provided DateTime in UTC form.
        /// Internally, method checks for a difference between UtcNow and provided 
        /// reload time. Difference is used as a timer. Caller HAS to assure reloadTime is
        /// provided in UTC.
        /// </summary>
        /// <param name="reloadTime">Manifest Reload Time in UTC</param>
        /// <returns>Task - Current awaitable reload task, NULL, update has been scheduled</returns>
        public void ReloadManifest(DateTime reloadTime)
        {
            if (!reloadsRequired)
            {
                LogReloadMessage("Document Reload is disabled. Last Document loaded was static");
                return;
            }

            var reloaderState = Interlocked.CompareExchange(ref reloadState, ReloadRunning, ReloadIdle);
            if (reloaderState != ReloadIdle)
            {
                LogReloadMessage("Document Reload in progress. Ignoring request");
                return;
            }

            // There is no expectation of a scenario where
            // timer event is fired & someone plays with ReloadManifest.
            // as such no protections for now...
            var reloadDelay = reloadTime - DateTime.UtcNow;
            if (reloadDelay < TimeSpan.Zero)
                reloadDelay = TimeSpan.Zero;

            Logger.Info($"Manifest {Uri} reload in {reloadDelay}");

            GetReloadManifestActivity = Task.Delay(reloadDelay).ContinueWith(_ => ReloadManifestEvent());
        }

        private void LogReloadMessage(string message)
        {
            if (reloadInfoPrinted == false)
                Logger.Info(message);

            reloadInfoPrinted = true;
        }

        private void OnManifestChanged(object newDoc)
        {
            // OnManifestChanged is called from ReloadManifestEvent.
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

