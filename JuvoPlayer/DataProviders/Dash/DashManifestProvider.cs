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
    public delegate void Play();

    internal class DashManifestException : Exception
    {
        public DashManifestException(string message) : base(message) { }
    }

    internal class DashManifestProvider : IDisposable
    {
        private const string Tag = "JuvoPlayer";
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger(Tag);

        public event StreamError StreamError;
        public event ClipDurationChanged ClipDurationChanged;
        public event Play Play;

        public readonly DashManifest Manifest;
        private Task manifestFeedTask;
        private CancellationTokenSource manifestFeedCts;
        
        private readonly List<SubtitleInfo> subtitleInfos = new List<SubtitleInfo>();

        private readonly DashMediaPipeline audioPipeline;
        private readonly DashMediaPipeline videoPipeline;

        public TimeSpan CurrentTime { get; set; } = TimeSpan.Zero;

        private bool isDisposed;

        public DashManifestProvider(DashManifest manifest, DashMediaPipeline audioPipeline, DashMediaPipeline videoPipeline )
        {
            this.Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest), "manifest cannot be null");

            this.audioPipeline = audioPipeline;
            this.videoPipeline = videoPipeline;
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
                var res = new SubtitleInfo(subtitleInfos[description.Id]);
                return res;
            }
        }

        private async Task LoadManifestAsync(CancellationToken token, TimeSpan reloadDelay)
        {
            if (reloadDelay != TimeSpan.Zero)
            {
                Logger.Info($"Manifest reload in {reloadDelay}");
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

            // If no period has been found, continue reloading untill a valid period has been found, regardless of static/dynamic content
            if (tmpPeriod == null)
                Logger.Warn($"Failed to find period for WallClock {wallClock}");
            

            return tmpPeriod;
        }

        private bool ApplyManifest(CancellationToken token, Period newPerdiod)
        {
            token.ThrowIfCancellationRequested();

            // Set new media from obtained period information
            return SetMediaFromPeriod(Manifest.CurrentDocument, newPerdiod);
        }


        private void ProcessManifest(CancellationToken token)
        {
            Logger.Info("");

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

            if (Manifest.HasChanged)
            {
                // Set period with Manifest Parameters. Will be applied to ALL media within period.
                //
                var manifestParams = new ManifestParameters(Manifest.CurrentDocument, tmpPeriod)
                {
                    PlayClock = Manifest.CurrentDocument.WallClock(CurrentTime)
                };
                tmpPeriod.SetManifestParameters(manifestParams);

                // Apply new manifest
                //
                // Sucess   = Start of playback.
                // Failure  = Static Content: Termination signalled by exception. 
                //            Dynamic content: Do manifest reload and force "has changed" flag to be set
                //            to allow re-application of manifest regardless of its content change.
                //
                if (ApplyManifest(token, tmpPeriod))
                {
                    token.ThrowIfCancellationRequested();
                    Play.Invoke();
                }
                else
                {
                    if(!Manifest.CurrentDocument.IsDynamic)
                        throw new DashManifestException("Failed to apply new manifest");

                    Logger.Warn("Failed to apply Manifest. Retrying.");
                    Manifest.ForceHasChangedOnNextReload();
                }
            }
        }

        public void Stop()
        {
            // Don't do double termination. Nothing  bad will happen but there is no point :)
            if (manifestFeedTask == null )
                return;

            Logger.Info("Terminating DashManifestProvider");
            manifestFeedCts?.Cancel();

            try
            {
                if (manifestFeedTask?.Status > TaskStatus.Created)
                {
                    Logger.Info($"Awaiting manifestTask completion {manifestFeedTask?.Status}");
                    manifestFeedTask?.Wait();
                }
            }
            catch (AggregateException) { }
            finally
            {
                manifestFeedCts?.Dispose();
                manifestFeedCts = null;
                manifestFeedTask = null;
                Logger.Info("DashManifestProvider terminated.");
            }
        }

        private void StartManifestTask(CancellationToken token, TimeSpan delay)
        {
            manifestFeedTask = Task.Factory.StartNew(async () =>
            {
                bool repeatFeed = false;

                try
                {
                    await LoadManifestAsync(token, delay);
                    ProcessManifest(token);

                    // Dynamic content - Keep on reloading manifest
                    //
                    repeatFeed = Manifest.CurrentDocument.IsDynamic;
                        
                }
                catch (DashManifestException dme)
                {
                    // Dynamic content - if we have previously obtained a manifest
                    // repeat downloads even if failures occour. If failure occoured
                    // before any previous downloads - report it.
                    // For static, do always.
                    //
                    repeatFeed = Manifest?.CurrentDocument.IsDynamic ?? false;

                    if (!repeatFeed)
                        StreamError?.Invoke(dme.Message);
                }
                catch (OperationCanceledException)
                {
                    Logger.Info("Maniest download cancelled");
                    repeatFeed = false;
                }
                catch(Exception e)
                {
                    // Report all other exceptions to screen
                    // as they silently terminate task to fail state
                    //
                    Logger.Error(e.ToString());
                    repeatFeed = false;
                }
                
                if (repeatFeed)
                    StartManifestTask(token, Manifest.GetReloadDueTime());

            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Start()
        {
            // Start reloader.
            //
            manifestFeedCts?.Dispose();
            manifestFeedCts = new CancellationTokenSource();
            var cancelToken = manifestFeedCts.Token;
            
            // Starts manifest download on manifestTask
            StartManifestTask(cancelToken, TimeSpan.Zero);
           
        }

        private bool SetMediaFromPeriod(Document document, Period period)
        {
            Logger.Info(period.ToString());

            var audioMedia = period.GetMedia(MediaType.Audio);
            var videoMedia = period.GetMedia(MediaType.Video);

            if (audioMedia == null || videoMedia == null)
            {
                Logger.Error($"Failed to initialize media. Video {videoMedia != null} Audio {audioMedia != null}");
                return false;
            }

            audioPipeline.UpdateMedia(audioMedia);
            videoPipeline.UpdateMedia(videoMedia);

            var audioRepresentation = audioPipeline.GetRepresentation();
            var videoRepresentation = videoPipeline.GetRepresentation();

            if (audioRepresentation == null || videoRepresentation == null)
            {
                Logger.Error($"Failed to prepare A/V streams. Video {videoRepresentation != null} Audio {audioRepresentation != null}");
                return false;
            }

            BuildSubtitleInfos(period);

            if (document.MediaPresentationDuration.HasValue && document.MediaPresentationDuration.Value > TimeSpan.Zero)
                ClipDurationChanged?.Invoke(document.MediaPresentationDuration.Value);


            audioRepresentation.AlignStartSegmentsWith(videoRepresentation);
           
            Logger.Info($"Segment Alignment: Video={videoRepresentation.AlignedStartSegmentID} Audio={audioRepresentation.AlignedStartSegmentID} TrimmOffset={videoRepresentation.AlignedTrimmOffset}");
            return true;
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
        }
    }
}
