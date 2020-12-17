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
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Tests.Utils;
using Nito.AsyncEx;
using NUnit.Framework;
using TestContext = JuvoPlayer.Tests.Utils.TestContext;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    public static class TSPlayerServiceTestCaseSource
    {
        private static ClipDefinition[] allClipsSource;
        private static ClipDefinition[] drmClipsSource;

        private static string[] allClipsData;
        private static string[] seekableClipsData;
        private static string[] dashClipsData;
        private static string[] rtspClipsData;

        private static IEnumerable<ClipDefinition> ReadClips()
        {
            var applicationPath = Paths.ApplicationPath;
            var clipsPath = Path.Combine(applicationPath, "res", "videoclips.json");
            return JuvoPlayer.Utils.JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(clipsPath).ToList();
        }

        static TSPlayerServiceTestCaseSource()
        {
            var panelPixelCount = Utils.SystemInfo.PanelPixelCount;

            allClipsSource = ReadClips()
                .Where(clip => clip.PixelCount <= panelPixelCount)
                .ToArray();

            drmClipsSource = allClipsSource
                .Where(clip => clip.DRMDatas != null)
                .ToArray();

            allClipsData = allClipsSource
                .Select(clip => clip.Title)
                .ToArray();

            seekableClipsData = allClipsSource
                .Where(clip => clip.Type != "rtsp")
                .Select(clip => clip.Title)
                .ToArray();

            dashClipsData = allClipsSource
                .Where(clip => clip.Type == "dash")
                .Select(clip => clip.Title)
                .ToArray();

            rtspClipsData = allClipsSource
                .Where(clip => clip.Type == "rtsp")
                .Select(clip => clip.Title)
                .ToArray();
        }

        public static string[] AllClips() => allClipsData;

        public static string[] SeekableClips() => seekableClipsData;

        public static string[] DashClips() => dashClipsData;

        public static string[] RtspClips() => rtspClipsData;

        public static bool IsEncrypted(string clipTitle) =>
            drmClipsSource.Any(clip => string.Equals(clip.Title, clipTitle));
    }

    [TestFixture]
    class TSPlayerService
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");

        private void RunPlayerTest(string clipTitle, Func<TestContext, Task> testImpl, bool executeStart = true)
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

                            if (executeStart)
                            {
                                var startOperation = new StartOperation();
                                startOperation.Prepare(context);
                                await startOperation.Execute(context);
                            }

                            await testImpl(context);
                        }
                        catch (Exception e)
                        {
                            _logger.Error($"Error: {NUnit.Framework.TestContext.CurrentContext.Test.FullName} {e.Message} {e.StackTrace}");
                            throw;
                        }

                        try
                        {
                            // Test completed. Cancel token to kill any test's sub activities.
                            // Do so before PlayerService gets destroyed (in case those activities access it)
                            cts.Cancel();
                        }
                        catch (Exception e)
                            when (e is TaskCanceledException || e is OperationCanceledException)
                        {
                            /* Ignore. Listed exception are expected due to cancellation */
                        }
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

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.SeekableClips))]
        public void Playback_StartFromThe90thSecond_PreparesAndStarts(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                context.SeekTime = TimeSpan.FromSeconds(90);
                var seekOperation = new SeekOperation();
                seekOperation.Prepare(context);
                var seek = seekOperation.Execute(context);

                var startOperation = new StartOperation();
                startOperation.Prepare(context);
                await startOperation.Execute(context);

                await seek;

            }, false);
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.SeekableClips))]
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

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.SeekableClips))]
        public void Seek_DisposeDuringSeek_Disposes(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var seekOperation = new SeekOperation();
                seekOperation.Prepare(context);
                _ = seekOperation.Execute(context);
                await Task.Delay(250);
            });

        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.SeekableClips))]
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

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.SeekableClips))]
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

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.SeekableClips))]
        public void Seek_ToTheEnd_SeeksOrCompletes(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;
                context.SeekTime = service.Duration;
                try
                {
                    var clipCompletedTask = service.StateChanged()
                        .AsCompletion()
                        .ToTask(context.Token)
                        .WithTimeout(context.Timeout);

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

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.SeekableClips))]
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

        [TestCase("Clean byte range MPEG DASH")]
        public void RepresentationChange_DestructiveChange_Succeeds(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;
                var descriptions = service.GetStreamsDescription(StreamType.Audio);
                var playerObserver = RunningPlayerTask.Observe(context);

                for (var i = 0; i < descriptions.Count; i++)
                {
                    var changeOp = new ChangeRepresentationOperation
                    {
                        Index = i,
                        StreamType = StreamType.Audio
                    };
                    var entry = descriptions[i];
                    _logger.Info($"Changing to {entry.Id} {entry.StreamType} { entry.Description}");

                    await changeOp.Execute(context);
                    _logger.Info($"Changing to {entry.Id} {entry.StreamType} { entry.Description} Done");

                    await playerObserver.VerifyRunning(TimeSpan.FromSeconds(3));
                }
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        public void RepresentationChange_WhileSeeking_Succeeds(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var streams = new[] { StreamType.Video, StreamType.Audio };
                var service = context.Service;
                context.SeekTime = null;    // Perform random seeks.
                var defaultTimeout = context.Timeout;
                foreach (var stream in streams)
                {
                    var descriptions = service.GetStreamsDescription(stream);

                    for (var i = 0; i < descriptions.Count; i++)
                    {
                        var seekOp = new SeekOperation();

                        // Wait for seekOp after ChangeRepresentation executes.
                        // Otherwise, position task of SeekOp may timeout as position may not be available
                        // till ChangeRepresentation completes.
                        context.Timeout = TimeSpan.Zero;
                        seekOp.Prepare(context);
                        var seekTask = seekOp.Execute(context);
                        context.Timeout = defaultTimeout;

                        var changeOp = new ChangeRepresentationOperation
                        {
                            Index = i,
                            StreamType = stream
                        };

                        var changeTask = changeOp.Execute(context);

                        await changeTask.WithCancellation(context.Token);
                        await seekTask.WithTimeout(context.Timeout).WithCancellation(context.Token);

                    }
                }
            });
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Suspend_Resume_WhilePlaying_Succeeds(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var suspendOperation = new SuspendOperation();
                var resumeOperation = new ResumeOperation();

                suspendOperation.SetPreconditions(
                    // Wait for playing state & running clock
                    () => Task.WhenAll(WaitForState.Observe(context.Service, PlayerState.Playing, context.Token, context.Timeout),
                            RunningClockTask.Observe(context.Service, context.Token, context.Timeout)),

                    // Let it run for a moment
                    () => Task.Delay(SuspendOperation.GetRandomTimeSpan(TimeSpan.FromSeconds(2)), context.Token));

                // Suspend
                suspendOperation.Prepare(context);
                await suspendOperation.Execute(context);

                // Resume
                await resumeOperation.Execute(context);
            }, false);
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.SeekableClips))]
        public void Suspend_Resume_WhileSeeking_Succeeds(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var suspendOperation = new SuspendOperation();
                var resumeOperation = new ResumeOperation();

                suspendOperation.SetPreconditions(
                    // Wait for playing state & running clock
                    () => Task.WhenAll(WaitForState.Observe(context.Service, PlayerState.Playing, context.Token, context.Timeout),
                            RunningClockTask.Observe(context.Service, context.Token, context.Timeout)),

                    // Let it run
                    () => Task.Delay(SuspendOperation.GetRandomTimeSpan(TimeSpan.FromSeconds(2)), context.Token),

                    // Execute seek.
                    () =>
                    {
                        var randomSeekPos = SuspendOperation.GetRandomTimeSpan(context.Service.Duration - TimeSpan.FromSeconds(10));
                        _ = context.Service.SeekTo(randomSeekPos);
                        return Task.CompletedTask;
                    });

                // Suspend
                suspendOperation.Prepare(context);
                await suspendOperation.Execute(context);

                // Resume
                await resumeOperation.Execute(context);
            }, false);
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Suspend_Resume_WhilePaused_Succeeds(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var suspendOperation = new SuspendOperation();
                var resumeOperation = new ResumeOperation();

                suspendOperation.SetPreconditions(
                    // Wait for playing state & running clock
                    () => Task.WhenAll(WaitForState.Observe(context.Service, PlayerState.Playing, context.Token, context.Timeout),
                            RunningClockTask.Observe(context.Service, context.Token, context.Timeout)),

                    // Let it run
                    () => Task.Delay(SuspendOperation.GetRandomTimeSpan(TimeSpan.FromSeconds(2)), context.Token),

                    // Pause & wait for confirmation
                    () =>
                    {
                        context.Service.Pause();
                        return WaitForState.Observe(context.Service, PlayerState.Paused, context.Token, context.Timeout);
                    });

                // Suspend
                suspendOperation.Prepare(context);
                await suspendOperation.Execute(context);

                // Resume
                await resumeOperation.Execute(context);
            }, false);
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.AllClips))]
        public void Suspend_Resume_WhileStartingPlayback_Succeeds(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var suspendOperation = new SuspendOperation();
                var resumeOperation = new ResumeOperation();

                // Suspend
                suspendOperation.Prepare(context);
                await suspendOperation.Execute(context);

                // Resume
                await resumeOperation.Execute(context);
            }, false);
        }

        [Test, TestCaseSource(typeof(TSPlayerServiceTestCaseSource), nameof(TSPlayerServiceTestCaseSource.SeekableClips))]
        public void Suspend_Resume_WhileStartingPlaybackAndSeeking_Succeeds(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var suspendOperation = new SuspendOperation();
                var resumeOperation = new ResumeOperation();

                suspendOperation.SetPreconditions(
                    () =>
                    {
                        var randomSeekPos = SuspendOperation.GetRandomTimeSpan(context.Service.Duration - TimeSpan.FromSeconds(10));
                        _ = context.Service.SeekTo(randomSeekPos);
                        return Task.CompletedTask;
                    });

                // Suspend
                suspendOperation.Prepare(context);
                await suspendOperation.Execute(context);

                // Resume
                await resumeOperation.Execute(context);
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        public void Random_20RandomOperations_ExecutedCorrectly(string clipTitle)
        {
            var operations =
                GenerateOperations(20, new List<Type> { typeof(StopOperation), typeof(PrepareOperation), typeof(SuspendOperation), typeof(ResumeOperation) });

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

        private async Task CompletePendingOperations(TestContext context, IEnumerable<(Task task, Type operationType, DateTimeOffset when)> operations)
        {
            var anyFailed = false;
            foreach (var operation in operations)
            {
                try
                {
                    _logger.Info($"Completing {operation.operationType} {operation.when}");
                    await operation.task.WithTimeout(context.Timeout).WithCancellation(context.Token);
                    _logger.Info($"Completing {operation.operationType} {operation.when} Done");
                }
                catch (Exception e)
                {
                    if (!context.Token.IsCancellationRequested)
                    {
                        anyFailed = true;
                        _logger.Error($"Completing {operation.operationType} {operation.when} Failed");
                        _logger.Error(e);
                    }
                }

                if (context.Token.IsCancellationRequested)
                {
                    if (anyFailed)
                        throw new Exception("Pending operations failed to complete");

                    // No failures till cancellation request - exit
                    return;
                }
            }
        }
        private void RunRandomOperationsTest(string clipTitle, IList<TestOperation> operations, bool shouldPrepare)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                context.RandomMaxDelayTime = TimeSpan.FromSeconds(3);
                context.DelayTime = TimeSpan.FromSeconds(2);
                context.Timeout = TSPlayerServiceTestCaseSource.IsEncrypted(clipTitle) ? TimeSpan.FromSeconds(40) : TimeSpan.FromSeconds(20);

                var pendingTasks = new List<(Task task, Type operationType, DateTimeOffset when)>();

                var service = context.Service;
                var defaultTimeout = context.Timeout;

                foreach (var operation in operations)
                {
                    if (shouldPrepare)
                    {
                        _logger.Info($"Prepare: {operation}");
                        operation.Prepare(context);
                    }

                    var startState = service.State;
                    _logger.Info($"Execute: {operation} in state {startState}");

                    if (startState == PlayerState.Paused)
                    {
                        // In paused state, change operation and seek operation are not awaited but executed
                        // and stored in pending operation pool.
                        // Start operation will attempt to complete all pending activities.
                        switch (operation)
                        {
                            case ChangeRepresentationOperation changeOp:
                            case SeekOperation seekOp:
                                // set timeout to zero, indicating there will be no timeout for this task.
                                context.Timeout = TimeSpan.Zero;
                                var opTask = operation.Execute(context);

                                // restore timeout value
                                context.Timeout = defaultTimeout;
                                pendingTasks.Add((opTask, operation.GetType(), DateTimeOffset.Now));
                                _logger.Info($"Pending: {operation}");
                                continue;

                            case StartOperation startOp:
                                // Execute op & pending operations
                                await operation.Execute(context);
                                await CompletePendingOperations(context, pendingTasks);
                                pendingTasks.Clear();
                                continue;
                        }
                    }

                    await operation.Execute(context).WithCancellation(context.Token);
                    _logger.Info($"Done: {operation}");
                }

                if (pendingTasks.Count == 0)
                    return;

                // Start playback to complete pending ops
                try
                {
                    var startOp = new StartOperation();
                    startOp.Prepare(context);
                    await startOp.Execute(context);
                    await CompletePendingOperations(context, pendingTasks);
                }
                catch (Exception)
                {
                    foreach (var pt in pendingTasks)
                    {
                        _logger.Warn($"{pt.when} {pt.operationType} {pt.task.Status}");
                    }

                    throw;
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
