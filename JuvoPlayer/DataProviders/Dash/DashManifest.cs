using System;
using System.Net.Http;
using JuvoLogger;
using MpdParser;
using System.Threading.Tasks;
using System.Threading;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashManifest: IManifest
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);
        private bool reloadsRequired = true;

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
        private Document ParseManifest()
        {
            Logger.Info($"Parsing Manifest {Uri}");
            try
            {
                var startTime = DateTime.Now;
                var document = Document.FromText(
                    XmlManifest ?? throw new InvalidOperationException(
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
        private void TimerEvent(DateTime reloadSchedule)
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
            Task.Delay(reloadDelay).Wait();

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

            reloadActivity = null;

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
            
            if(reloadsRequired == false)
            {
                if(reloadInfoPrinted == false )
                    Logger.Warn("Document Reload is disabled. Last Document loaded was static");

                reloadInfoPrinted = true;
                return false;
            }

            // Reload activity will be null at very start / termination
            // There is a tiny "hole" between this point and creation of reloadActivity.
            // Problem? Should not be...
            if (reloadActivity != null)
            {
                if (reloadInfoPrinted == false)
                    Logger.Info("Document Reload already scheduled. Ignoring request");

                reloadInfoPrinted = true;
                return false;
            }

            // There is no expectation of a scenario where
            // timer event is fired & someone plays with ReloadManifest.
            // as such no protections for now...
            reloadActivity = Task.Run(()=>TimerEvent(reloadTime));

            return true;
        }

        private void OnManifestChanged(IDocument newDoc)
        {
            ManifestChanged?.Invoke(newDoc);
        }
    }
}
