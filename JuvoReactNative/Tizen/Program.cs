﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using JuvoPlayer.Common;
using ReactNative;
using ReactNative.Shell;
using ReactNative.Modules.Core;
using JuvoLogger;
using JuvoLogger.Tizen;
using ILogger = JuvoLogger.ILogger;
using Log = Tizen.Log;
using Tizen.Applications;
using ReactNative.UIManager.Events;
using System.Threading.Tasks;

namespace JuvoReactNative
{
    class ReactNativeApp : ReactProgram
    {        
        private static ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");
        public static readonly string Tag = "JuvoRN";
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
                    new JuvoPlayerReactPackage()
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
        }
        public void ShutDown()
        {
            Log.Error(Tag, "Shutting down...");
            ReactNativeApp app = (ReactNativeApp)Application.Current;
            app.Dispose();
            app.Exit();
        }
        protected override async void OnAppControlReceived(AppControlReceivedEventArgs e)
        {
            //TODO - EDEN preview needs to be implemented here.            
            ReactNativeApp app = (ReactNativeApp) Application.Current;            
            var payloadParser = new PayloadParser(e.ReceivedAppControl);
            if (!payloadParser.TryGetUrl(out var url)) 
               return;
            Task result = LoadUrl(url);
            await result;            
            base.OnAppControlReceived(e);
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
        public async Task LoadUrl(string url)
        {
            await Task.Run( () => {
                foreach (IReactPackage package in Packages)
                {
                    if (package.GetType() == typeof(JuvoPlayerReactPackage))
                    {                        
                        JuvoPlayerReactPackage pkg = (JuvoPlayerReactPackage)package;                       
                    }
                }
            });
            return;
        }
    }
}