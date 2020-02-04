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

using NUnitLite;
using NUnit.Common;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoLogger.Tizen;
using JuvoPlayer.Common;
using JuvoPlayer.Tests.Utils;
using Tizen.Applications;
using Path = System.IO.Path;
using Window = ElmSharp.Window;

namespace JuvoPlayer.TizenTests
{
    class Program : CoreUIApplication
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("UT");
        private ReceivedAppControl receivedAppControl;
        private string[] nunitArgs;
        private bool enableGCLogs = false;
        private Window mainWindow;

        private static Assembly GetAssemblyByName(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == name);
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            mainWindow = new Window("Main Window") {Geometry = new ElmSharp.Rect(0, 0, 1920, 1080)};
            mainWindow.Show();
            PlayerService.SetWindowValue(mainWindow);
        }

        private void ExtractNunitArgs()
        {
            nunitArgs = new string[0];
            if (receivedAppControl.ExtraData.TryGet("--nunit-args", out string unparsed))
            {
                nunitArgs = unparsed.Split(":");
            }
        }
        
        /// <summary>
        /// Extracts GC command line argument.
        /// </summary>
        private void ExtractGCArg()
        {
            if (receivedAppControl.ExtraData.TryGet("--gc-logs", out string gcArg))
            {
                if (!(gcArg.Equals("0") || gcArg.Equals("false", StringComparison.InvariantCultureIgnoreCase)))
                     enableGCLogs = true;
            }
        }

        private void RunTests(Assembly assembly)
        {
            StringBuilder sb = new StringBuilder();
            string dllName = assembly.ManifestModule.ToString();

            using (ExtendedTextWrapper writer = new ExtendedTextWrapper(new StringWriter(sb)))
            {
                string[] finalNunitArgs = nunitArgs.Concat(new string[]
                    {"--result=/tmp/" + Path.GetFileNameWithoutExtension(dllName) + ".xml", "--work=/tmp"}).ToArray();
                new AutoRun(assembly).Execute(finalNunitArgs, writer, Console.In);
            }

            foreach (var line in sb.ToString().Split("\n"))
                Logger.Info(line);
        }

        private void RunJuvoPlayerTizenTests()
        {
            RunTests(typeof(Program).GetTypeInfo().Assembly);
        }

        private void RunJuvoPlayerTests()
        {
            Assembly.Load("JuvoPlayer.Tests");
            RunTests(GetAssemblyByName("JuvoPlayer.Tests"));
        }

        protected override async void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            receivedAppControl = e.ReceivedAppControl;
            ExtractNunitArgs();
            ExtractGCArg();
            GCLogger gcLogger = null;
            
            if (enableGCLogs)
            {
                gcLogger = new GCLogger();
                gcLogger.Start(TimeSpan.FromMilliseconds(1000));
            }

            using (gcLogger)
            {
                await Task.Factory.StartNew(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
                    RunJuvoPlayerTizenTests();
                    RunJuvoPlayerTests();
                }, TaskCreationOptions.LongRunning);
            }

            Exit();
        }

        static void Main(string[] args)
        {
            TizenLoggerManager.Configure();
            Program program = new Program();
            program.Run(args);
        }
    }
}
