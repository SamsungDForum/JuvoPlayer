using NUnitLite;
using NUnit.Common;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using JuvoPlayer.Common.Logging;
using JuvoPlayer.Logging;
using Tizen.Applications;

namespace JuvoPlayer.TizenTests
{
    class Program : ServiceApplication
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("UT");

        protected override void OnCreate()
        {
            base.OnCreate();
            StringBuilder sb = new StringBuilder();
            string dllName = typeof(Program).GetTypeInfo().Assembly.ManifestModule.ToString();

            using (ExtendedTextWrapper writer = new ExtendedTextWrapper(new StringWriter(sb)))
            {
                new AutoRun(typeof(Program).GetTypeInfo().Assembly).Execute(new string[] { "--result=/tmp/" + Path.GetFileNameWithoutExtension(dllName) + ".xml" }, writer, Console.In);
            }
            Logger.Info(sb.ToString());

            global::System.Environment.Exit(0);
        }

        static void Main(string[] args)
        {
            TizenLoggerManager.Configure();
            Program program = new Program();
            program.Run(args);
        }
    }
}