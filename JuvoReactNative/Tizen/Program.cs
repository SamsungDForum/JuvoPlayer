using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactNative;
using ReactNative.Shell;
using ReactNative.Modules.Core;
using JuvoLogger;
using JuvoLogger.Tizen;
using JuvoPlayer.Common;
using ILogger = JuvoLogger.ILogger;
using Log = Tizen.Log;
using Tizen.Applications;

namespace JuvoReactNative
{
    class ReactNativeApp : ReactProgram, IDeepLinkSender
    {
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");
        public static readonly string Tag = "JuvoRN";

        private BehaviorSubject<string> deepLinkReceivedSubject = new BehaviorSubject<string>(null);
        public override string MainComponentName
        {
            get
            {
                return "JuvoReactNative";
            }
        }
        public override string JavaScriptMainModuleName
        {
            get
            {
                return "index.tizen";
            }
        }
#if !DEBUG
        public override string JavaScriptBundleFile
        {
            get
            {
                return Application.Current.DirectoryInfo.SharedResource + "index.tizen.bundle";
            }
        }
#endif
        public override List<IReactPackage> Packages
        {
            get
            {
                Log.Error(Tag, "Packages loading...");
                return new List<IReactPackage>
                {
                    new MainReactPackage(),
                    new JuvoPlayerReactPackage(this)
                };
            }
        }
        public override bool UseDeveloperSupport
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }
        static void UnhandledException(object sender, UnhandledExceptionEventArgs evt)
        {
            if (evt.ExceptionObject is Exception e)
            {
                if (e.InnerException != null)
                    e = e.InnerException;

                Log.Error(Tag, e.Message);
                Log.Error(Tag, e.StackTrace);
            }
            else
            {
                Log.Error(Tag, "Got unhandled exception event: " + evt);
            }
        }
        protected override void OnCreate()
        {
            base.OnCreate();
            Log.Error(Tag, "OnCreate()...");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ServicePointManager.DefaultConnectionLimit = 100;
            RootView.BackgroundColor = ElmSharp.Color.Transparent;
        }

        protected override void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            base.OnAppControlReceived(e);
            var payloadParser = new PayloadParser(e.ReceivedAppControl);
            if (!payloadParser.TryGetUrl(out var url))
                return;
            deepLinkReceivedSubject.OnNext(url);
        }

        public IObservable<string> DeepLinkReceived()
        {
            return deepLinkReceivedSubject.AsObservable();
        }

        static void Main(string[] args)
        {
            try
            {
                TizenLoggerManager.Configure();
                AppDomain.CurrentDomain.UnhandledException += UnhandledException;
                ReactNativeApp app = new ReactNativeApp();
                app.Run(args);
            }
            catch (Exception e)
            {
                Log.Error(Tag, e.ToString());
            }
        }
    }
}