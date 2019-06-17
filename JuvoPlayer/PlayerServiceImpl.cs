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
        private CompositeDisposable subscriptions;

        public TimeSpan Duration => playerController?.ClipDuration ?? TimeSpan.FromSeconds(0);

        public TimeSpan CurrentPosition =>
            dataProvider == null ? TimeSpan.FromSeconds(0) : playerController.CurrentTime;

        public bool IsSeekingSupported => dataProvider?.IsSeekingSupported() ?? false;

        public PlayerState State { get; private set; } = PlayerState.Idle;

        public string CurrentCueText => dataProvider?.CurrentCue?.Text;

        private Window playerWindow;
        private readonly IDrmManager drmManager;
        private Subject<PlayerState> stateChangedSubject = new Subject<PlayerState>();
        private Subject<string> playbackErrorSubject = new Subject<string>();
        private Subject<int> bufferingProgressSubject = new Subject<int>();

        public PlayerServiceImpl(Window window)
        {
            dataProviders = new DataProviderFactoryManager();
            dataProviders.RegisterDataProviderFactory(new DashDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new HLSDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new RTSPDataProviderFactory());

            drmManager = new DrmManager();
            drmManager.RegisterDrmHandler(new CencHandler());

            playerWindow = window;

            CreatePlayerController();
        }

        private void CreatePlayerController()
        {
            if (playerWindow == null)
                playerWindow = WindowUtils.CreateElmSharpWindow();
            var player = new EsPlayer(playerWindow);

            playerController = new PlayerController(player, drmManager);

            subscriptions = new CompositeDisposable
            {
                playerController.StateChanged()
                    .Subscribe(state =>
                    {
                        State = state;
                        stateChangedSubject.OnNext(state);
                    }, () => stateChangedSubject.OnCompleted(), SynchronizationContext.Current),
                playerController.PlaybackError()
                    .Subscribe(playbackErrorSubject.OnNext,SynchronizationContext.Current),
                playerController.BufferingProgress()
                    .Subscribe(bufferingProgressSubject.OnNext,SynchronizationContext.Current)
            };
        }

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
            Logger.Info(State.ToString());

            if (!dataProvider.IsDataAvailable())
            {
                RestartPlayerController();
                return;
            }

            playerController.OnPlay();
        }

        private void RestartPlayerController()
        {
            Logger.Info("Player controller restart");

            dataProvider.OnStopped();
            subscriptions.Dispose();
            connector?.Dispose();
            playerController?.Dispose();

            CreatePlayerController();

            connector = new DataProviderConnector(playerController, dataProvider);

            dataProvider.Start();
            playerController.OnPlay();
        }

        public void Stop()
        {
            dataProvider.OnStopped();
            playerController.OnStop();
            connector.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                subscriptions.Dispose();
                connector?.Dispose();
                playerController?.Dispose();
                playerController = null;
                dataProvider?.Dispose();
                dataProvider = null;
                stateChangedSubject.Dispose();
                playbackErrorSubject.Dispose();
                bufferingProgressSubject.Dispose();
                GC.Collect();
            }
        }

        public IObservable<PlayerState> StateChanged()
        {
            return stateChangedSubject.AsObservable();
        }

        public IObservable<string> PlaybackError()
        {
            return playbackErrorSubject.AsObservable();
        }

        public IObservable<int> BufferingProgress()
        {
            return bufferingProgressSubject.AsObservable();
        }
    }
}
