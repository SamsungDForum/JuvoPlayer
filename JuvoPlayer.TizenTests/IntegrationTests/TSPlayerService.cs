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
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Tests.Utils;
using JuvoPlayer.TizenTests.Utils;
using Nito.AsyncEx;
using NUnit.Framework;
using TestContext = JuvoPlayer.Tests.Utils.TestContext;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    class TSPlayerService
    {
        private void RunPlayerTest(string clipTitle, Func<TestContext, Task> testImpl)
        {
            AsyncContext.Run(async () =>
            {
                using (var service = new PlayerService())
                using (var cts = new CancellationTokenSource())
                {
                    var context = new TestContext
                    {
                        Service = service,
                        ClipTitle = clipTitle,
                        Token = cts.Token,
                        Timeout = TimeSpan.FromSeconds(20)
                    };
                    await new PrepareOperation().Execute(context);
                    await new StartOperation().Execute(context);

                    await testImpl(context);
                }
            });
        }

        [TestCase("Clean MP4 over HTTP")]
        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        [TestCase("Clean HLS")]
        [TestCase("Clean HEVC 4k MPEG DASH")]
        [TestCase("Art Of Motion")]
        [TestCase("Encrypted 4K MPEG DASH UHD")]
        public void Playback_Basic_PreparesAndStarts(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));

                Assert.That(context.Service.CurrentPosition, Is.GreaterThan(TimeSpan.Zero));
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        [TestCase("Art Of Motion")]
        public void Seek_Random10Times_Seeks(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                for (var i = 0; i < 10; ++i)
                    await new SeekOperation().Execute(context);
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        [TestCase("Art Of Motion")]
        public void Seek_DisposeDuringSeek_Disposes(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
#pragma warning disable 4014
                new SeekOperation().Execute(context);
#pragma warning restore 4014
                await Task.Delay(250);
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        [TestCase("Art Of Motion")]
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
                    await new SeekOperation().Execute(context);
                }
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        [TestCase("Art Of Motion")]
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
                    await new SeekOperation().Execute(context);
                }
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        [TestCase("Art Of Motion")]
        public void Seek_ToTheEnd_Seeks(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;
                context.SeekTime = service.Duration;
                try
                {
                    await new SeekOperation().Execute(context);
                }
                catch (SeekException)
                {
                    // ignored
                }
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        [TestCase("Art Of Motion")]
        [TestCase("Encrypted 4K MPEG DASH UHD")]
        public void Seek_EOSReached_StateChangedCompletes(string clipTitle)
        {
            RunPlayerTest(clipTitle, async context =>
            {
                var service = context.Service;
                context.SeekTime = service.Duration - TimeSpan.FromSeconds(5);
                await new SeekOperation().Execute(context);
                await service.StateChanged()
                    .AsCompletion()
                    .Timeout(TimeSpan.FromSeconds(10))
                    .ToTask();
            });
        }

        [TestCase("Clean byte range MPEG DASH")]
        public void Random_20RandomOperations_ExecutedCorrectly(string clipTitle)
        {
            var operations =
                GenerateOperations(20, new List<Type> {typeof(StopOperation), typeof(PrepareOperation)});

            try
            {
                RunRandomOperationsTest(clipTitle, operations, true);
            }
            catch (Exception)
            {
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

                if (shouldPrepare)
                    foreach (var operation in operations)
                        operation.Prepare(context);

                foreach (var operation in operations)
                    await operation.Execute(context);
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