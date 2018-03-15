using NUnitLite;
using NUnit.Common;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using JuvoLogger;
using JuvoLogger.Tizen;
using Tizen.Applications;

namespace JuvoPlayer.TizenTests
{
    class Program : ServiceApplication
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("UT");

        Assembly GetAssemblyByName(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().
                SingleOrDefault(assembly => assembly.GetName().Name == name);
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            RunTests(typeof(Program).GetTypeInfo().Assembly);
            RunTests(GetAssemblyByName("JuvoPlayer.Tests"));

            global::System.Environment.Exit(0);
        }

        private static void RunTests(Assembly assembly)
        {
            StringBuilder sb = new StringBuilder();
            string dllName = assembly.ManifestModule.ToString();

            using (ExtendedTextWrapper writer = new ExtendedTextWrapper(new StringWriter(sb)))
            {
                new AutoRun(assembly).Execute(
                    new string[] {"--result=/tmp/" + Path.GetFileNameWithoutExtension(dllName) + ".xml"}, writer, Console.In);
            }

            Logger.Info(sb.ToString());
        }

        static void Main(string[] args)
        {
            TizenLoggerManager.Configure();
            Program program = new Program();
            program.Run(args);
        }
    }
}