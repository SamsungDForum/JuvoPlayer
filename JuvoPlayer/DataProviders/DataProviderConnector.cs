using System;
using JuvoPlayer.Player;

namespace JuvoPlayer.DataProviders
{
    public static class DataProviderConnector
    {
        public static void Connect(IPlayerController controller, IDataProvider newDataProvider)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller), "Player controller cannot be null");

            if (newDataProvider == null)
                return;

            newDataProvider.ClipDurationChanged += controller.OnClipDurationChanged;
            newDataProvider.DRMInitDataFound += controller.OnDRMInitDataFound;
            newDataProvider.SetDrmConfiguration += controller.OnSetDrmConfiguration;
            newDataProvider.StreamConfigReady += controller.OnStreamConfigReady;
            newDataProvider.PacketReady += controller.OnPacketReady;

            controller.TimeUpdated += newDataProvider.OnTimeUpdated;
            controller.Paused += newDataProvider.OnPaused;
            controller.Played += newDataProvider.OnPlayed;
            controller.Seek += newDataProvider.OnSeek;
            controller.Stopped += newDataProvider.OnStopped;
        }

        public static void Disconnect(IPlayerController controller, IDataProvider oldDataProvider)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller), "Player controller cannot be null");

            if (oldDataProvider == null)
                return;

            oldDataProvider.ClipDurationChanged -= controller.OnClipDurationChanged;
            oldDataProvider.DRMInitDataFound -= controller.OnDRMInitDataFound;
            oldDataProvider.SetDrmConfiguration -= controller.OnSetDrmConfiguration;
            oldDataProvider.StreamConfigReady -= controller.OnStreamConfigReady;
            oldDataProvider.PacketReady -= controller.OnPacketReady;

            controller.TimeUpdated -= oldDataProvider.OnTimeUpdated;
            controller.Paused -= oldDataProvider.OnPaused;
            controller.Played -= oldDataProvider.OnPlayed;
            controller.Seek -= oldDataProvider.OnSeek;
            controller.Stopped -= oldDataProvider.OnStopped;
        }
    }
}
