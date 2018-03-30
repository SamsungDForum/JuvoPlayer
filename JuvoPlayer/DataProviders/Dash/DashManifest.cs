using System;
using System.Net.Http;
using JuvoLogger;
using MpdParser;
using System.Threading.Tasks;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashManifest: IManifest
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);
        private bool reloadsRquired = true;

        /// <summary>
        /// Notification event. Will be called when MPD is updated.
        /// </summary>
        public event ManifestChanged ManifestChanged;
        public Uri Uri { get; set; }
        public string XmlManifest { get; set; }
        private bool reloadInfoPrinted = false;
        public Document Document { get; set; }

        /// <summary>
        /// Task as returned by TimerEvent responsible for 
        /// MPD download and parsing.
        /// </summary>
        private Task reloadActivity;

        public DashManifest(string url)
        {
            Uri = new Uri(
                url ?? throw new ArgumentNullException(
                    nameof(url), "Dash manifest url is empty."));
            
        }

        private string DownloadManifest()
        {
            Logger.Info($"Downloading Manifest {Uri}");

            try
            {
                var client = new HttpClient();
                DateTime st = DateTime.Now;

                HttpResponseMessage response = client.GetAsync(
                    Uri,
                    HttpCompletionOption.ResponseHeadersRead).Result;
                response.EnsureSuccessStatusCode();
                Logger.Info($"Downloading Manifest Done in {DateTime.Now - st} {Uri}");
                return response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                Logger.Error(
                    "Cannot download manifest file. Error: " + ex.Message);
                return null;
            }
        }
        private Document ParseManifest()
        {
            Logger.Info($"Parsing Manifest {Uri}");
            try
            {
                DateTime st = DateTime.Now;
                var document = Document.FromText(
                    XmlManifest ?? throw new InvalidOperationException(
                        "Xml manifest is empty."),
                    Uri.ToString());

                Logger.Info($"Parsing Manifest Done in {DateTime.Now - st} {Uri}");
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
        /// <param name="reloadDelay">delay which is to be before a download</param>
        /// <returns></returns>
        private async Task TimerEvent(Int32 reloadDelay=0)
        {
            // Wait specified time & detach from caller context.
            await Task.Delay(reloadDelay).ConfigureAwait(false);

            var requestTime = DateTime.UtcNow;
            XmlManifest = DownloadManifest();
            var downloadTime = DateTime.UtcNow;

            if (XmlManifest == null)
            {
                return;
            }
                

            var newDoc = ParseManifest();
            var parseTime = DateTime.UtcNow;

            // Should we reschedule in case of parse failure?
            if (newDoc == null)
            {
                return;
            }

            newDoc.DownloadRequestTime = requestTime;
            newDoc.DownloadCompleteTime = downloadTime;
            newDoc.ParseCompleteTime = parseTime;

            OnManifestChanged(newDoc);

            // Reload schedule flag is released after manifest change notification
            // to block any manifest updates during this process
            reloadInfoPrinted = false;


        }

        /// <summary>
        /// Reloads manifest at provided DateTime in UTC form.
        /// Internally, method checks for a difference between UtcNow and provided 
        /// reload time. Difference is used as a timer.
        /// </summary>
        /// <param name="reloadTime">Manifest Reload Time in UTC</param>
        /// <returns>True - reload request. False - reload not requested. Already Scheduled/Not dynamic</returns>
        public bool ReloadManifest(DateTime reloadTime)
        {
            
            if( reloadsRquired == false)
            {
                if(reloadInfoPrinted == false )
                    Logger.Warn("Document Reload is disabled. Last Document loaded was static");

                reloadInfoPrinted = true;
                return false;
            }

            //Reload activity will be null at very start
            if (reloadActivity?.Status < TaskStatus.RanToCompletion)
            {
                if (reloadInfoPrinted == false)
                    Logger.Info("Document Reload already scheduled. Ignoring request");

                reloadInfoPrinted = true;
                return false;
            }

            // If reloadTime is "now" or behing "now", do immediate schedule,
            // otherwise use provided value. Trigger is "delay from now" so get different from now to 
            // requested time in ms. Let's just hope different classes do not run in different 
            // time zones :D
            DateTime current = DateTime.UtcNow;
            TimeSpan timediff = reloadTime - current;
            Int32 reloadDelay = timediff.Milliseconds;

            if (reloadDelay < 0)
                reloadDelay = 0;

            //TaskScheduler.
            Logger.Info($"Manifest {Uri} reload in {reloadDelay}ms");

            // There is no expectation of a scenario where
            // timer event is fired & someone plays with ReloadManifest.
            // as such no protections for now...
            reloadActivity = TimerEvent(reloadDelay);
            return true;
        }

        private void OnManifestChanged(IDocument newDoc)
        {
            ManifestChanged?.Invoke(newDoc);
        }
    }
}
