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
using JuvoPlayer.TizenTests.Utils;
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
            PlayerService.SetWindow(mainWindow);
        }

        private void ExtractNunitArgs()
        {
            nunitArgs = new string[0];
            if (receivedAppControl.ExtraData.TryGet("--nunit-args", out string unparsed))
            {
                nunitArgs = unparsed.Split(":");
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

            Logger.Info(sb.ToString());
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

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            receivedAppControl = e.ReceivedAppControl;
            ExtractNunitArgs();
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            Task.Factory.StartNew(() =>
            {
                RunJuvoPlayerTizenTests();
                RunJuvoPlayerTests();
                Environment.Exit(0);
            }, TaskCreationOptions.LongRunning);
        }

        static void Main(string[] args)
        {
            TizenLoggerManager.Configure();
            Program program = new Program();
            program.Run(args);
        }
    }
}