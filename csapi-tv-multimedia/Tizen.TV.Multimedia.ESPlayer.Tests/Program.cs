using System;
using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Common;
using NUnitLite;

namespace Tizen.TV.Multimedia.ESPlayer.Tests
{
    public class Program
    {
        private const string Tag = "Tizen.Multimedia.ESPlayer";
        private static string[] globalArgs;

        public delegate void ecore_idle_callback(IntPtr data);
        public delegate int ecore_timer_callback(IntPtr data);

        [DllImport("/usr/lib/libelementary.so.1")]
        private static extern void elm_init(int argc, string[] args);

        [DllImport("/usr/lib/libecore.so.1")]
        private static extern void ecore_idler_add(ecore_idle_callback cb, IntPtr data);

        [DllImport("/usr/lib/libecore.so.1")]
        private static extern int ecore_timer_add(double timeout, ecore_timer_callback cb, IntPtr data);

        [DllImport("/usr/lib/libelementary.so.1")]
        private static extern void elm_run();

        [DllImport("/usr/lib/libelementary.so.1")]
        private static extern void elm_shutdown();

        [DllImport("/usr/lib/libelementary.so.1")]
        private static extern void elm_exit();

        private static bool breaker = false;

        private static void Entry()
        {
            Log.Info(Tag, "start");

            breaker = false;
            StringBuilder sb = new StringBuilder();

            using (ExtendedTextWrapper writer = new ExtendedTextWrapper(new StringWriter(sb)))
            {
                new AutoRun(typeof(Program).GetTypeInfo().Assembly).Execute(globalArgs, writer, Console.In);
            }

            Log.Info(Tag, sb.ToString());
            breaker = true;

            Log.Info(Tag, "end");
        }

        public static void Main(string[] args)
        {
            Log.Info(Tag, "==================== Main ====================");
            string dllName = typeof(Program).GetTypeInfo().Assembly.ManifestModule.ToString();
            List<string> argList = new List<string>();
            argList.Add("--result=/tmp/" + Path.GetFileNameWithoutExtension(dllName) + ".xml");

            string testcases = "--test=";
            testcases += "Tizen.TV.Multimedia.ESPlayer.Tests.TSESPlayer.ESPlayer_SetDrm_Test";
            argList.Add(testcases); // If you want to pick a single testcase, set '--test'

            globalArgs = argList.ToArray<string>();

            elm_init(args.Length, args);

            ecore_timer_add(1.0, (value) =>
            {
                if (breaker)
                {
                    Log.Info(Tag, "All tc is finished.");
                    elm_exit();
                    return 0;
                }

                return 1;
            }, IntPtr.Zero);

            ecore_idler_add((value) =>
            {
                Log.Info(Tag, "==================== App Start ====================");
                Entry();
            }, IntPtr.Zero);

            elm_run();
            elm_shutdown();

            Log.Info(Tag, "==================== App Terminated ====================");
        }
    }
}