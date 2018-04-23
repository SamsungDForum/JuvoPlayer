using System;
using System.Collections.Generic;
using System.Linq;
using JuvoPlayer.Common;
using JuvoPlayer.DataProviders;
using JuvoPlayer.DataProviders.Dash;
using JuvoPlayer.DataProviders.HLS;
using JuvoPlayer.DataProviders.RTSP;
using JuvoPlayer.Drms;
using JuvoPlayer.Drms.Cenc;
using JuvoPlayer.Drms.DummyDrm;
using JuvoPlayer.Player;
using JuvoPlayer.Player.SMPlayer;
using Xamarin.Forms;
using XamarinPlayer.Services;
using XamarinPlayer.Tizen.Services;

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
                StateChanged?.Invoke(this, new PlayerStateChangedEventArgs(playerState));
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

            var player = new SMPlayer();
            playerController = new PlayerController(player, drmManager);
            playerController.PlaybackCompleted += () =>
            {
                State = PlayerState.Completed;
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

        public void SetSource(object o)
        {
            if (!(o is ClipDefinition))
                return;
            var clip = o as ClipDefinition;

            DataProviderConnector.Disconnect(playerController, dataProvider);
            dataProvider?.Dispose();

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
            playerController.OnPlay();

            State = PlayerState.Playing;
        }

        public void Stop()
        {
            playerController.OnStop();

            //prevent the callback from firing multiple times
            if (this.State != PlayerState.Stopped)
            {
                State = PlayerState.Stopped;
            }
        }

        public void Dispose()
        {
            DataProviderConnector.Disconnect(playerController, dataProvider);
            playerController.Dispose();
            dataProvider.Dispose();
        }
    }
}
