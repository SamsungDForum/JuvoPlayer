using System;
using System.Collections.Generic;

using ReactNative;
using ReactNative.Common;
using ReactNative.Tracing;
using ReactNative.Shell;
using ReactNative.Modules.Core;

using Tizen.Applications;
using Tizen;

namespace TestApp
{
    class TestApp : ReactProgram
    {
        public override string MainComponentName
        {
            get
            {
                return "react_native_template";
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
                return new List<IReactPackage>
                {
                    new MainReactPackage(),
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

        static void Main(string[] args)
        {
            try
            {
                TestApp app = new TestApp();

                app.Run(args);
            }
            catch (Exception e)
            {
                Log.Error("myApp", e.ToString());
            }
        }
    }
}