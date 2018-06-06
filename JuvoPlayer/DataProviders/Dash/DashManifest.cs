using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using MpdParser;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashManifest
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private Uri Uri { get; }

        private readonly SemaphoreSlim updateInProgressLock = new SemaphoreSlim(1);
        private DateTime lastReloadTime = DateTime.MinValue;
        private TimeSpan minimumReloadPeriod = TimeSpan.Zero;

        public Document CurrentDocument { get; private set; }

        public DashManifest(string url)
        {
            Uri = new Uri(
                url ?? throw new ArgumentNullException(
                    nameof(url), "Dash manifest url is empty."));
        }

        public bool NeedsReload()
        {
            return CurrentDocument == null || (CurrentDocument.IsDynamic 
                   && (DateTime.UtcNow - lastReloadTime) >= minimumReloadPeriod);
        }

        public async Task ReloadManifestTask()
        {
            if (!updateInProgressLock.Wait(0))
                return;

            try
            {
                lastReloadTime = DateTime.UtcNow;

                var requestTime = DateTime.UtcNow;
                var xmlManifest = await DownloadManifest();
                var downloadTime = DateTime.UtcNow;

                if (xmlManifest == null)
                {
                    Logger.Info($"Manifest download failure {Uri}");
                    return;
                }

                var newDoc = await ParseManifest(xmlManifest);
                if (newDoc == null)
                {
                    Logger.Error($"Manifest parse error {Uri}");
                    return;
                }
                var parseTime = DateTime.UtcNow;
                newDoc.DownloadRequestTime = requestTime;
                newDoc.DownloadCompleteTime = downloadTime;
                newDoc.ParseCompleteTime = parseTime;

                minimumReloadPeriod = newDoc.MinimumUpdatePeriod ?? TimeSpan.MaxValue;

                CurrentDocument = newDoc;
            }
            finally
            {
                updateInProgressLock.Release();
            }
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
        private async Task<Document> ParseManifest(string aManifest)
        {
            Logger.Info($"Parsing Manifest {Uri}");
            try
            {
                var startTime = DateTime.Now;
                var document = await Document.FromText(
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
    }
}
