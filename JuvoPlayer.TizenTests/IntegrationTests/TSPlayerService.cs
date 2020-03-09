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
                _logger.Info($"Begin: {NUnit.Framework.TestContext.CurrentContext.Test.FullName}");

                using (var cts = new CancellationTokenSource())
                {
                    using (var service = new PlayerService())
                    {
                        try
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
                            var prepareOperation = new PrepareOperation();
                            prepareOperation.Prepare(context);
                            await prepareOperation.Execute(context);

                            var startOperation = new StartOperation();
                            startOperation.Prepare(context);
                            await startOperation.Execute(context);

                            await testImpl(context);
                        }
                        catch (Exception e)
                        {
                            _logger.Error($"Error: {NUnit.Framework.TestContext.CurrentContext.Test.FullName} {e.Message} {e.StackTrace}");
                            throw;
                        }

                        // Test completed. Cancel token to kill any test's sub activities.
                        // Do so before PlayerService gets destroyed (in case those activities access it)
                        cts.Cancel();
                    }
                }

                _logger.Info($"End: {NUnit.Framework.TestContext.CurrentContext.Test.FullName}");

            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Playback_Basic_PreparesAndStarts(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                await RunningClockTask.Observe(context.Service, context.Token, context.Timeout);
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
                    var clipCompletedTask = service.StateChanged()
                        .ObserveOn(SynchronizationContext.Current)
                        .AsCompletion()
                        .Timeout(context.Timeout)
                        .ToTask(context.Token);

                    var seekOperation = new SeekOperation();
                    seekOperation.Prepare(context);
                    var seekTask = seekOperation.Execute(context);

                    await await Task.WhenAny(seekTask, clipCompletedTask);
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                    throw;
                }
            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Seek_EOSReached_StateChangedCompletes(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;
                var playbackErrorTask = service.PlaybackError()
                    .FirstAsync()
                    .Timeout(context.Timeout)
                    .ToTask();

                var clipCompletedTask = service.StateChanged()
                    .AsCompletion()
                    .Timeout(context.Timeout)
                    .ToTask();

                context.SeekTime = service.Duration - TimeSpan.FromSeconds(5);
                var seekOperation = new SeekOperation();
                seekOperation.Prepare(context);

                // seek.execute() completes when seek position is reached. Do not wait for it!
                // Desired clock may never be reached. Wait for desired state changes only.
                var seekExecution = seekOperation.Execute(context);

                await await Task.WhenAny(clipCompletedTask, playbackErrorTask);
            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Random_10Pending_Seeks(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;
                await StateChangedTask.Observe(service, PlayerState.Playing, context.Token, context.Timeout);

                var pauseOp = new PauseOperation();
                pauseOp.Prepare(context);
                await pauseOp.Execute(context);

                context.SeekTime = null;
                var seekPosition = TimeSpan.Zero;
                for (var i = 0; i <= 10; ++i)
                {
                    var pendingSeekOp = new SeekOperation();
                    pendingSeekOp.Prepare(context);
                    seekPosition = pendingSeekOp.SeekPosition;
                    await pendingSeekOp.Execute(context);
                }

                var runningClock = RunningClockTask.Observe(service, seekPosition, context.Token, context.Timeout);

                var startOp = new StartOperation();
                startOp.Prepare(context);
                var startTask = startOp.Execute(context);

                await Task.WhenAll(startTask, runningClock);
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        public void Destructive_Representation_Change(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;
                var descriptions = service.GetStreamsDescription(StreamType.Audio);
                if (descriptions.Count == 0)
                    return;

                await StateChangedTask.Observe(service, PlayerState.Playing, context.Token, context.Timeout);

                foreach (var entry in descriptions)
                {
                    var changeOp = new ChangeRepresentationOperation
                    {
                        Index = entry.Id,
                        StreamType = StreamType.Audio
                    };
                    changeOp.Prepare(context);
                    var changeTask = changeOp.Execute(context);
                    var runningClockTask = RunningClockTask.Observe(context.Service, context.Token, context.Timeout);

                    await Task.WhenAll(changeTask, runningClockTask);
                }
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        public void Seek_Then_Change_Representation(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var streams = new[] { StreamType.Video, StreamType.Audio };
                var service = context.Service;

                await StateChangedTask.Observe(service, PlayerState.Playing, context.Token, context.Timeout);

                foreach (var stream in streams)
                {
                    var descriptions = service.GetStreamsDescription(StreamType.Audio);
                    if (descriptions.Count == 0)
                        continue;

                    context.SeekTime = null;
                    foreach (var entry in descriptions)
                    {
                        var seekOp = new SeekOperation();
                        seekOp.Prepare(context);
                        await seekOp.Execute(context);

                        var changeOp = new ChangeRepresentationOperation
                        {
                            Index = entry.Id,
                            StreamType = stream
                        };
                        changeOp.Prepare(context);
                        var changeTask = changeOp.Execute(context);
                        var runningClockTask = RunningClockTask.Observe(context.Service, context.Token, context.Timeout);

                        await Task.WhenAll(changeTask, runningClockTask);
                    }
                }
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

                foreach (var operation in operations)
                {
                    if (shouldPrepare)
                    {
                        _logger.Info($"Prepare: {operation}");
                        operation.Prepare(context);
                    }

                    _logger.Info($"Execute: {operation}");
                    await operation.Execute(context);
                    _logger.Info($"Done: {operation}");
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
