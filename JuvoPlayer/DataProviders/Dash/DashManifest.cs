/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using MpdParser;
using static Configuration.DashManifest;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashManifest : IDisposable
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        private Uri Uri { get; }

        private readonly HttpClient httpClient = new HttpClient
        {
            Timeout = DownloadTimeout
        };

        public Document CurrentDocument { get; private set; }

        public bool HasChanged { get; private set; }

        private DateTime? publishTime;

        public DashManifest(string url)
        {
            Uri = new Uri(
                url ?? throw new ArgumentNullException(
                    nameof(url), "Dash manifest url is empty."));
        }

        /// <summary>
        /// Function resets document publish time, effectively forcing next reloaded document to be
        /// processed regardless of publishTime change.
        /// </summary>
        public void ForceHasChangedOnNextReload()
        {
            publishTime = null;
        }

        public TimeSpan GetReloadDueTime()
        {
            // No doc, use default
            if (CurrentDocument == null)
                return ManifestReloadDelay;

            var reloadTime = CurrentDocument.MinimumUpdatePeriod ?? ManifestReloadDelay;

            // For zero minimum update periods (aka, after every chunk) use default reload.
            if (reloadTime == TimeSpan.Zero)
                reloadTime = ManifestReloadDelay;

            return reloadTime;
        }

        public async Task<bool> ReloadManifestTask(CancellationToken cancelToken)
        {
            var downloadRetries = MaxManifestDownloadRetries;
            Document newDoc = null;
            DateTime requestTime = DateTime.MinValue;
            DateTime downloadTime = DateTime.MinValue;
            DateTime parseTime = DateTime.MinValue;

            HasChanged = false;

            do
            {
                cancelToken.ThrowIfCancellationRequested();

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

                    Logger.Error($"Manifest parse error {Uri}");
                }
                else
                {
                    Logger.Warn($"Manifest download failure {Uri}");
                }

                cancelToken.ThrowIfCancellationRequested();

                if (downloadRetries > 0)
                {
                    await Task.Delay(ManifestDownloadDelay, cancelToken);
                    cancelToken.ThrowIfCancellationRequested();
                }

            } while (downloadRetries-- > 0);

            // If doc is null, max # of retries have been reached with no success
            if (newDoc == null)
                return false;

            newDoc.DownloadRequestTime = requestTime;
            newDoc.DownloadCompleteTime = downloadTime;
            newDoc.ParseCompleteTime = parseTime;

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
                Logger.Error(ex, "Cannot download manifest file");
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
                Logger.Error(ex, "Cannot parse manifest file");
                return null;
            }
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
