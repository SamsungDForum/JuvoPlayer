using System;
using System.Collections.Generic;
using System.Threading;
using JuvoLogger;
using ReactNative.Bridge;
using ReactNative.Modules.Core;
using ReactNative.UIManager;

namespace JuvoReactNative
{
    public class JuvoPlayerReactPackage : IReactPackage
    {
        private ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoRN");
        public readonly string Tag = "JuvoRN";
        private readonly IDeepLinkSender deepLinkSender;

        public JuvoPlayerReactPackage(IDeepLinkSender deepLinkSender)
        {
            this.deepLinkSender = deepLinkSender;
        }

        public IReadOnlyList<INativeModule> CreateNativeModules(ReactContext reactContext)
        {
            return new List<INativeModule>
            {
                new JuvoPlayerModule(reactContext, deepLinkSender, SynchronizationContext.Current)
        };
        }
        public IReadOnlyList<Type> CreateJavaScriptModulesConfig()
        {
            return new List<Type>(0);
        }
        public IReadOnlyList<IViewManager> CreateViewManagers(
            ReactContext reactContext)
        {
            return new List<IViewManager>(0);
        }

    }
}
