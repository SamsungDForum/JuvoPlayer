using System;
using JuvoPlayer;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Delegates;
using JuvoPlayer.Dash;
using JuvoPlayer.DRM;
using JuvoPlayer.DRM.Cenc;
using JuvoPlayer.HLS;
using JuvoPlayer.Player;
using JuvoPlayer.RTSP;
using Xamarin.Forms;
using XamarinPlayer.Services;
using XamarinPlayer.Tizen.Services;

[assembly: Dependency(typeof(PlayerService))]
namespace XamarinPlayer.Tizen.Services
{
    class PlayerService : IPlayerService
    {
        private IDataProvider dataProvider;
        private IPlayerController playerController;
        private readonly DataProviderFactoryManager dataProviders;
        private PlayerState playerState = PlayerState.Idle;

        public event PlayerStateChangedEventHandler StateChanged;

        public event PlaybackCompleted PlaybackCompleted;
        public event ShowSubtitile ShowSubtitle;
        
        public TimeSpan Duration => playerController?.ClipDuration ?? TimeSpan.FromSeconds(0) ;

        public TimeSpan CurrentPosition => dataProvider == null ? TimeSpan.FromSeconds(0) : playerController.CurrentTime;

        public PlayerState State
        {
            get { return playerState; }
            private set
            {
                playerState = value;
                StateChanged?.Invoke(this, new PlayerStateChangedEventArgs(playerState));
            }
        }

        public PlayerService()
        {
            dataProviders = new DataProviderFactoryManager();
            dataProviders.RegisterDataProviderFactory(new DashDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new HLSDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new RTSPDataProviderFactory());

            var drmManager = new DRMManager();
            drmManager.RegisterDrmHandler(new CencHandler());

            var playerAdapter = new SMPlayerAdapter();
            playerController = new PlayerController(playerAdapter, drmManager);
            playerController.PlaybackCompleted += () =>
            {
                PlaybackCompleted?.Invoke();
                State = PlayerState.Stopped;
            };
            playerController.ShowSubtitle += (subtitle) =>
            {
                ShowSubtitle?.Invoke(subtitle);
            };
            playerController.PlayerInitialized += () =>
            {
                State = PlayerState.Prepared;
            };
            playerController.PlaybackError += (message) =>
            {
                State = PlayerState.Error;
            };
        }

        public void Pause()
        {
            playerController.OnPause();
            State = PlayerState.Paused;
        }

        public void SeekTo(TimeSpan to)
        {
            playerController.OnSeek(to);
        }

        public void SetSource(ClipDefinition clip)
        {
            ControllerConnector.DisconnectDataProvider(playerController, dataProvider);

            dataProvider = dataProviders.CreateDataProvider(clip);

            // TODO(p.galiszewsk) rethink!
            if (clip.DRMDatas != null)
            {
                foreach (var drm in clip.DRMDatas)
                    playerController.OnSetDrmConfiguration(drm);
            }

            ControllerConnector.ConnectDataProvider(playerController, dataProvider);

            dataProvider.Start();
        }

        public void Start()
        {
            playerController.OnPlay();

            State = PlayerState.Playing;
        }

        public void Stop()
        {
            playerController.OnStop();

            State = PlayerState.Stopped;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ControllerConnector.DisconnectDataProvider(playerController, dataProvider);
                playerController?.Dispose();
                playerController = null;
                dataProvider?.Dispose();
                dataProvider = null;
                GC.Collect();
            }
        }
    }
}
