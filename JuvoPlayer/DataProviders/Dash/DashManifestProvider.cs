// Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using JuvoLogger;
using JuvoPlayer.Common;
using MpdParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JuvoPlayer.DataProviders.Dash
{
    public delegate void ManifestReady();

    internal class DashManifestException : Exception
    {
        public DashManifestException(string message) : base(message) { }
    }

    internal class DashManifestProvider : IDisposable
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger(Tag);

        public event StreamError StreamError;
        public event ClipDurationChanged ClipDurationChanged;
        public event ManifestReady ManifestReady;

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
            ManifestReady?.Invoke();
        }

        private void NotifyDurationChange()
        {
            var duration = Manifest.CurrentDocument.MediaPresentationDuration;

            logger.Info($"{duration}");
            if (duration.HasValue && duration.Value > TimeSpan.Zero)
                ClipDurationChanged?.Invoke(duration.Value);
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
                        StreamError?.Invoke(dme.Message);

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
                        StreamError?.Invoke(ae.Message);

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

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            // Stop handles clearing of task/cancellation tokens.
            Stop();
            Manifest.Dispose();
            Manifest = null;
        }
    }
}
