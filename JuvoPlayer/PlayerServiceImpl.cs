/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using ElmSharp;
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
using JuvoPlayer.Utils;

namespace JuvoPlayer
{
    public class PlayerServiceImpl : IPlayerService
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private IDataProvider dataProvider;
        private IPlayerController playerController;
        private DataProviderConnector connector;
        private readonly DataProviderFactoryManager dataProviders;

        private CompositeDisposable _playerControllerConnections;
        private readonly CompositeDisposable _playerControllerDisposables;

        public TimeSpan Duration => playerController?.ClipDuration ?? TimeSpan.FromSeconds(0);

        public TimeSpan CurrentPosition { get; private set; }

        public bool IsSeekingSupported => dataProvider?.IsSeekingSupported() ?? false;

        public PlayerState State { get; private set; }

        public string CurrentCueText => dataProvider?.CurrentCue?.Text;

        private Window playerWindow;
        private readonly IDrmManager drmManager;

        // Dispatch PlayerState through behavior subject. Any "late subscribers" will receive
        // current state upon subscription.
        private readonly Subject<PlayerState> _playerStateSubject = new Subject<PlayerState>();
        private readonly Subject<string> _playerErrorSubject = new Subject<string>();
        private readonly Subject<int> _playerBufferingSubject = new Subject<int>();
        private readonly Subject<TimeSpan> _playerClockSubject = new Subject<TimeSpan>();

        private readonly SynchronizationContext _syncCtx;

        private IDisposable _configurationSub;

        private IPlayer _player;

        public PlayerServiceImpl()
        {
            if (SynchronizationContext.Current == null)
                throw new ArgumentNullException(nameof(SynchronizationContext.Current), "Null synchronization context");

            _syncCtx = SynchronizationContext.Current;

            _playerControllerDisposables = new CompositeDisposable
            {
                _playerStateSubject,
                _playerErrorSubject,
                _playerBufferingSubject,
                _playerClockSubject
            };


            dataProviders = new DataProviderFactoryManager();
            dataProviders.RegisterDataProviderFactory(new DashDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new HLSDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new RTSPDataProviderFactory());

            drmManager = new DrmManager();
            drmManager.RegisterDrmHandler(new CencHandler());
        }

        public void SetWindow(Window window)
        {
            playerWindow = window;

            CreatePlayerController();
            ConnectPlayerControllerObservables();
        }

        private void CreatePlayerController(object playerStateSnapshot = null)
        {
            if (playerWindow == null)
                playerWindow = WindowUtils.CreateElmSharpWindow();
            _player = new EsPlayer(playerWindow, playerStateSnapshot);

            playerController = new PlayerController(_player, drmManager);
        }

        private void ConnectPlayerControllerObservables()
        {
            _playerControllerConnections = new CompositeDisposable
            {
                playerController.StateChanged().Subscribe(SetState,_syncCtx),
                playerController.StateChanged().Subscribe(_playerStateSubject),
                playerController.PlaybackError().Subscribe( _playerErrorSubject),
                playerController.BufferingProgress().Subscribe(_playerBufferingSubject),
                playerController.PlayerClock().Subscribe(_playerClockSubject),
                playerController.TimeUpdated().Subscribe(SetClock,_syncCtx),

            };
        }

        private async Task OnNewConfiguration(bool reconfigurationRequired)
        {
            Logger.Info($"Reconfigure: {reconfigurationRequired}");

            _configurationSub?.Dispose();
            _configurationSub = null;

            if (reconfigurationRequired)
            {
                // Data provider must be stopped prior to player controller
                // re-creation. Otherwise data provider may fill player with packets
                // before calling seek.
                dataProvider.Pause();
                var playerStateSnapshot = EsPlayer.GetStateSnapshot(_player);

                _playerControllerConnections.Dispose();
                connector?.Dispose();

                playerController.OnStop();
                playerController?.Dispose();

                CreatePlayerController(playerStateSnapshot);
                ConnectPlayerControllerObservables();

                connector = new DataProviderConnector(playerController, dataProvider);

                // Seek resumes data provider
                await dataProvider.Seek(CurrentPosition, CancellationToken.None);
                return;
            }

            await SeekTo(CurrentPosition);
        }
        private void SetClock(TimeSpan clock) =>
            CurrentPosition = clock;

        private void SetState(PlayerState state) =>
            State = state;

        public void Pause()
        {
            playerController.OnPause();
        }

        public Task SeekTo(TimeSpan to)
        {
            return playerController.OnSeek(to);
        }

        public void ChangeActiveStream(StreamDescription streamDescription)
        {
            // Note: Although we should get away with current model as video changes are not destructive
            // and audio does not use adaptive streaming, handler may pick up different stream change
            // then one about to be invoked: 
            // - adaptive stream switch
            // - Manual representation change
            // - First stream config may come from adaptive streaming
            if (streamDescription.StreamType == StreamType.Audio || streamDescription.StreamType == StreamType.Video)
            {
                _configurationSub = playerController.ConfigurationChanged(streamDescription.StreamType)
                    .Subscribe(async r => await OnNewConfiguration(r).ConfigureAwait(false), _syncCtx);
            }

            dataProvider.ChangeActiveStream(streamDescription);
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
            drmManager.ClearCache();
            connector?.Dispose();

            dataProvider = dataProviders.CreateDataProvider(clip);

            if (clip.DRMDatas != null)
            {
                foreach (var drm in clip.DRMDatas)
                    drmManager.UpdateDrmConfiguration(drm);
            }

            connector = new DataProviderConnector(playerController, dataProvider);

            dataProvider.Start();
        }

        public void Start()
        {
            Logger.Info(State.ToString());

            if (!dataProvider.IsDataAvailable())
            {
                // Live content
                RestartPlayerController();
            }

            playerController.OnPlay();
        }

        private void RestartPlayerController()
        {
            Logger.Info("Player controller restart");

            drmManager.ClearCache();
            dataProvider.OnStopped();
            _playerControllerConnections.Dispose();
            connector?.Dispose();
            playerController?.Dispose();

            CreatePlayerController();
            connector = new DataProviderConnector(playerController, dataProvider);
            ConnectPlayerControllerObservables();

            dataProvider.Start();
        }

        public void Stop()
        {
            dataProvider.OnStopped();
            playerController.OnStop();
            drmManager.ClearCache();
            connector.Dispose();
        }

        public void Suspend()
        {
            playerController.OnSuspend();
        }

        public Task Resume()
        {
            return playerController.OnResume();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _configurationSub?.Dispose();
                drmManager.ClearCache();
                _playerControllerConnections.Dispose();
                _playerControllerDisposables.Dispose();
                connector?.Dispose();
                playerController?.Dispose();
                playerController = null;
                dataProvider?.Dispose();
                dataProvider = null;
                GC.Collect();
            }
        }

        public IObservable<PlayerState> StateChanged()
        {
            return _playerStateSubject.AsObservable();
        }

        public IObservable<string> PlaybackError()
        {
            return _playerErrorSubject.AsObservable();
        }

        public IObservable<int> BufferingProgress()
        {
            return _playerBufferingSubject.AsObservable();
        }

        public IObservable<TimeSpan> PlayerClock()
        {
            return _playerClockSubject.AsObservable();
        }
    }
}
