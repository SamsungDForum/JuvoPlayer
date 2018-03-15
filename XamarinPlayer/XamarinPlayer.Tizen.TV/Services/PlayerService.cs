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
        public event ShowSubtitleEventHandler ShowSubtitle;
        
        public TimeSpan Duration => playerController?.ClipDuration ?? TimeSpan.FromSeconds(0) ;

        public TimeSpan CurrentPosition => dataProvider == null ? TimeSpan.FromSeconds(0) : playerController.CurrentTime;

        public bool IsSeekingSupported => dataProvider?.IsSeekingSupported() ?? false;

        public PlayerState State
        {
            get => playerState;
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

            var drmManager = new DrmManager();
            drmManager.RegisterDrmHandler(new CencHandler());
            drmManager.RegisterDrmHandler(new DummyDrmHandler());

            var player = new SMPlayer();
            playerController = new PlayerController(player, drmManager);
            playerController.PlaybackCompleted += () =>
            {
                State = PlayerState.Completed;
            };
            playerController.ShowSubtitle += (subtitle) =>
            {
                var sub = new XamarinPlayer.Services.Subtitle
                {
                    Duration = subtitle.Duration,
                    Text = subtitle.Text
                };
                ShowSubtitle?.Invoke(this, new ShowSubtitleEventArgs(sub));
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

        public void ChangeStream(Stream stream)
        {
            var streamDefinition = new StreamDefinition()
            {
                Id = stream.Id,
                Lang = stream.Lang,
                StreamType = ToJuvoStreamType(stream.Type)
            };

            dataProvider.OnChangeRepresentation(streamDefinition);
        }

        public List<Stream> GetStreams(Stream.StreamType streamType)
        {
            var streams = dataProvider.GetStreams(ToJuvoStreamType(streamType));
            return streams.Select(o => new Stream() { Id = o.Id, Lang = o.Lang, Type = ToStreamType(o.StreamType) }).ToList();
        }

        private StreamType ToJuvoStreamType(Stream.StreamType streamType)
        {
            switch (streamType)
            {
                case Stream.StreamType.Audio:
                    return StreamType.Audio;
                case Stream.StreamType.Video:
                    return StreamType.Video;
                case Stream.StreamType.Subtitle:
                    return StreamType.Subtitle;
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        private Stream.StreamType ToStreamType(StreamType streamType)
        {
            switch (streamType)
            {
                case StreamType.Audio:
                    return Stream.StreamType.Audio;
                case StreamType.Video:
                    return Stream.StreamType.Video;
                case StreamType.Subtitle:
                    return Stream.StreamType.Subtitle;
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
                DataProviderConnector.Disconnect(playerController, dataProvider);
                playerController?.Dispose();
                playerController = null;
                dataProvider?.Dispose();
                dataProvider = null;
                GC.Collect();
            }
        }
    }
}
