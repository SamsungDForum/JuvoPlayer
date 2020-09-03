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
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Platforms.Tizen;
using Nito.AsyncEx;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.Tests
{
    [TestFixture]
    public class DashPlayerTests
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");

        public static string[] Clips =
        {
            "http://106.120.45.49/googlecar/car-20120827-manifest.mpd",
            "http://106.120.45.49/sintel-dash/sintel.mpd", "http://106.120.45.49/formula1/Manifest.mpd",
            "http://106.120.45.49/paris/dashevc-live-2s-4k.mpd"
        };

        private static async Task WaitForTargetPosition(IPlayer player, TimeSpan targetPosition)
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(8));
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

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(10)]
        public void Playback_PlayCalled_PlaysSuccessfully(string mpdUri)
        {
            AsyncContext.Run(async () =>
            {
                IPlayer dashPlayer = null;
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var window = new Window(AppContext.Instance.MainWindow);
                    var builder = new DashPlayerBuilder();
                    dashPlayer = builder
                        .SetMpdUri(mpdUri)
                        .SetWindow(window)
                        .Build();
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
        public void Playback_StartFrom20thSecond_PlaysSuccessfully(string mpdUri)
        {
            AsyncContext.Run(async () =>
            {
                IPlayer dashPlayer = null;
                try
                {
                    var window = new Window(AppContext.Instance.MainWindow);
                    var builder = new DashPlayerBuilder();
                    var configuration =
                        new Configuration {StartTime = TimeSpan.FromSeconds(20)};
                    dashPlayer = builder
                        .SetMpdUri(mpdUri)
                        .SetWindow(window)
                        .SetConfiguration(configuration)
                        .Build();
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

        private void RandomSeeksTest(
            string mpdUri,
            int seekCount,
            bool shouldPlay)
        {
            AsyncContext.Run(async () =>
            {
                IPlayer dashPlayer = null;
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var window = new Window(AppContext.Instance.MainWindow);
                    var builder = new DashPlayerBuilder();
                    dashPlayer = builder
                        .SetMpdUri(mpdUri)
                        .SetWindow(window)
                        .Build();
                    await dashPlayer.Prepare();

                    if (shouldPlay)
                        dashPlayer.Play();
                    var random = new Random();

                    for (var i = 0; i < seekCount; i++)
                    {
                        var nextPositionTicks = random.Next((int) TimeSpan.FromSeconds(60).Ticks);
                        var nextPosition = TimeSpan.FromTicks(nextPositionTicks);
                        var start = stopwatch.Elapsed;
                        await dashPlayer.Seek(nextPosition);
                        var end = stopwatch.Elapsed;
                        _logger.Info($"Seek took: {(end - start).TotalMilliseconds} ms");
                        if (shouldPlay)
                            await WaitForTargetPosition(dashPlayer, nextPosition);
                        var actualState = dashPlayer.State;
                        var expectedState = shouldPlay ? PlayerState.Playing : PlayerState.Ready;
                        Assert.That(actualState, Is.EqualTo(expectedState));
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
        public void Seek_WhilePlaying_Seeks(string mpdUri)
        {
            RandomSeeksTest(mpdUri, 20, true);
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        public void Seek_WhileReady_Seeks(string mpdUri)
        {
            RandomSeeksTest(mpdUri, 20, false);
        }

        private void SetStreamGroupsTest(
            string mpdUri,
            Func<StreamGroup[], (StreamGroup[], IStreamSelector[])[]> generator)
        {
            AsyncContext.Run(async () =>
            {
                IPlayer dashPlayer = null;
                try
                {
                    var window = new Window(AppContext.Instance.MainWindow);
                    var builder = new DashPlayerBuilder();
                    dashPlayer = builder
                        .SetMpdUri(mpdUri)
                        .SetWindow(window)
                        .Build();
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
            });
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(5)]
        public void SetStreamGroups_SetsOnlyAudio_PlaysOnlyAudio(string mpdUri)
        {
            SetStreamGroupsTest(mpdUri, streamGroups =>
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
            });
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(5)]
        public void SetStreamGroups_SetsVideoOnly_PlaysOnlyVideo(string mpdUri)
        {
            SetStreamGroupsTest(mpdUri, streamGroups =>
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
            });
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(5)]
        public void SetStreamGroups_UpdatesVideoStreamSelector_WorksAsExpected(string mpdUri)
        {
            SetStreamGroupsTest(mpdUri, streamGroups =>
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
            });
        }

        [Test]
        [TestCaseSource(nameof(Clips))]
        [Repeat(5)]
        public void SetStreamGroups_UpdatesAudioStreamSelector_WorksAsExpected(string mpdUri)
        {
            SetStreamGroupsTest(mpdUri, streamGroups =>
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
            });
        }
    }
}