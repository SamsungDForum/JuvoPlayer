using JuvoPlayer;
using JuvoPlayer.Common;
using JuvoPlayer.Common.Delegates;
using JuvoPlayer.Dash;
using JuvoPlayer.HLS;
using JuvoPlayer.Player;
using JuvoPlayer.RTSP;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Tizen;
using XamarinPlayer.Services;
using XamarinPlayer.Tizen.Services;

[assembly: Dependency(typeof(PlayerService))]
namespace XamarinPlayer.Tizen.Services
{
    class PlayerService : IPlayerService, IDisposable
    {
        private IDataProvider dataProvider;
        private IPlayerController playerController;
        private DataProviderFactoryManager dataProviders;
        private PlayerState playerState = PlayerState.Idle;

        private double currentTime = 0;

        public event PlayerStateChangedEventHandler StateChanged;

        public event PlaybackCompleted PlaybackCompleted;
        public event ShowSubtitile ShowSubtitle;

        //TODO(p.galiszewsk):
        public int Duration => dataProvider == null ? 0 : 100000;

        public int CurrentPosition => dataProvider == null ? 0 : (int)currentTime;

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

            var playerAdapter = new SMPlayerAdapter();
            playerController = new PlayerController(playerAdapter);
            playerController.PlaybackCompleted += () =>
            {
                PlaybackCompleted?.Invoke();
                State = PlayerState.Stopped;
            };
            playerController.ShowSubtitle += (subtitle) =>
            {
                ShowSubtitle?.Invoke(subtitle);
            };
            playerController.TimeUpdated += OnTimeUpdated;
        }

        private void OnTimeUpdated(double time)
        {
            currentTime = time;
        }

        public void Pause()
        {
            playerController.OnPause();
            State = PlayerState.Paused;
        }

        public void SeekTo(int to)
        {
            playerController.OnSeek(to);
        }

        public void SetSource(ClipDefinition clip)
        {
            dataProvider = dataProviders.CreateDataProvider(clip);

            playerController.SetDataProvider(dataProvider);

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
                playerController.Dispose();
            }
        }
    }
}
