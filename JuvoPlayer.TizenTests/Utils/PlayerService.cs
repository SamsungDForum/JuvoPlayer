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

﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using ElmSharp;
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
using JuvoPlayer.Utils;

namespace JuvoPlayer.TizenTests.Utils
{
    public class PlayerService : IDisposable
    {
        public enum PlayerState
        {
            Error = -1,
            Idle,
            Prepared,
            Stopped,
            Playing,
            Buffering,
            Paused,
            Completed
        }

        private static Window window;

        public static void SetWindow(Window w)
        {
            window = w;
        }

        public List<ClipDefinition> ReadClips()
        {
            var applicationPath = Paths.ApplicationPath;
            var clipsPath = Path.Combine(applicationPath, "res", "videoclips.json");
            return JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(clipsPath).ToList();
        }

        private IDataProvider dataProvider;
        private IPlayerController playerController;
        private DataProviderConnector connector;
        private readonly DataProviderFactoryManager dataProviders;

        public TimeSpan Duration => playerController?.ClipDuration ?? TimeSpan.FromSeconds(0);

        public TimeSpan CurrentPosition =>
            dataProvider == null ? TimeSpan.FromSeconds(0) : playerController.CurrentTime;

        public bool IsSeekingSupported => dataProvider?.IsSeekingSupported() ?? false;

        public PlayerState State { get; private set; } = PlayerState.Idle;

        public string CurrentCueText => dataProvider?.CurrentCue?.Text;

        private readonly CompositeDisposable subscriptions;

        public PlayerService()
        {
            dataProviders = new DataProviderFactoryManager();
            dataProviders.RegisterDataProviderFactory(new DashDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new HLSDataProviderFactory());
            dataProviders.RegisterDataProviderFactory(new RTSPDataProviderFactory());

            var drmManager = new DrmManager();
            drmManager.RegisterDrmHandler(new CencHandler());
            drmManager.RegisterDrmHandler(new DummyDrmHandler());

            if (window == null)
                window = WindowUtils.CreateElmSharpWindow();
            var player = new EsPlayer(window);

            playerController = new PlayerController(player, drmManager);

            subscriptions = new CompositeDisposable
            {
                playerController.StateChanged()
                    .Subscribe(state => State = FromJuvoState(state), SynchronizationContext.Current),
                playerController.Initialized()
                    .Subscribe(unit => { State = PlayerState.Prepared; }, SynchronizationContext.Current),
                playerController.PlaybackCompleted()
                    .Subscribe(unit => State = PlayerState.Completed, SynchronizationContext.Current),
                playerController.PlaybackError()
                    .Subscribe(unit => State = PlayerState.Error, SynchronizationContext.Current)
            };
        }

        private PlayerState FromJuvoState(JuvoPlayer.Player.PlayerState juvoState)
        {
            switch (juvoState)
            {
                case Player.PlayerState.Uninitialized:
                    return PlayerState.Idle;
                case Player.PlayerState.Ready:
                    return PlayerState.Prepared;
                case Player.PlayerState.Buffering:
                    return PlayerState.Buffering;
                case Player.PlayerState.Paused:
                    return PlayerState.Paused;
                case Player.PlayerState.Playing:
                    return PlayerState.Playing;
                case Player.PlayerState.Finished:
                    return PlayerState.Completed;
                case Player.PlayerState.Error:
                    return PlayerState.Error;
                default:
                    throw new ArgumentOutOfRangeException(nameof(juvoState), juvoState, null);
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
            dataProvider.OnChangeActiveStream(stream);
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            return dataProvider?.GetStreamsDescription(streamType) ?? new List<StreamDescription>();
        }

        public void SetClipDefinition(ClipDefinition clip)
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
                connector?.Dispose();
                playerController?.Dispose();
                playerController = null;
                dataProvider?.Dispose();
                dataProvider = null;

                subscriptions.Dispose();

                GC.Collect();
            }
        }
    }
}