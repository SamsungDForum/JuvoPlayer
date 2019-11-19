using System;

namespace JuvoReactNative
{
    public interface IDeepLinkSender
    {
        IObservable<string> DeepLinkReceived();
    }
}