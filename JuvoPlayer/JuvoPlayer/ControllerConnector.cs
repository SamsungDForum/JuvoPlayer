using JuvoPlayer.Common;
using JuvoPlayer.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JuvoPlayer
{
    public static class ControllerConnector
    {
        public static void ConnectDataProvider(IPlayerController controller, IDataProvider newDataProvider)
        {
            if (controller == null)
                throw new ArgumentNullException("Player controller cannot be null");

            if (newDataProvider == null)
                return;

            newDataProvider.ClipDurationChanged += controller.OnClipDurationChanged;
            newDataProvider.DRMDataFound += controller.OnDrmDataFound;
            newDataProvider.StreamConfigReady += controller.OnStreamConfigReady;
            newDataProvider.StreamPacketReady += controller.OnStreamPacketReady;
            newDataProvider.StreamsFound += controller.OnStreamsFound;

            controller.TimeUpdated += newDataProvider.OnTimeUpdated;
            controller.Paused += newDataProvider.OnPaused;
            controller.Played += newDataProvider.OnPlayed;
            controller.Stopped += newDataProvider.OnPlayed;
        }

        public static void DisconnectDataProvider(IPlayerController controller, IDataProvider oldDataProvider)
        {
            if (controller == null)
                throw new ArgumentNullException("Player controller cannot be null");

            if (oldDataProvider == null)
                return;

            oldDataProvider.ClipDurationChanged -= controller.OnClipDurationChanged;
            oldDataProvider.DRMDataFound -= controller.OnDrmDataFound;
            oldDataProvider.StreamConfigReady -= controller.OnStreamConfigReady;
            oldDataProvider.StreamPacketReady -= controller.OnStreamPacketReady;
            oldDataProvider.StreamsFound -= controller.OnStreamsFound;

            controller.TimeUpdated -= oldDataProvider.OnTimeUpdated;
            controller.Paused -= oldDataProvider.OnPaused;
            controller.Played -= oldDataProvider.OnPlayed;
            controller.Stopped -= oldDataProvider.OnPlayed;
        }
    }
}
