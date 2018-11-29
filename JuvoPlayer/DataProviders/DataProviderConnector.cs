using System;
using System.Reactive.Disposables;
using System.Threading;
using JuvoPlayer.Player;
using JuvoPlayer.Common;

namespace JuvoPlayer.DataProviders
{
    public class DataProviderConnector : IDisposable
    {
        private CompositeDisposable subscriptions;

        public DataProviderConnector(IPlayerController playerController, IDataProvider dataProvider,
            SynchronizationContext context = null)
        {
            Connect(playerController, dataProvider, context);
        }

        private void Connect(IPlayerController controller, IDataProvider dataProvider,
            SynchronizationContext context)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller), "Player controller cannot be null");

            if (dataProvider == null)
                return;

            subscriptions = new CompositeDisposable
            {
                dataProvider.ClipDurationChanged()
                    .Subscribe(controller.OnClipDurationChanged, context),
                dataProvider.DRMInitDataFound()
                    .Subscribe(controller.OnDRMInitDataFound, context),
                dataProvider.SetDrmConfiguration()
                    .Subscribe(controller.OnSetDrmConfiguration, context),
                dataProvider.StreamConfigReady()
                    .Subscribe(controller.OnStreamConfigReady, context),
                dataProvider.PacketReady()
                    .Subscribe(controller.OnPacketReady, context),
                dataProvider.BufferingStarted()
                    .Subscribe(unit => controller.OnBufferingStarted(), context),
                dataProvider.BufferingCompleted()
                    .Subscribe(unit => controller.OnBufferingCompleted(), context),
                dataProvider.StreamError()
                    .Subscribe(controller.OnStreamError, SynchronizationContext.Current),
                controller.TimeUpdated().Subscribe(dataProvider.OnTimeUpdated, context),
                controller.Paused().Subscribe(unit => dataProvider.OnPaused(), context),
                controller.Played().Subscribe(unit => dataProvider.OnPlayed(), context),
                controller.SeekStarted()
                    .Subscribe(args => dataProvider.OnSeekStarted(args.Position, args.Id), context),
                controller.SeekCompleted()
                    .Subscribe(unit => dataProvider.OnSeekCompleted(), context),
                controller.Stopped().Subscribe(unit => dataProvider.OnStopped(), context)
            };
        }

        public void Dispose()
        {
            subscriptions?.Dispose();
        }
    }
}