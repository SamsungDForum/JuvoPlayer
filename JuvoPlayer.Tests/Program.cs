using System;
using System.Reflection;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tizen;
using NUnitLite;
using NUnit.Common;

namespace JuvoPlayer.Tests
{
    public class Program
    {
        private const string Tag = "UT";
        private static string[] globalArgs;

        public delegate void ecore_idle_callback(IntPtr data);
        public delegate int ecore_timer_callback(IntPtr data);

        [DllImport("/usr/lib/libecore.so.1")]
        private static extern void ecore_init();

        [DllImport("/usr/lib/libecore.so.1")]
        private static extern void ecore_idler_add(ecore_idle_callback cb, IntPtr data);

        [DllImport("/usr/lib/libecore.so.1")]
        private static extern int ecore_timer_add(double timeout, ecore_timer_callback cb, IntPtr data);

        [DllImport("/usr/lib/libecore.so.1")]
        private static extern void ecore_main_loop_begin();

        [DllImport("/usr/lib/libecore.so.1")]
        private static extern void ecore_shutdown();

        [DllImport("/usr/lib/libecore.so.1")]
        private static extern void ecore_main_loop_quit();

        private static bool needExitMainLoop = false;

        private static void ThreadEntry()
        {
            needExitMainLoop = false;
            StringBuilder sb = new StringBuilder();

            using (ExtendedTextWrapper writer = new ExtendedTextWrapper(new StringWriter(sb)))
            {
                new AutoRun(typeof(Program).GetTypeInfo().Assembly).Execute(globalArgs, writer, Console.In);
            }

            Log.Info(Tag, sb.ToString());
            needExitMainLoop = true;
        }

        private static void MainEntry()
        {
            Thread t = new Thread(ThreadEntry);
            t.Start();
        }

        public static void Main(string[] args)
        {
            Log.Info(Tag, "==================== Main ====================");
            string dllName = typeof(Program).GetTypeInfo().Assembly.ManifestModule.ToString();
            List<string> argList = new List<string>();
            argList.Add("--result=/tmp/" + Path.GetFileNameWithoutExtension(dllName) + ".xml");
            globalArgs = argList.ToArray<string>();

            ecore_init();

            ecore_timer_add(1, (value) =>
            {
                if (needExitMainLoop)
                {
                    ecore_main_loop_quit();
                }
                return 1;
            }, IntPtr.Zero);

            ecore_idler_add((value) =>
            {
                Log.Info(Tag, "==================== App Start ====================");
                MainEntry();
            }, IntPtr.Zero);

            ecore_main_loop_begin();

            ecore_shutdown();

            Log.Info(Tag, "==================== App Terminated ====================");
        }
    }
}
