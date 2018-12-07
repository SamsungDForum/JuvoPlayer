using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.DataProviders;
using JuvoPlayer.DataProviders.Dash;
using JuvoPlayer.DataProviders.HLS;
using JuvoPlayer.DataProviders.RTSP;
using JuvoPlayer.Drms;
using JuvoPlayer.Drms.Cenc;
using JuvoPlayer.Player;
using JuvoPlayer.Player.EsPlayer;
using StreamType = JuvoPlayer.Common.StreamType;

namespace JuvoPlayer.OpenGL
{
    public delegate void PlayerStateChangedEventHandler(object sender, PlayerStateChangedEventArgs e);

    public delegate void SeekCompleted();

    class Player
    {
        private IDataProvider dataProvider;
        private IPlayerController playerController;
        private DataProviderConnector connector;
        private readonly DataProviderFactoryManager dataProviders;
        private PlayerState playerState = PlayerState.Idle;
        private string playerStateMessage;
        private readonly CompositeDisposable subscriptions;
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public event PlayerStateChangedEventHandler StateChanged;
        public event SeekCompleted SeekCompleted;

        public TimeSpan Duration => playerController?.ClipDuration ?? TimeSpan.FromSeconds(0);

        public TimeSpan CurrentPosition =>
            dataProvider == null ? TimeSpan.FromSeconds(0) : playerController.CurrentTime;

        public bool IsSeekingSupported => dataProvider?.IsSeekingSupported() ?? false;

        public PlayerState State
        {
            get => playerState;
            private set
            {
                playerState = value;
                StateChanged?.Invoke(this,
                    playerState == PlayerState.Error
                        ? new PlayerStateChangedStreamError(playerState, playerStateMessage)
                        : new PlayerStateChangedEventArgs(playerState));
            }
        }

        public string CurrentCueText => dataProvider?.CurrentCue?.Text;

        public Player()
        {
            dataProviders = new DataProviderFactoryManager();
            dataProviders.RegisterDataProviderFactory(new DashDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new HLSDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new RTSPDataProviderFactory());

            var drmManager = new DrmManager();
            drmManager.RegisterDrmHandler(new CencHandler());            

            var player = new EsPlayer();
            playerController = new PlayerController(player, drmManager);

            subscriptions = new CompositeDisposable
            {
                playerController.Initialized()
                    .Subscribe(unit => { State = PlayerState.Prepared; }, SynchronizationContext.Current),

                playerController.SeekCompleted()
                    .Subscribe(unit => SeekCompleted?.Invoke(), SynchronizationContext.Current),

                playerController.PlaybackError().Subscribe(msg =>
                {
                    playerStateMessage = msg;
                    State = PlayerState.Error;
                }, SynchronizationContext.Current),

                playerController.StateChanged()
                    .Subscribe(state =>
                    {
                        try
                        {
                            State = ToPlayerState(state);
                        }
                        catch (ArgumentOutOfRangeException ex)
                        {
                            logger.Warn($"Unsupported state: {ex.Message}");
                            logger.Warn($"{ex.StackTrace}");
                        }
                    }, SynchronizationContext.Current)
            };
        }

        private PlayerState ToPlayerState(JuvoPlayer.Player.PlayerState state)
        {
            switch (state)
            {
                case JuvoPlayer.Player.PlayerState.Uninitialized:
                    return PlayerState.Idle;
                case JuvoPlayer.Player.PlayerState.Ready:
                    return PlayerState.Prepared;
                case JuvoPlayer.Player.PlayerState.Buffering:
                    return PlayerState.Buffering;
                case JuvoPlayer.Player.PlayerState.Paused:
                    return PlayerState.Paused;
                case JuvoPlayer.Player.PlayerState.Playing:
                    return PlayerState.Playing;
                case JuvoPlayer.Player.PlayerState.Finished:
                    return PlayerState.Completed;
                case JuvoPlayer.Player.PlayerState.Error:
                    return PlayerState.Error;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
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

        public void ChangeActiveStream(StreamDescription stream)
        {
            var streamDescription = new StreamDescription()
            {
                Id = stream.Id,
                Description = stream.Description,
                StreamType = stream.StreamType
            };

            dataProvider.OnChangeActiveStream(streamDescription);
        }

        public void DeactivateStream(StreamType streamType)
        {
            dataProvider.OnDeactivateStream(streamType);
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            return dataProvider.GetStreamsDescription(streamType);
        }

        public void SetSource(ClipDefinition clip)
        {
            connector?.Dispose();

            dataProvider = dataProviders.CreateDataProvider(clip);

            if (clip.DRMDatas != null)
            {
                foreach (var drm in clip.DRMDatas)
                    playerController.OnSetDrmConfiguration(drm);
            }

            connector = new DataProviderConnector(playerController, dataProvider);

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
                subscriptions.Dispose();
                connector?.Dispose();
                playerController?.Dispose();
                playerController = null;
                dataProvider?.Dispose();
                dataProvider = null;
                GC.Collect();
            }
        }
    }
}