/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;
using JuvoPlayer.Platforms.Tizen;
using Nito.AsyncEx;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.Tests
{
    [TestFixture]
    public class DashPlayerTests
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");
        private static readonly TimeSpan CatchupPlaybackTimeout = TimeSpan.FromSeconds(14);
        private static readonly TimeSpan BufferingEndTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan EosTimeout = TimeSpan.FromSeconds(14);

        public struct Clip
        {
            public string MpdUri { get; set; }
            public DrmDescription? DrmDescription { get; set; }

            public override string ToString()
            {
                return MpdUri;
            }
        }

        public struct DrmDescription
        {
            public string KeySystem { get; set; }
            public string LicenseServerUri { get; set; }
            public Dictionary<string, string> RequestHeaders { get; set; }
        }

        public static Clip[] Clips()
        {
            return new[]
            {
                new Clip
                {
                    MpdUri = "http://106.120.45.49/googlecar/car-20120827-manifest.mpd",
                },
                new Clip
                {
                    MpdUri = "http://106.120.45.49/sintel-dash/sintel.mpd"
                },
                new Clip
                {
                    MpdUri = "http://106.120.45.49/formula1/Manifest.mpd"
                },
                new Clip
                {
                    MpdUri = "http://106.120.45.49/paris/dashevc-live-2s-4k.mpd"
                },
                new Clip
                {
                    MpdUri = "http://106.120.45.49/nexusq/oops_cenc-20121114-signedlicenseurl-manifest.mpd",
                    DrmDescription = new DrmDescription
                    {
                        KeySystem = "com.microsoft.playready",
                        LicenseServerUri =
                            "https://dash-mse-test.appspot.com/api/drm/playready?drm_system=playready&source=YOUTUBE&ip=0.0.0.0&ipbits=0&expire=19000000000&sparams=ip,ipbits,expire,drm_system,source,video_id&video_id=03681262dc412c06&signature=448279561E2755699618BE0A2402189D4A30B03B.0CD6A27286BD2DAF00577FFA21928665DCD320C2&key=test_key1",
                        RequestHeaders = new Dictionary<string, string>
                        {
                            ["Content-Type"] = "text/xml; charset=utf-8"
                        }
                    }
                },
                new Clip
                {
                    MpdUri = "http://106.120.45.49/nexusq/oops_cenc-20121114-signedlicenseurl-manifest.mpd",
                    DrmDescription = new DrmDescription
                    {
                        KeySystem = "com.widevine.alpha",
                        LicenseServerUri =
                            "https://dash-mse-test.appspot.com/api/drm/widevine?drm_system=widevine&source=YOUTUBE&ip=0.0.0.0&ipbits=0&expire=19000000000&key=test_key1&sparams=ip,ipbits,expire,drm_system,source,video_id&video_id=03681262dc412c06&signature=9C4BE99E6F517B51FED1F0B3B31966D3C5DAB9D6.6A1F30BB35F3A39A4CA814B731450D4CBD198FFD",
                        RequestHeaders = new Dictionary<string, string>
                        {
                            ["Content-Type"] = "text/xml; charset=utf-8"
                        }
                    }
                },
                new Clip
                {
                    MpdUri = "http://106.120.45.49/art-of-motion/manifest/11331.mpd",
                    DrmDescription = new DrmDescription
                    {
                        KeySystem = "com.widevine.alpha",
                        LicenseServerUri =
                            "https://widevine-proxy.appspot.com/proxy",
                        RequestHeaders = new Dictionary<string, string>
                        {
                            ["Content-Type"] = "text/xml; charset=utf-8"
                        }
                    }
                },
                new Clip
                {
                    MpdUri = "http://106.120.45.49/tears_of_steel/manifest(format=mpd-time-csf)",
                    DrmDescription = new DrmDescription
                    {
                        KeySystem = "com.microsoft.playready",
                        LicenseServerUri =
                            "http://playready-testserver.azurewebsites.net/rightsmanager.asmx?PlayRight=1&UseSimpleNonPersistentLicense=1",
                        RequestHeaders = new Dictionary<string, string>
                        {
                            ["Content-Type"] = "text/xml; charset=utf-8"
                        }
                    }
                },
                new Clip
                {
                    MpdUri = "http://106.120.45.49/tears_of_steel_uhd/tears_uhd.mpd",
                    DrmDescription = new DrmDescription
                    {
                        KeySystem = "com.widevine.alpha",
                        LicenseServerUri =
                            "https://proxy.uat.widevine.com/proxy?provider=widevine_test",
                        RequestHeaders = new Dictionary<string, string>
                        {
                            ["Content-Type"] = "text/xml; charset=utf-8"
                        }
                    }
                }
            };
        }

        private static async Task WaitForTargetPosition(IPlayer player, TimeSpan targetPosition)
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                cancellationTokenSource.CancelAfter(CatchupPlaybackTimeout);
                while (true)
                {
                    var currentPosition = player.Position;
                    if (currentPosition != null)
                    {
                        var diff = Math.Abs(
                            currentPosition.Value.TotalMilliseconds
                            - targetPosition.TotalMilliseconds);
                        if (diff <= 500)
                            break;
                    }

                    await Task.Delay(250, cancellationTokenSource.Token);
                }
            }
        }

        private static async Task WaitForBufferingEnd(IPlayer player)
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                cancellationTokenSource.CancelAfter(BufferingEndTimeout);
                await player.OnEvent()
                    .Where(@event =>
                    {
                        if (@event is BufferingEvent bufferingEvent)
                            return !bufferingEvent.IsBuffering;
                        return false;
                    })
                    .FirstAsync()
                    .ToTask(cancellationTokenSource.Token);
            }
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(10)]
        public void Playback_PlayCalled_PlaysSuccessfully(Clip clip)
        {
            RunTest(async () =>
            {
                IPlayer dashPlayer = null;
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    dashPlayer = BuildPlayer(clip);
                    await dashPlayer.Prepare();
                    _logger.Info($"After prepare {stopwatch.Elapsed}");
                    dashPlayer.Play();
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    var position = dashPlayer.Position;
                    var state = dashPlayer.State;
                    Assert.That(state, Is.EqualTo(PlayerState.Playing));
                    Assert.That(position, Is.GreaterThan(TimeSpan.Zero));
                }
                finally
                {
                    if (dashPlayer != null)
                        await dashPlayer.DisposeAsync();
                }
            });
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(10)]
        public void Playback_StartFrom20thSecond_PlaysSuccessfully(Clip clip)
        {
            RunTest(async () =>
            {
                IPlayer dashPlayer = null;
                try
                {
                    var configuration =
                        new Configuration {StartTime = TimeSpan.FromSeconds(20)};
                    dashPlayer = BuildPlayer(
                        clip,
                        configuration);
                    await dashPlayer.Prepare();
                    dashPlayer.Play();
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    var position = dashPlayer.Position;
                    var state = dashPlayer.State;
                    Assert.That(state, Is.EqualTo(PlayerState.Playing));
                    Assert.That(position, Is.GreaterThan(TimeSpan.FromSeconds(10)));
                }
                finally
                {
                    if (dashPlayer != null)
                        await dashPlayer.DisposeAsync();
                }
            });
        }

        private async Task SeekTest(
            Clip clip,
            bool shouldPlay,
            Func<TimeSpan, IEnumerable<TimeSpan>> generator)
        {
            IPlayer dashPlayer = null;
            try
            {
                var stopwatch = Stopwatch.StartNew();
                dashPlayer = BuildPlayer(clip);
                await dashPlayer.Prepare();
                var duration = dashPlayer.Duration;
                if (duration == null)
                    Assert.Ignore();
                var testcases = generator.Invoke(duration.Value);

                if (shouldPlay)
                    dashPlayer.Play();

                foreach (var nextPosition in testcases)
                {
                    var start = stopwatch.Elapsed;
                    await dashPlayer.Seek(nextPosition);
                    var end = stopwatch.Elapsed;

                    var seekTimeDuration = end - start;
                    var msg = $"Seek took: {seekTimeDuration.TotalMilliseconds} ms";
                    if (seekTimeDuration >= TimeSpan.FromSeconds(2))
                        Assert.Fail(msg);
                    else if (seekTimeDuration >= TimeSpan.FromMilliseconds(1300))
                        _logger.Warn($"[WARNING] {msg}");
                    else
                        _logger.Info(msg);

                    if (shouldPlay)
                        await WaitForTargetPosition(dashPlayer, nextPosition);
                    var expectedState = shouldPlay ? PlayerState.Playing : PlayerState.Ready;
                    var actualState = dashPlayer.State;
                    if (shouldPlay && actualState == PlayerState.Paused)
                    {
                        await WaitForBufferingEnd(dashPlayer);
                        actualState = dashPlayer.State;
                    }

                    Assert.That(actualState, Is.EqualTo(expectedState));
                }
            }
            finally
            {
                if (dashPlayer != null)
                    await dashPlayer.DisposeAsync();
            }
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        public void Seek_WhilePlaying_Seeks(Clip clip)
        {
            RunTest(async () =>
                await SeekTest(
                    clip,
                    true,
                    duration => RandomPositionGenerator(duration, 20)));
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        public void Seek_WhileReady_Seeks(Clip clip)
        {
            RunTest(async () =>
                await SeekTest(
                    clip,
                    false,
                    duration => RandomPositionGenerator(duration, 20)));
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        public void Seek_Backward_Seeks(Clip clip)
        {
            RunTest(async () =>
                await SeekTest(
                    clip,
                    true,
                    duration => BackwardPositionGenerator(duration, TimeSpan.FromSeconds(20))));
        }

        private async Task SetStreamGroupsTest(
            Clip clip,
            Func<StreamGroup[], (StreamGroup[], IStreamSelector[])[]> generator)
        {
            IPlayer dashPlayer = null;
            try
            {
                dashPlayer = BuildPlayer(clip);
                await dashPlayer.Prepare();
                dashPlayer.Play();
                await Task.Delay(TimeSpan.FromSeconds(3));

                var inputStreamGroups = dashPlayer.GetStreamGroups();
                var testCases = generator.Invoke(inputStreamGroups);
                foreach (var testCase in testCases)
                {
                    var (outputStreamGroups, outputSelectors) = testCase;
                    await dashPlayer.SetStreamGroups(
                        outputStreamGroups,
                        outputSelectors);
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    var position = dashPlayer.Position;
                    var state = dashPlayer.State;
                    Assert.That(position, Is.GreaterThan(TimeSpan.Zero));
                    Assert.That(state, Is.EqualTo(PlayerState.Playing));
                }
            }
            finally
            {
                if (dashPlayer != null)
                    await dashPlayer.DisposeAsync();
            }
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(5)]
        public void SetStreamGroups_SetsOnlyAudio_PlaysOnlyAudio(Clip clip)
        {
            RunTest(async () => await SetStreamGroupsTest(clip, streamGroups =>
            {
                var audioStreamGroup =
                    streamGroups.First(streamGroup =>
                        streamGroup.ContentType == ContentType.Audio);
                var lastIndex = audioStreamGroup.Streams.Count - 1;
                var streamSelector = new FixedStreamSelector(lastIndex);

                return new[]
                {
                    (
                        new[] {audioStreamGroup},
                        new IStreamSelector[] {streamSelector}
                    )
                };
            }));
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(5)]
        public void SetStreamGroups_SetsVideoOnly_PlaysOnlyVideo(Clip clip)
        {
            RunTest(async () => await SetStreamGroupsTest(clip, streamGroups =>
            {
                var videoStreamGroup =
                    streamGroups.First(streamGroup =>
                        streamGroup.ContentType == ContentType.Video);
                return new[]
                {
                    (
                        new[] {videoStreamGroup},
                        new IStreamSelector[] {null}
                    )
                };
            }));
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(5)]
        public void SetStreamGroups_UpdatesVideoStreamSelector_WorksAsExpected(Clip clip)
        {
            RunTest(async () => await SetStreamGroupsTest(clip, streamGroups =>
            {
                var videoStreamGroup =
                    streamGroups.FirstOrDefault(streamGroup =>
                        streamGroup.ContentType == ContentType.Video
                        && streamGroup.Streams.Count > 1);

                if (videoStreamGroup == null)
                    Assert.Ignore();

                var audioStreamGroup =
                    streamGroups.FirstOrDefault(streamGroup =>
                        streamGroup.ContentType == ContentType.Audio);

                var testCases = new List<(StreamGroup[], IStreamSelector[])>();
                for (var j = 0; j < videoStreamGroup.Streams.Count; ++j)
                {
                    var selector = new FixedStreamSelector(j);
                    testCases.Add(
                        (
                            new[] {videoStreamGroup, audioStreamGroup},
                            new IStreamSelector[] {selector, null}
                        ));
                }

                return testCases.ToArray();
            }));
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(5)]
        public void SetStreamGroups_UpdatesAudioStreamSelector_WorksAsExpected(Clip clip)
        {
            RunTest(async () => await SetStreamGroupsTest(clip, streamGroups =>
            {
                var videoStreamGroup =
                    streamGroups.FirstOrDefault(streamGroup =>
                        streamGroup.ContentType == ContentType.Video);

                var audioStreamGroups =
                    streamGroups.Where(streamGroup =>
                        streamGroup.ContentType == ContentType.Audio);

                var testCases = new List<(StreamGroup[], IStreamSelector[])>();
                foreach (var audioStreamGroup in audioStreamGroups)
                {
                    for (var j = 0; j < audioStreamGroup.Streams.Count; ++j)
                    {
                        var selector = new FixedStreamSelector(j);
                        testCases.Add(
                            (
                                new[] {videoStreamGroup, audioStreamGroup},
                                new IStreamSelector[] {null, selector}
                            ));
                    }
                }

                return testCases.ToArray();
            }));
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        public void EOS_Reached_NotifiesClient(Clip clip)
        {
            RunTest(async () =>
            {
                IPlayer dashPlayer = null;
                try
                {
                    dashPlayer = BuildPlayer(clip);
                    await dashPlayer.Prepare();
                    var duration = dashPlayer.Duration;
                    if (duration == null)
                        Assert.Ignore();
                    for (var i = 0; i < 5; i++)
                    {
                        var nearToEndSeekTime =
                            duration.Value
                                .Subtract(TimeSpan.FromSeconds(2));
                        await dashPlayer.Seek(nearToEndSeekTime);
                        var exceptionTask = dashPlayer
                            .OnEvent()
                            .OfType<ExceptionEvent>()
                            .FirstAsync()
                            .ToTask();
                        var eosTask = dashPlayer
                            .OnEvent()
                            .OfType<EosEvent>()
                            .FirstAsync()
                            .ToTask();
                        dashPlayer.Play();
                        var timeoutTask = Task.Delay(EosTimeout);
                        var completedTask = await Task.WhenAny(
                            exceptionTask,
                            eosTask,
                            timeoutTask);
                        if (completedTask == exceptionTask)
                        {
                            var exceptionEvent = await exceptionTask;
                            throw exceptionEvent.Exception;
                        }
                        else if (completedTask == timeoutTask)
                            throw new TimeoutException();
                    }
                }
                finally
                {
                    if (dashPlayer != null)
                        await dashPlayer.DisposeAsync();
                }
            });
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        public void Seek_ToClipDuration_CompletesAndPublishesEos(Clip clip)
        {
            RunTest(async () =>
            {
                IPlayer dashPlayer = null;
                try
                {
                    dashPlayer = BuildPlayer(clip);
                    await dashPlayer.Prepare();
                    var duration = dashPlayer.Duration;
                    if (duration == null)
                        Assert.Ignore();

                    var exceptionTask = dashPlayer
                        .OnEvent()
                        .OfType<ExceptionEvent>()
                        .FirstAsync()
                        .ToTask();
                    var eosTask = dashPlayer
                        .OnEvent()
                        .OfType<EosEvent>()
                        .FirstAsync()
                        .ToTask();

                    await dashPlayer.Seek(duration.Value);
                    dashPlayer.Play();

                    var timeoutTask = Task.Delay(EosTimeout);

                    var completedTask = await Task.WhenAny(
                        exceptionTask,
                        eosTask,
                        timeoutTask);
                    if (completedTask == exceptionTask)
                    {
                        var exceptionEvent = await exceptionTask;
                        throw exceptionEvent.Exception;
                    }
                    else if (completedTask == timeoutTask)
                        throw new TimeoutException();
                }
                finally
                {
                    if (dashPlayer != null)
                        await dashPlayer.DisposeAsync();
                }
            });
        }

        private void RunTest(Func<Task> test)
        {
            var testContext = TestContext.CurrentContext;
            var testName = testContext.Test.FullName;
            var isPassed = true;
            try
            {
                _logger.Info($"{testName} starts");
                AsyncContext.Run(async () => { await test.Invoke(); });
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                isPassed = false;
                throw;
            }
            finally
            {
                const string passed = "PASSED";
                const string failed = "FAILED";
                var result = isPassed ? passed : failed;
                _logger.Info($"{testName} ends with Result={result}");
            }
        }

        private static IPlayer BuildPlayer(Clip clip, Configuration configuration = default)
        {
            var window = new ElmSharpWindow(AppContext.Instance.MainWindow);
            var mpdUri = clip.MpdUri;
            var drmInfo = clip.DrmDescription;
            var builder = new DashPlayerBuilder();
            builder = builder
                .SetWindow(window)
                .SetMpdUri(mpdUri)
                .SetConfiguration(configuration);
            if (drmInfo != null)
            {
                var keySystem = drmInfo.Value.KeySystem;
                var platformCapabilities = Platform.Current.Capabilities;
                var supportsKeySystem =
                    platformCapabilities.SupportsKeySystem(keySystem);
                Assert.That(supportsKeySystem, Is.True);
                var licenseServerUri = drmInfo.Value.LicenseServerUri;
                var requestHeaders = drmInfo.Value.RequestHeaders;
                builder = builder
                    .SetKeySystem(keySystem)
                    .SetDrmSessionHandler(new YoutubeDrmSessionHandler(
                        licenseServerUri,
                        requestHeaders));
            }

            return builder.Build();
        }

        private static IEnumerable<TimeSpan> RandomPositionGenerator(TimeSpan duration, int count)
        {
            var maxSeekPosition = duration.Subtract(TimeSpan.FromSeconds(5));
            var random = new Random();
            var testcases = new List<TimeSpan>();
            for (var i = 0; i < count; i++)
            {
                var nextPositionSeconds = random.Next((int) maxSeekPosition.TotalSeconds);
                var nextPosition = TimeSpan.FromSeconds(nextPositionSeconds);
                testcases.Add(nextPosition);
            }

            return testcases;
        }

        private static IEnumerable<TimeSpan> BackwardPositionGenerator(TimeSpan duration, TimeSpan step)
        {
            var nextSeekPosition = duration.Subtract(TimeSpan.FromSeconds(5));
            var testcases = new List<TimeSpan>();
            while (nextSeekPosition.TotalMilliseconds >= 0)
            {
                testcases.Add(nextSeekPosition);
                nextSeekPosition = nextSeekPosition.Subtract(step);
            }

            return testcases;
        }
    }
}