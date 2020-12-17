/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
                playerController.TimeUpdated().Subscribe(SetClock,_syncCtx),
            };
        }

        private void SetClock(TimeSpan clock)
        {
            CurrentPosition = clock;
            _playerClockSubject.OnNext(clock);
        }

        private void SetState(PlayerState state)
        {
            State = state;
            _playerStateSubject.OnNext(State);
        }

        public void Pause()
        {
            playerController.OnPause();
        }

        public Task SeekTo(TimeSpan to)
        {
            return playerController.OnSeek(to);
        }

        public Task ChangeActiveStream(StreamDescription streamDescription)
        {
            Logger.Info($"ChangeActiveStream {streamDescription.StreamType} {streamDescription.Id} {streamDescription.Description}");

            var isAV = streamDescription.StreamType == StreamType.Audio ||
                       streamDescription.StreamType == StreamType.Video;

            if (!isAV)
            {
                dataProvider.ChangeActiveStream(streamDescription);
                return Task.CompletedTask;
            }

            return playerController.OnRepresentationChanged(streamDescription);
        }

        public void DeactivateStream(StreamType streamType)
        {
            dataProvider.OnDeactivateStream(streamType);
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            return dataProvider.GetStreamsDescription(streamType);
        }

        public async Task SetSource(ClipDefinition clip)
        {
            drmManager.Clear();
            connector?.Dispose();

            dataProvider = dataProviders.CreateDataProvider(clip);

            if (clip.DRMDatas != null)
            {
                foreach (var drm in clip.DRMDatas)
                    await drmManager.UpdateDrmConfiguration(drm);
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

            dataProvider.OnStopped();
            _playerControllerConnections.Dispose();
            connector?.Dispose();
            playerController?.Dispose();
            drmManager?.Clear();

            CreatePlayerController();
            connector = new DataProviderConnector(playerController, dataProvider);
            ConnectPlayerControllerObservables();

            dataProvider.Start();
        }

        public void Stop()
        {
            dataProvider.OnStopped();
            playerController.OnStop();
            drmManager.Clear();
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
                _playerControllerConnections.Dispose();
                _playerControllerDisposables.Dispose();
                connector?.Dispose();
                playerController?.Dispose();
                playerController = null;
                dataProvider?.Dispose();
                dataProvider = null;
                drmManager?.Clear();
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
