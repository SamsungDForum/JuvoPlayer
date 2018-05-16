using NUnitLite;
using NUnit.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoLogger.Tizen;
using Tizen.Applications;

namespace JuvoPlayer.TizenTests
{
    class Program : ServiceApplication
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("UT");
        private ReceivedAppControl receivedAppControl;
        private string[] nunitArgs;

        private static Assembly GetAssemblyByName(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().
                SingleOrDefault(assembly => assembly.GetName().Name == name);
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
                string[] finalNunitArgs = nunitArgs.Concat(new string[] { "--result=/tmp/" + Path.GetFileNameWithoutExtension(dllName) + ".xml" }).ToArray();
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
                global::System.Environment.Exit(0);
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