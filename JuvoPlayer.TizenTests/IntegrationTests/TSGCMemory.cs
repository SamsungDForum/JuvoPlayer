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
using JuvoPlayer.TizenTests.Utils;
using JuvoPlayer.Utils;
using Nito.AsyncEx;
using NUnit.Framework;
using Tizen.Applications;
using TestContext = JuvoPlayer.Tests.Utils.TestContext;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    public static class GCLogger
    {
        
        public static void StartLogs(object logger)
        {
            var Logger = (ILogger) logger;
            int iteration = 0;
            while (true)
            {
                Logger.Info(iteration + " GetTotalMemory(false): " + GC.GetTotalMemory(false));
                Logger.Info(iteration + " CollectionCount(0): "  + GC.CollectionCount(0));
                Logger.Info(iteration + " CollectionCount(1): " + GC.CollectionCount(1));
                Logger.Info(iteration + " CollectionCount(2): " + GC.CollectionCount(2));
                Logger.Info(iteration + " GetAllocatedBytesForCurrentThread(): " + GC.GetAllocatedBytesForCurrentThread());
                iteration++;
                Thread.Sleep(1000);
            }
        }
    }
    
    [TestFixture]
    class TSGCMemory
    {
        
        private ILogger Logger = LoggerManager.GetInstance().GetLogger("GC");
        private Thread InstanceCaller;
        
        [OneTimeSetUp] 
        public void Init()
        {
            Logger.Info("IN RunTests");
            
            Thread InstanceCaller = new Thread(new ParameterizedThreadStart(GCLogger.StartLogs));
            InstanceCaller.Start(Logger);
        }
        
        [OneTimeTearDown]
        public void Dispose()
        {
            Logger.Info("OUT RunTests");
            
            InstanceCaller.Interrupt();
        }
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
        
        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH UHD")]
        public void Seek_Random(string clipTitle)
        {
            Logger.Info("clipTitle: " + clipTitle);
            RunPlayerTest(clipTitle, async context =>
            {
                context.SeekTime = null;
                for (var i = 0; i < 25; ++i)
                {
                    var seekOperation = new SeekOperation();
                    seekOperation.Prepare(context);
                    await seekOperation.Execute(context);
                }
            });
        }
        

    }
}