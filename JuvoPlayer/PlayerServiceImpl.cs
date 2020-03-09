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
        private readonly ReplaySubject<PlayerState> _playerStateSubject = new ReplaySubject<PlayerState>(1);
        private readonly Subject<string> _playerErrorSubject = new Subject<string>();
        private readonly ReplaySubject<int> _playerBufferingSubject = new ReplaySubject<int>(1);
        private readonly Subject<TimeSpan> _playerClockSubject = new Subject<TimeSpan>();
        private readonly SynchronizationContext _syncCtx;

        private readonly TaskCompletionSource<object>[] _streamConfigurationSetTcs;
        private TimeSpan? _pendingSeekClock;

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

            _streamConfigurationSetTcs = new TaskCompletionSource<object>[(int)StreamType.Count];
            var completedTcs = new TaskCompletionSource<object>();
            completedTcs.SetResult(null);
            for (int i = 0; i < (int)StreamType.Count; i++)
            {
                _streamConfigurationSetTcs[i] = completedTcs;
            }
        }

        public void SetWindow(Window window)
        {
            playerWindow = window;

            CreatePlayerController();
            ConnectPlayerControllerObservables();
        }

        private void CreatePlayerController()
        {
            if (playerWindow == null)
                playerWindow = WindowUtils.CreateElmSharpWindow();
            _player = new EsPlayer(playerWindow);

            playerController = new PlayerController(_player, drmManager);
        }

        private void ConnectPlayerControllerObservables()
        {
            _playerControllerConnections = new CompositeDisposable
            {
                playerController.StateChanged().Subscribe(SetState,_playerStateSubject.OnCompleted,_syncCtx),
                playerController.PlaybackError().Subscribe( _playerErrorSubject),
                playerController.BufferingProgress().Subscribe(_playerBufferingSubject),
                playerController.PlayerClock().Subscribe(_playerClockSubject),
                playerController.TimeUpdated().Subscribe(SetClock,_syncCtx),
                playerController.ConfigurationChanged()
                    .Subscribe(async sr=> await OnNewConfiguration(sr), _syncCtx)
            };
        }

        private async Task OnNewConfiguration((StreamType stream, bool reconfigurationRequired) args)
        {
            var (stream, reconfigurationRequired) = args;

            Logger.Info($"{stream} Reconfigure {reconfigurationRequired}");

            if (!reconfigurationRequired)
            {
                _streamConfigurationSetTcs[(int)stream].TrySetResult(null);
                return;
            }

            // Destructive configuration change. Need to complete.
            dataProvider.Pause();

            // Player & PlayerSevice snapshot
            var playerStateSnapshot = _player.GetStateSnapshot();
            var clipDuration = Duration;

            // Destroy player controller & its connections
            playerController.OnStop();
            connector?.Dispose();
            _playerControllerConnections.Dispose();
            playerController?.Dispose();

            // Create player controller & its connections
            CreatePlayerController();
            ConnectPlayerControllerObservables();
            connector = new DataProviderConnector(playerController, dataProvider);

            // Restore clip duration & player state
            playerController.OnClipDurationChanged(clipDuration);
            var restoreTask = _player.RestoreStateSnapshot(playerStateSnapshot);

            // Reposition data provider to compensate for packet loss during tear down
            // Seek starts data provider.
            var dataProviderPosition = _pendingSeekClock ?? CurrentPosition;
            var repositionTask = dataProvider.Seek(dataProviderPosition, CancellationToken.None);

            Logger.Info($"{stream} Reconfigure {reconfigurationRequired} Waiting player restore");

            await repositionTask;
            _streamConfigurationSetTcs[(int)stream].TrySetResult(null);

            Logger.Info($"{stream} Reconfigure {reconfigurationRequired} Player restore");
        }

        private void SetClock(TimeSpan clock) =>
            CurrentPosition = clock;

        private void SetState(PlayerState state)
        {
            State = state;
            _playerStateSubject.OnNext(State);
            Logger.Info($"State Set: {State}");
        }

        public void Pause()
        {
            playerController.OnPause();
        }


        public async Task SeekTo(TimeSpan to)
        {
            _pendingSeekClock = to;
            var seekClock = await playerController.OnSeek(to);

            // clear pending seek position if completed seek
            if (_pendingSeekClock == seekClock)
                _pendingSeekClock = null;
        }

        public async Task ChangeActiveStream(StreamDescription streamDescription)
        {
            Logger.Info($"ChangeActiveStream {streamDescription.StreamType} {streamDescription.Id} {streamDescription.Description}");

            if (streamDescription.StreamType != StreamType.Audio &&
                streamDescription.StreamType != StreamType.Video)
            {
                // Non A/V stream change
                dataProvider.ChangeActiveStream(streamDescription);
                return;
            }

            dataProvider.Pause();
            if (!dataProvider.ChangeActiveStream(streamDescription))
            {
                dataProvider.Resume();
                return;
            }

            _streamConfigurationSetTcs[(int)streamDescription.StreamType] =
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            var targetClock = _pendingSeekClock ?? CurrentPosition;
            var repositionTask = dataProvider.Seek(targetClock, CancellationToken.None);

            await SeekTo(targetClock);

            // Await stream changed confirmation to prevent shooting requests at player while
            // it is down on all fours as this operation may result in player tear down.
            Logger.Info($"ChangeActiveStream {streamDescription.StreamType} {streamDescription.Id} {streamDescription.Description} Waiting for confirmation");
            await _streamConfigurationSetTcs[(int)streamDescription.StreamType].Task;
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
