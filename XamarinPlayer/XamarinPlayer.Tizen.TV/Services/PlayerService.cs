using System;
using System.Collections.Generic;
using System.Linq;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.DataProviders;
using JuvoPlayer.DataProviders.Dash;
using JuvoPlayer.DataProviders.HLS;
using JuvoPlayer.DataProviders.RTSP;
using JuvoPlayer.Drms;
using JuvoPlayer.Drms.Cenc;
using JuvoPlayer.Drms.DummyDrm;
using JuvoPlayer.Player;
using JuvoPlayer.Player.EsPlayer;
using Xamarin.Forms;
using XamarinPlayer.Services;
using XamarinPlayer.Tizen.Services;
using PlayerState = XamarinPlayer.Services.PlayerState;
using StreamDefinition = XamarinPlayer.Services.StreamDescription;
using StreamType = XamarinPlayer.Services.StreamDescription.StreamType;

[assembly: Dependency(typeof(PlayerService))]
namespace XamarinPlayer.Tizen.Services
{
    sealed class PlayerService : IPlayerService
    {
        private IDataProvider dataProvider;
        private IPlayerController playerController;
        private readonly DataProviderFactoryManager dataProviders = new DataProviderFactoryManager();
        private PlayerState playerState = PlayerState.Idle;
        private string playerStateMessage;
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public event PlayerStateChangedEventHandler StateChanged;

        public TimeSpan Duration => playerController.ClipDuration;

        public TimeSpan CurrentPosition => playerController.CurrentTime;

        public bool IsSeekingSupported => dataProvider.IsSeekingSupported();

        public PlayerState State
        {
            get => playerState;
            private set
            {
                playerState = value;
                if (playerState == PlayerState.Error)
                {
                    StateChanged?.Invoke(this, new PlayerStateChangedStreamError(playerState, playerStateMessage));
                }
                else
                {
                    StateChanged?.Invoke(this, new PlayerStateChangedEventArgs(playerState));
                }
            }
        }

        public string CurrentCueText => dataProvider.CurrentCue?.Text;

        public PlayerService()
        {
            dataProviders.RegisterDataProviderFactory(new DashDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new HLSDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new RTSPDataProviderFactory());

            var drmManager = new DrmManager();
            drmManager.RegisterDrmHandler(new CencHandler());
            drmManager.RegisterDrmHandler(new DummyDrmHandler());

            var player = new EsPlayer();

            playerController = new PlayerController(player, drmManager);
            playerController.StateChanged += (sender, args) =>
            {
                try
                {
                    var state = args.State;
                    State = ToPlayerState(state);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    Logger.Warn($"Unsupported state: {ex.Message}");
                    Logger.Warn($"{ex.StackTrace}");
                }
            };

            playerController.PlaybackError += (message) =>
            {
                lock (playerController)
                {
                    if (State == PlayerState.Error)
                        return;
                    playerStateMessage = message;
                    State = PlayerState.Error;
                }
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

        public void ChangeActiveStream(StreamDefinition stream)
        {
            var streamDescription = new JuvoPlayer.Common.StreamDescription()
            {
                Id = stream.Id,
                Description = stream.Description,
                StreamType = ToJuvoStreamType(stream.Type)
            };

            dataProvider.OnChangeActiveStream(streamDescription);
        }

        public void DeactivateStream(StreamType streamType)
        {
            dataProvider.OnDeactivateStream(ToJuvoStreamType(streamType));
        }

        public List<StreamDefinition> GetStreamsDescription(StreamType streamType)
        {
            var streams = dataProvider.GetStreamsDescription(ToJuvoStreamType(streamType));
            return streams.Select(o =>
                new StreamDefinition()
                {
                    Id = o.Id,
                    Description = o.Description,
                    Default = o.Default,
                    Type = ToStreamType(o.StreamType)
                }).ToList();
        }

        private JuvoPlayer.Common.StreamType ToJuvoStreamType(StreamType streamType)
        {
            switch (streamType)
            {
                case StreamType.Audio:
                    return JuvoPlayer.Common.StreamType.Audio;
                case StreamType.Video:
                    return JuvoPlayer.Common.StreamType.Video;
                case StreamType.Subtitle:
                    return JuvoPlayer.Common.StreamType.Subtitle;
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        private StreamType ToStreamType(JuvoPlayer.Common.StreamType streamType)
        {
            switch (streamType)
            {
                case JuvoPlayer.Common.StreamType.Audio:
                    return StreamType.Audio;
                case JuvoPlayer.Common.StreamType.Video:
                    return StreamType.Video;
                case JuvoPlayer.Common.StreamType.Subtitle:
                    return StreamType.Subtitle;
                default:
                    throw new IndexOutOfRangeException();
            }
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
                    Logger.Warn($"Unsupported state {state}");
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        public void SetSource(object o)
        {
            Logger.Info("");
            if (!(o is ClipDefinition))
                return;
            var clip = o as ClipDefinition;

            DataProviderConnector.Disconnect(playerController, dataProvider);
            dataProvider?.Dispose();

            if (State != PlayerState.Idle)
                Stop();

            State = PlayerState.Preparing;

            dataProvider = dataProviders.CreateDataProvider(clip);

            // TODO(p.galiszewsk) rethink!
            if (clip.DRMDatas != null)
            {
                foreach (var drm in clip.DRMDatas)
                    playerController.OnSetDrmConfiguration(drm);
            }

            DataProviderConnector.Connect(playerController, dataProvider);

            dataProvider.Start();
        }

        public void Start()
        {
            Logger.Info("");

            playerController.OnPlay();

            State = PlayerState.Playing;
        }

        public void Stop()
        {
            if (State == PlayerState.Stopped)
                return;

            Logger.Info("");

            // Stop data provider first so no new data is fed to player while
            // it is being stopped.
            //
            dataProvider.OnStopped();

            playerController.OnStop();

            //prevent the callback from firing multiple times
            if (State != PlayerState.Stopped)
                State = PlayerState.Stopped;
        }

        public void Dispose()
        {
            Logger.Info("");

            DataProviderConnector.Disconnect(playerController, dataProvider);

            // Stop Data provider during dispose AFTER disconnecting data privider and controller.
            // Events propagated during stop (when disposing) are no longer needed nor required.
            // Stop is issued here as exit without prior content end does not invoke Stop() method.
            dataProvider.OnStopped();

            playerController.Dispose();
            dataProvider.Dispose();

            Int32 gcMaxGeneration = Math.Max(GC.GetGeneration(playerController), GC.GetGeneration(dataProvider));
            playerController = null;
            dataProvider = null;
            GC.Collect(gcMaxGeneration, GCCollectionMode.Forced, false, true); //non-blocking (if possible), compacting
        }
    }
}
