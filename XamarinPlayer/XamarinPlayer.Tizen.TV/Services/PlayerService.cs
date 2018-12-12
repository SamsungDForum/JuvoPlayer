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
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly CompositeDisposable subscriptions;

        private DataProviderConnector connector;

        public TimeSpan Duration => playerController.ClipDuration;

        public TimeSpan CurrentPosition => playerController.CurrentTime;

        public bool IsSeekingSupported => dataProvider.IsSeekingSupported();
        public PlayerState State { get; private set; }

        public string CurrentCueText => dataProvider.CurrentCue?.Text;

        public PlayerService()
        {
            dataProviders.RegisterDataProviderFactory(new DashDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new HLSDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new RTSPDataProviderFactory());

            var drmManager = new DrmManager();
            drmManager.RegisterDrmHandler(new CencHandler());

            var player = new EsPlayer();

            playerController = new PlayerController(player, drmManager);
            subscriptions = new CompositeDisposable
            {
                playerController.StateChanged().Select(ToPlayerState)
                    .Subscribe(state => { State = state; }, SynchronizationContext.Current)
            };
        }

        public void Pause()
        {
            playerController.OnPause();
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
                new StreamDefinition
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

        private PlayerState ToPlayerState(JuvoPlayer.Common.PlayerState state)
        {
            switch (state)
            {
                case JuvoPlayer.Common.PlayerState.Idle:
                    return PlayerState.Idle;
                case JuvoPlayer.Common.PlayerState.Prepared:
                    return PlayerState.Prepared;
                case JuvoPlayer.Common.PlayerState.Paused:
                    return PlayerState.Paused;
                case JuvoPlayer.Common.PlayerState.Playing:
                    return PlayerState.Playing;
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

            connector?.Dispose();

            dataProvider?.Dispose();

            if (State != PlayerState.Idle)
                Stop();

            dataProvider = dataProviders.CreateDataProvider(clip);

            // TODO(p.galiszewsk) rethink!
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
            Logger.Info("");

            playerController.OnPlay();
        }

        public void Stop()
        {
            Logger.Info("");

            // Stop data provider first so no new data is fed to player while
            // it is being stopped.
            //
            dataProvider.OnStopped();

            playerController.OnStop();
        }

        public IObservable<PlayerState> StateChanged()
        {
            return playerController.StateChanged().Select(ToPlayerState);
        }

        public IObservable<string> PlaybackError()
        {
            return playerController.PlaybackError();
        }

        public IObservable<double> BufferingProgress()
        {
            return playerController.BufferingProgress();
        }

        public void Dispose()
        {
            Logger.Info("");

            connector?.Dispose();
            subscriptions.Dispose();

            // Stop Data provider during dispose AFTER disconnecting data provider and controller.
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