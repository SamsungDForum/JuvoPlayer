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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Tests.Utils;
using JuvoPlayer.Utils;
using Nito.AsyncEx;
using NUnit.Framework;
using JuvoPlayer.Player.EsPlayer;
using TestContext = JuvoPlayer.Tests.Utils.TestContext;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    public static class TSPlayerServiceTestCaseSource
    {
        private static ClipDefinition[] allClipSource;
        private static string[] allClipsData;
        private static string[] dashClipsData;
        private static ClipDefinition[] drmClipsSource;

        private static IEnumerable<ClipDefinition> ReadClips()
        {
            var applicationPath = Paths.ApplicationPath;
            var clipsPath = Path.Combine(applicationPath, "res", "videoclips.json");
            return JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(clipsPath).ToList();
        }

        static TSPlayerServiceTestCaseSource()
        {
            allClipSource = ReadClips()
                .ToArray();

            allClipsData = allClipSource
                .Select(clip => clip.Title)
                .ToArray();

            dashClipsData = allClipSource
                .Where(clip => clip.Type == "dash")
                .Select(clip => clip.Title)
                .ToArray();

            drmClipsSource = allClipSource
                .Where(clip => clip.DRMDatas != null)
                .ToArray();

        }

        public static string[] AllClips() => allClipsData;

        public static string[] DashClips() => dashClipsData;

        public static bool IsEncrypted(string clipTitle) =>
            drmClipsSource.Any(clip => string.Equals(clip.Title, clipTitle));
    }

    [TestFixture]
    class TSPlayerService
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");

        private void RunPlayerTest(string clipTitle, Func<TestContext, Task> testImpl)
        {
            AsyncContext.Run(async () =>
            {
                try
                {
                    _logger.Info($"Begin: {NUnit.Framework.TestContext.CurrentContext.Test.FullName}");

                    using (var service = new PlayerService())
                    {
                        using (var cts = new CancellationTokenSource())
                        {
                            var context = new TestContext
                            {
                                Service = service,
                                ClipTitle = clipTitle,
                                Token = cts.Token,

                                // Requested seek position may differ from
                                // seek position issued to player. Difference can be 10s+
                                // Encrypted streams (Widevine in particular) may have LONG license
                                // installation times (10s+).
                                // DRM content has larger timeout
                                Timeout = TSPlayerServiceTestCaseSource.IsEncrypted(clipTitle)
                                    ? TimeSpan.FromSeconds(40)
                                    : TimeSpan.FromSeconds(20)
                            };
                            await new PrepareOperation().Execute(context);
                            await new StartOperation().Execute(context);

                            await testImpl(context);

                            // Test completed.
                            // Do cancellation to terminate test's sub activities (if any)
                            cts.Cancel();
                        }
                    }

                    _logger.Info($"End: {NUnit.Framework.TestContext.CurrentContext.Test.FullName}");
                }
                catch (Exception e)
                {
                    _logger.Error($"Error: {NUnit.Framework.TestContext.CurrentContext.Test.FullName} {e.Message} {e.StackTrace}");
                    throw;
                }
            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Playback_Basic_PreparesAndStarts(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var tcs = new TaskCompletionSource<TimeSpan>();

                using (new ClockProvider().PlayerClockObservable()
                    .Subscribe(clk =>
                    {
                        if (clk <= TimeSpan.Zero)
                            return;
                        tcs.TrySetResult(clk);
                    }))
                {

                    var clock = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(1))) == tcs.Task ?
                        tcs.Task.Result : TimeSpan.Zero;

                    Assert.That(clock, Is.GreaterThan(TimeSpan.Zero));
                }
            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Seek_Random10Times_Seeks(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                context.SeekTime = null;
                for (var i = 0; i < 10; ++i)
                {
                    var seekOperation = new SeekOperation();
                    seekOperation.Prepare(context);
                    await seekOperation.Execute(context);
                }
            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Seek_DisposeDuringSeek_Disposes(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var seekOperation = new SeekOperation();
                seekOperation.Prepare(context);
#pragma warning disable 4014
                seekOperation.Execute(context);
#pragma warning restore 4014
                await Task.Delay(250);
            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Seek_Forward_Seeks(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;

                for (var nextSeekTime = TimeSpan.Zero;
                    nextSeekTime < service.Duration - TimeSpan.FromSeconds(5);
                    nextSeekTime += TimeSpan.FromSeconds(10))
                {
                    context.SeekTime = nextSeekTime;
                    var seekOperation = new SeekOperation();
                    seekOperation.Prepare(context);
                    await seekOperation.Execute(context);
                }
            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Seek_Backward_Seeks(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;

                for (var nextSeekTime = service.Duration - TimeSpan.FromSeconds(15);
                    nextSeekTime > TimeSpan.Zero;
                    nextSeekTime -= TimeSpan.FromSeconds(20))
                {
                    context.SeekTime = nextSeekTime;
                    var seekOperation = new SeekOperation();
                    seekOperation.Prepare(context);
                    await seekOperation.Execute(context);
                }
            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Seek_ToTheEnd_SeeksOrCompletes(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;
                context.SeekTime = service.Duration;
                try
                {
                    var seekOperation = new SeekOperation();
                    seekOperation.Prepare(context);
                    var seekTask = seekOperation.Execute(context);

                    var clipCompletedTask = service.StateChanged()
                        .AsCompletion()
                        .Timeout(context.Timeout)
                        .FirstAsync()
                        .ToTask();

                    await await Task.WhenAny(seekTask, clipCompletedTask);
                }
                catch (SeekException)
                {
                    // ignored
                }
            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Seek_EOSReached_StateChangedCompletes(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;

                context.SeekTime = service.Duration - TimeSpan.FromSeconds(5);
                var seekOperation = new SeekOperation();
                seekOperation.Prepare(context);

                var playbackErrorTask = service.PlaybackError()
                    .FirstAsync()
                    .ToTask();

                var clipCompletedTask = service.StateChanged()
                    .AsCompletion()
                    .Timeout(context.Timeout)
                    .FirstAsync()
                    .ToTask();

                await await Task.WhenAny(seekOperation.Execute(context), clipCompletedTask, playbackErrorTask);
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        public void Random_20RandomOperations_ExecutedCorrectly(string clipTitle)
        {
            var operations =
                GenerateOperations(20, new List<Type> { typeof(StopOperation), typeof(PrepareOperation) });

            try
            {
                _logger.Info("Begin: " + NUnit.Framework.TestContext.CurrentContext.Test.Name);
                RunRandomOperationsTest(clipTitle, operations, true);
                _logger.Info("Done: " + NUnit.Framework.TestContext.CurrentContext.Test.Name);
            }
            catch (Exception)
            {
                _logger.Error("Error " + clipTitle);
                DumpOperations(clipTitle, operations);
                throw;
            }
        }

        private void RunRandomOperationsTest(string clipTitle, IList<TestOperation> operations, bool shouldPrepare)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                context.RandomMaxDelayTime = TimeSpan.FromSeconds(3);
                context.DelayTime = TimeSpan.FromSeconds(2);
                context.Timeout = TSPlayerServiceTestCaseSource.IsEncrypted(clipTitle) ? TimeSpan.FromSeconds(40) : TimeSpan.FromSeconds(20);

                if (shouldPrepare)
                    foreach (var operation in operations)
                    {
                        _logger.Info($"Prepare: {operation}");
                        operation.Prepare(context);
                        _logger.Info($"Prepare Done: {operation}");
                    }

                foreach (var operation in operations)
                {
                    _logger.Info($"Execute: {operation}");
                    await operation.Execute(context);
                    _logger.Info($"Execute Done: {operation}");
                }
            });
        }

        [Test]
        [IgnoreIfParamMissing("RandomTestOperationsPath")]
        public void Random_CustomOperations_ExecutedCorrectly()
        {
            var operationsPath = NUnit.Framework.TestContext.Parameters["RandomTestOperationsPath"];
            using (var reader = new StreamReader(operationsPath))
            {
                var clipTitle = reader.ReadLine();
                var operations = OperationSerializer.Deserialize(reader);
                RunRandomOperationsTest(clipTitle, operations, false);
            }
        }

        private static IList<TestOperation> GenerateOperations(int count, ICollection<Type> blackList)
        {
            var generatedOperations = new List<TestOperation>();
            var generator = new RandomOperationGenerator();
            for (var i = 0; i < count;)
            {
                var operation = generator.NextOperation();
                if (blackList.Contains(operation.GetType()))
                    continue;
                generatedOperations.Add(operation);
                ++i;
            }

            return generatedOperations;
        }

        private static void DumpOperations(string clipTitle, IEnumerable<TestOperation> operations)
        {
            var testName = NUnit.Framework.TestContext.CurrentContext.Test.Name
                .Replace(" ", "-");
            var testDate = DateTime.Now.TimeOfDay.ToString()
                .Replace(" ", "-")
                .Replace(":", "-");
            var pid = Process.GetCurrentProcess().Id;

            var fullPath = Path.Combine(Path.GetTempPath(), $"{testName}_{pid}_{testDate}");

            using (var writer = new StreamWriter(fullPath))
            {
                writer.WriteLine(clipTitle);
                OperationSerializer.Serialize(writer, operations);
                Console.WriteLine($"Test operations dumped to {fullPath}");
            }
        }
    }
}
