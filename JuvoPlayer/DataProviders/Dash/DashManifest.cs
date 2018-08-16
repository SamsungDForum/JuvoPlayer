using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using MpdParser;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashManifest : IDisposable
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private Uri Uri { get; }

        private readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        private DateTime lastReloadTime = DateTime.MinValue;
        private TimeSpan minimumReloadPeriod = TimeSpan.Zero;

        public Document CurrentDocument { get; private set; }

        public bool HasChanged { get; private set; }

        private DateTime? publishTime = null;
        private static readonly int maxManifestDownloadRetries = 3;
        private static readonly TimeSpan manifestDownloadDelay = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan manifestReloadDelay = TimeSpan.FromMilliseconds(1500);

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

        /// <summary>
        /// Function resets document publish time, effectively forcing next reloaded document to be 
        /// processed regardless of publishTime change.
        /// </summary>
        public void ResetPublishTime()
        {
            publishTime = null;
        }

        public TimeSpan? GetReloadDueTime()
        {
            // No doc, use default
            if (CurrentDocument == null)
                return manifestReloadDelay;

            // Doc is static, return -1 to disable reload Timer
            if (!CurrentDocument.IsDynamic)
                return null;

            var reloadTime = CurrentDocument.MinimumUpdatePeriod ?? manifestReloadDelay;

            // For zero minimum update periods (aka, after every chunk) use default reload.
            if (reloadTime == TimeSpan.Zero)
                reloadTime = manifestReloadDelay;

            return reloadTime;
        }

        public async Task<bool> ReloadManifestTask(CancellationToken cancelToken)
        {
            var downloadRetries = maxManifestDownloadRetries;
            Document newDoc = null;
            DateTime requestTime = DateTime.MinValue;
            DateTime downloadTime = DateTime.MinValue;
            DateTime parseTime = DateTime.MinValue;

            HasChanged = false;

            do
            {
                cancelToken.ThrowIfCancellationRequested();
                lastReloadTime = DateTime.UtcNow;

                requestTime = DateTime.UtcNow;
                var xmlManifest = await DownloadManifest(cancelToken);
                downloadTime = DateTime.UtcNow;

                if (xmlManifest != null)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    newDoc = await ParseManifest(xmlManifest);
                    parseTime = DateTime.UtcNow;

                    if (newDoc != null)
                    {
                        break;
                    }
                    else
                    {
                        Logger.Error($"Manifest parse error {Uri}");
                    }
                }
                else
                {
                    Logger.Info($"Manifest download failure {Uri}");
                }

                cancelToken.ThrowIfCancellationRequested();

                if (downloadRetries > 0)
                {
                    await Task.Delay(manifestDownloadDelay, cancelToken);
                    cancelToken.ThrowIfCancellationRequested();
                }

            } while (downloadRetries-- > 0);

            // Done our
            if (newDoc == null)
                return false;

            newDoc.DownloadRequestTime = requestTime;
            newDoc.DownloadCompleteTime = downloadTime;
            newDoc.ParseCompleteTime = parseTime;

            minimumReloadPeriod = newDoc.MinimumUpdatePeriod ?? TimeSpan.MaxValue;

            CurrentDocument = newDoc;

            // Manifests without publish time are "uncheckable" for updates, assume
            // always change
            if (!publishTime.HasValue || !CurrentDocument.PublishTime.HasValue || CurrentDocument.PublishTime > publishTime)
            {
                publishTime = CurrentDocument.PublishTime;
                HasChanged = true;
            }

            return true;
        }

        private async Task<string> DownloadManifest(CancellationToken ct)
        {
            Logger.Info($"Downloading Manifest {Uri}");

            try
            {
                var startTime = DateTime.Now;

                using (var response = await httpClient.GetAsync(Uri, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    Logger.Info($"Downloading Manifest Done in {DateTime.Now - startTime} {Uri}");

                    var result = await response.Content.ReadAsStringAsync();
                    return result;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(
                    "Cannot download manifest file. Error: " + ex.Message);
                return null;
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

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
