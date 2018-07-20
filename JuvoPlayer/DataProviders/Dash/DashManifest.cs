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

        private readonly SemaphoreSlim updateInProgressLock = new SemaphoreSlim(1);
        private readonly HttpClient httpClient = new HttpClient {Timeout = TimeSpan.FromSeconds(3)};

        private CancellationTokenSource cancellationTokenSource;

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

        public void CancelReload()
        {
            try
            {
                cancellationTokenSource?.Cancel();
            }
            catch
            {
                // ignored
            }
        }

        public async Task<bool> ReloadManifestTask()
        {
            cancellationTokenSource = new CancellationTokenSource();
            var ct = cancellationTokenSource.Token;

            OperationCanceledException toReThrow = null;

            try
            {
                ct.ThrowIfCancellationRequested();
                lastReloadTime = DateTime.UtcNow;

                var requestTime = DateTime.UtcNow;
                var xmlManifest = await DownloadManifest(ct);
                var downloadTime = DateTime.UtcNow;

                if (xmlManifest == null)
                {
                    ct.ThrowIfCancellationRequested();
                    Logger.Info($"Manifest download failure {Uri}");
                    return false;
                }

                var newDoc = await ParseManifest(xmlManifest);
                if (newDoc == null)
                {
                    ct.ThrowIfCancellationRequested();
                    Logger.Error($"Manifest parse error {Uri}");
                    return false;
                }

                ct.ThrowIfCancellationRequested();

                var parseTime = DateTime.UtcNow;
                newDoc.DownloadRequestTime = requestTime;
                newDoc.DownloadCompleteTime = downloadTime;
                newDoc.ParseCompleteTime = parseTime;

                minimumReloadPeriod = newDoc.MinimumUpdatePeriod ?? TimeSpan.MaxValue;

                CurrentDocument = newDoc;

                return true;
            }
            catch (OperationCanceledException e)
            {
                // Catch needed!
                // try/finally without a catch may result in lack of finally block
                // execution (depends on usage scenario and we need it here!)
                // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/try-finally
                //
                toReThrow = e;
            }
            finally
            {
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;

                if (toReThrow != null)
                    throw toReThrow;
            }

            if (toReThrow != null)
                return false;

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
            // Disposing need to be done only when AvailableWaitHandle is used
            // We dont use it so, dont dispose lock to avoid exceptions when releasing 
            // semaphore in other thread
            cancellationTokenSource?.Dispose();
        }
    }
}
