using System;
using System.Collections.Generic;
using System.IO;
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
using JuvoPlayer.Utils;

namespace JuvoPlayer.TizenTests.Utils
{
    class PlayerService : IDisposable
    {
        public enum PlayerState
        {
            Error = -1,
            Idle,
            Prepared,
            Stopped,
            Playing,
            Paused,
            Completed
        }

        public List<ClipDefinition> ReadClips()
        {
            var applicationPath = Paths.ApplicationPath;
            var clipsPath = Path.Combine(applicationPath, "res", "videoclips.json");
            return JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(clipsPath).ToList();
        }

        private IDataProvider dataProvider;
        private IPlayerController playerController;
        private readonly DataProviderFactoryManager dataProviders;

        public TimeSpan Duration => playerController?.ClipDuration ?? TimeSpan.FromSeconds(0);

        public TimeSpan CurrentPosition => dataProvider == null ? TimeSpan.FromSeconds(0) : playerController.CurrentTime;

        public bool IsSeekingSupported => dataProvider?.IsSeekingSupported() ?? false;

        public PlayerState State { get; private set; } = PlayerState.Idle;

        public string CurrentCueText => dataProvider?.CurrentCue?.Text;

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

        public void ChangeActiveStream(StreamDescription stream)
        {
            dataProvider.OnChangeActiveStream(stream);
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            return dataProvider.GetStreamsDescription(streamType);
        }

        public void SetClipDefinition(ClipDefinition clip)
        {
            DataProviderConnector.Disconnect(playerController, dataProvider);

            dataProvider = dataProviders.CreateDataProvider(clip);

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
