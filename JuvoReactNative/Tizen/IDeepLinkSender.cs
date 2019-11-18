namespace JuvoReactNative
{
    public delegate void OnDeepLinkReceived(string url);

    public interface IDeepLinkSender
    {
        event OnDeepLinkReceived OnDeepLinkReceived;
    }
}