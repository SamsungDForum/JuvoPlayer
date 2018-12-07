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

using JuvoLogger;
using JuvoPlayer.Common;
using MpdParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace JuvoPlayer.DataProviders.Dash
{
    internal class DashManifestException : Exception
    {
        public DashManifestException(string message) : base(message) { }
    }

    internal class DashManifestProvider : IDisposable
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger(Tag);

        private readonly Subject<string> streamErrorSubject = new Subject<string>();
        private readonly Subject<TimeSpan> clipDurationSubject = new Subject<TimeSpan>();
        private readonly Subject<Unit> manifestReadySubject = new Subject<Unit>();

        public DashManifest Manifest { get; internal set; }
        private Task manifestFeedTask;
        private CancellationTokenSource manifestFeedCts;
        private readonly Object startStopLock = new Object();

        private readonly List<SubtitleInfo> subtitleInfos = new List<SubtitleInfo>();

        private readonly DashMediaPipeline audioPipeline;
        private readonly DashMediaPipeline videoPipeline;

        public TimeSpan CurrentTime { get; set; } = TimeSpan.Zero;

        private bool isDisposed;

        public DashManifestProvider(DashManifest manifest, DashMediaPipeline audioPipeline, DashMediaPipeline videoPipeline)
        {
            this.Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest), "manifest cannot be null");

            this.audioPipeline = audioPipeline;
            this.videoPipeline = videoPipeline;
        }

        public List<StreamDescription> GetSubtitlesDescription()
        {
            lock (subtitleInfos)
            {
                return subtitleInfos.Select(info => info.ToStreamDescription()).ToList();
            }
        }

        public SubtitleInfo GetSubtitleInfo(StreamDescription description)
        {
            // Subtitle Infos gets cleared/updated during manifest reloads
            // no locks = possible false positives when checking subtitleInfos.Count.
            //
            lock (subtitleInfos)
            {
                if (description.Id >= subtitleInfos.Count)
                    throw new ArgumentException("Invalid subtitle description");

                // Create a copy!
                return new SubtitleInfo(subtitleInfos[description.Id]);
            }
        }

        private async Task LoadManifestAsync(CancellationToken token, TimeSpan reloadDelay)
        {
            if (reloadDelay != TimeSpan.Zero)
            {
                logger.Info($"Manifest reload in {reloadDelay}");
                await Task.Delay(reloadDelay, token);
            }

            var haveManifest = await Manifest.ReloadManifestTask(token);
            if (!haveManifest)
            {
                token.ThrowIfCancellationRequested();
                throw new DashManifestException("Manifest download failure");
            }
        }

        private Period GetPeriod(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            // Get Current period
            var wallClock = Manifest.CurrentDocument.WallClock(CurrentTime);
            var tmpPeriod = Manifest.CurrentDocument.FindPeriod(wallClock);

            // If no period has been found, continue reloading until a valid period has been found, regardless of static/dynamic content
            if (tmpPeriod == null)
                logger.Warn($"Failed to find period for WallClock {wallClock}");


            return tmpPeriod;
        }

        private void ApplyPeriod(CancellationToken token, Period newPeriod)
        {
            // Set new media from obtained period information
            SetMediaFromPeriod(newPeriod, token);
        }

        private void ProcessManifest(CancellationToken token)
        {
            logger.Info("");

            token.ThrowIfCancellationRequested();

            var tmpPeriod = GetPeriod(token);
            if (tmpPeriod == null)
            {
                // If we do not have a period, force HasChanged flag to be set
                // regardless of document publish time.
                //
                Manifest.ForceHasChangedOnNextReload();
                return;
            }

            if (!Manifest.HasChanged)
                return;

            // Set period with Manifest Parameters. Will be applied to ALL media within period.
            //
            var manifestParams = new ManifestParameters(Manifest.CurrentDocument, tmpPeriod)
            {
                PlayClock = Manifest.CurrentDocument.WallClock(CurrentTime)
            };

            tmpPeriod.SetManifestParameters(manifestParams);

            // Apply new manifest
            //
            // Success  = Start of playback.
            // Failure  = Static Content: Termination signaled by exception.
            //            Dynamic content: Do manifest reload and force "has changed" flag to be set
            //            to allow re-application of manifest regardless of its content change.
            //
            // Manageable errors will be notified by ArgumentException and handled by the caller.
            //
            ApplyPeriod(token, tmpPeriod);
            NotifyDurationChange();
            BuildSubtitleInfos(tmpPeriod);
            token.ThrowIfCancellationRequested();
            manifestReadySubject.OnNext(Unit.Default);
        }

        private void NotifyDurationChange()
        {
            var duration = Manifest.CurrentDocument.MediaPresentationDuration;

            logger.Info($"{duration}");
            if (duration.HasValue && duration.Value > TimeSpan.Zero)
                clipDurationSubject.OnNext(duration.Value);
        }

        public void Stop()
        {
            lock (startStopLock)
            {
                // Don't do double termination. Nothing  bad will happen but there is no point :)
                if (manifestFeedTask == null)
                    return;

                logger.Info("Terminating DashManifestProvider");
                manifestFeedCts?.Cancel();

                try
                {
                    if (manifestFeedTask?.Status > TaskStatus.Created)
                    {
                        logger.Info($"Awaiting manifestTask completion {manifestFeedTask?.Status}");
                        manifestFeedTask?.Wait();
                    }
                }
                catch (AggregateException)
                {
                }
                finally
                {
                    manifestFeedCts?.Dispose();
                    manifestFeedCts = null;
                    manifestFeedTask = null;
                    logger.Info("DashManifestProvider terminated.");
                }
            }
        }

        private async Task ManifestFeedProcess(CancellationToken token, TimeSpan delay)
        {
            var repeatFeed = false;
            var currentDelay = delay;

            do
            {
                try
                {
                    await LoadManifestAsync(token, currentDelay);
                    ProcessManifest(token);

                    // Dynamic content - Keep on reloading manifest
                    //
                    repeatFeed = Manifest.CurrentDocument.IsDynamic;

                }
                catch (DashManifestException dme)
                {
                    // Dynamic content - if we have previously obtained a manifest
                    // repeat downloads even if failures occur. If failure occured
                    // before any previous downloads - report it.
                    // For static, do always.
                    //
                    repeatFeed = Manifest?.CurrentDocument?.IsDynamic ?? false;

                    if (!repeatFeed)
                        streamErrorSubject.OnNext(dme.Message);

                    Manifest.ForceHasChangedOnNextReload();
                }
                catch (OperationCanceledException)
                {
                    logger.Info("Manifest download cancelled");
                    repeatFeed = false;
                }
                catch (ArgumentException ae)
                {
                    // When ApplyPeriod() in ProcessManifest() fails in a predictable way, ArgumentException will be thrown
                    // Dynamic Docs - keep on running.
                    // Static Docs - Notify failure and exit.
                    repeatFeed = Manifest?.CurrentDocument.IsDynamic ?? false;

                    if (!repeatFeed)
                        streamErrorSubject.OnNext(ae.Message);

                    logger.Warn("Failed to apply Manifest. Retrying. Failure: " + ae.Message);

                    Manifest.ForceHasChangedOnNextReload();
                }
                catch (Exception e)
                {
                    // Report all other exceptions to screen
                    // as they silently terminate task to fail state
                    //
                    logger.Error(e.ToString());
                    repeatFeed = false;
                }

                currentDelay = Manifest.GetReloadDueTime();

            } while (repeatFeed);
        }

        private void StartManifestTask(CancellationToken token, TimeSpan delay)
        {
            manifestFeedTask = Task.Factory.StartNew(() => ManifestFeedProcess(token, delay), token,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Start()
        {
            lock (startStopLock)
            {
                // Double start prevention.
                if (manifestFeedTask != null)
                    return;

                logger.Info("Starting DashManifestProvider");

                // Start reloader.
                //
                manifestFeedCts?.Dispose();
                manifestFeedCts = new CancellationTokenSource();
                var cancelToken = manifestFeedCts.Token;

                // Starts manifest download on manifestTask
                StartManifestTask(cancelToken, TimeSpan.Zero);
            }
        }

        private void SetMediaFromPeriod(Period period, CancellationToken token)
        {
            logger.Info(period.ToString());

            // NOTE: UpdateMedia is potentially a time consuming operation
            // Case: BaseRepresentation with downloadable index data.
            // Index download is synchronous now and blocking.
            //
            // Possible workarounds (not to be mistaken with solution) would
            // be use of Parallel.Invoke() to improve performance of UpdateMedia calls
            //
            audioPipeline.UpdateMedia(period);
            token.ThrowIfCancellationRequested();

            videoPipeline.UpdateMedia(period);
            token.ThrowIfCancellationRequested();

            audioPipeline.SynchronizeWith(videoPipeline);
        }

        private void BuildSubtitleInfos(Period period)
        {
            // Prevent simultaneous access to GetSubtitleInfo() / subtitleInfos.Clear()
            //
            lock (subtitleInfos)
            {
                subtitleInfos.Clear();

                var textAdaptationSets = period.Sets.Where(o => o.Type.Value == MediaType.Text).ToList();
                foreach (var textAdaptationSet in textAdaptationSets)
                {
                    var lang = textAdaptationSet.Lang;
                    var mimeType = textAdaptationSet.Type;
                    foreach (var representation in textAdaptationSet.Representations)
                    {
                        var mediaSegments = representation.Segments.MediaSegments().ToList();
                        if (!mediaSegments.Any()) continue;

                        var segment = mediaSegments.First();
                        var streamDescription = new SubtitleInfo
                        {
                            Id = subtitleInfos.Count,
                            Language = lang,
                            Path = segment.Url.ToString(),
                            MimeType = mimeType?.Key
                        };

                        subtitleInfos.Add(streamDescription);
                    }
                }
            }
        }

        public IObservable<string> StreamError()
        {
            return streamErrorSubject.AsObservable();
        }

        public IObservable<TimeSpan> ClipDurationChanged()
        {
            return clipDurationSubject.AsObservable();
        }

        public IObservable<Unit> ManifestReady()
        {
            return manifestReadySubject.AsObservable();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            streamErrorSubject.Dispose();
            clipDurationSubject.Dispose();
            manifestReadySubject.Dispose();

            // Stop handles clearing of task/cancellation tokens.
            Stop();
            Manifest.Dispose();
            Manifest = null;
        }
    }
}
