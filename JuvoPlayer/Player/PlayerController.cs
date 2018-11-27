// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Drms;

namespace JuvoPlayer.Player
{
    public class PlayerController : IPlayerController
    {
        private bool seeking;
        private PlayerState state = PlayerState.Uninitialized;
        private TimeSpan currentTime;
        private TimeSpan duration;

        private PlayerState State
        {
            get => state;
            set
            {
                state = value;
                stateChangedSubject.OnNext(value);
            }
        }

        private readonly IDrmManager drmManager;
        private readonly IPlayer player;
        private readonly Dictionary<StreamType, IPacketStream> streams = new Dictionary<StreamType, IPacketStream>();

        private readonly CompositeDisposable subscriptions;
        private readonly Subject<PlayerState> stateChangedSubject = new Subject<PlayerState>();

        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        public PlayerController(IPlayer player, IDrmManager drmManager)
        {
            this.drmManager = drmManager ??
                              throw new ArgumentNullException(nameof(drmManager), "drmManager cannot be null");
            this.player = player ?? throw new ArgumentNullException(nameof(player), "player cannot be null");

            subscriptions = new CompositeDisposable
            {
                TimeUpdated().Subscribe(time => currentTime = time, SynchronizationContext.Current),
                SeekCompleted().Subscribe(unit => seeking = false, SynchronizationContext.Current),
                Initialized()
                    .Subscribe(unit => state = PlayerState.Ready, SynchronizationContext.Current),
                PlaybackCompleted()
                    .Subscribe(unit => state = PlayerState.Finished, SynchronizationContext.Current)
            };

            var audioCodecExtraDataHandler = new AudioCodecExtraDataHandler(player);
            var videoCodecExtraDataHandler = new VideoCodecExtraDataHandler(player);

            streams[StreamType.Audio] =
                new PacketStream(StreamType.Audio, this.player, drmManager, audioCodecExtraDataHandler);
            streams[StreamType.Video] =
                new PacketStream(StreamType.Video, this.player, drmManager, videoCodecExtraDataHandler);
        }

        public IObservable<string> PlaybackError()
        {
            return player.PlaybackError();
        }

        public IObservable<Unit> Initialized()
        {
            return player.Initialized();
        }

        public IObservable<TimeSpan> TimeUpdated()
        {
            return player.TimeUpdated();
        }

        public IObservable<Unit> Paused()
        {
            return player.Paused();
        }

        public IObservable<Unit> Played()
        {
            return player.Played();
        }

        public IObservable<SeekArgs> SeekStarted()
        {
            return player.SeekStarted();
        }

        public IObservable<Unit> SeekCompleted()
        {
            return player.SeekCompleted();
        }

        public IObservable<Unit> Stopped()
        {
            return player.Stopped();
        }

        public IObservable<PlayerState> StateChanged()
        {
            return stateChangedSubject.AsObservable();
        }

        public IObservable<Unit> PlaybackCompleted()
        {
            return player.PlaybackCompleted();
        }

        public void OnClipDurationChanged(TimeSpan duration)
        {
            this.duration = duration;
        }

        public void OnDRMInitDataFound(DRMInitData data)
        {
            if (!streams.ContainsKey(data.StreamType))
                return;

            streams[data.StreamType].OnDRMFound(data);
        }

        public void OnSetDrmConfiguration(DRMDescription description)
        {
            drmManager?.UpdateDrmConfiguration(description);
        }

        public void OnPause()
        {
            if (State != PlayerState.Playing)
                return;

            player.Pause();

            State = PlayerState.Paused;
        }

        public void OnPlay()
        {
            if (State < PlayerState.Ready)
                return;

            player.Play();

            State = PlayerState.Playing;
        }

        public void OnSeek(TimeSpan time)
        {
            if (seeking)
                return;

            if (time > duration)
                time = duration;

            try
            {
                player.Seek(time);

                // prevent simultaneously seeks
                seeking = true;
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Operation Canceled");
            }
        }

        public void OnStop()
        {
            Logger.Info("");

            foreach (var stream in streams.Values)
                stream.OnClearStream();

            player.Stop();

            State = PlayerState.Finished;
        }

        public void OnStreamConfigReady(StreamConfig config)
        {
            if (!streams.ContainsKey(config.StreamType()))
                return;

            streams[config.StreamType()].OnStreamConfigChanged(config);
        }

        public void OnPacketReady(Packet packet)
        {
            if (!streams.ContainsKey(packet.StreamType))
                return;

            streams[packet.StreamType].OnAppendPacket(packet);
        }

        public void OnStreamError(string errorMessage)
        {
            // TODO: Implement or remove
        }

        public void OnSetPlaybackRate(float rate)
        {
            player.SetPlaybackRate(rate);
        }

        public void OnBufferingStarted()
        {
            if (State != PlayerState.Playing)
                return;

            player.Pause();
            State = PlayerState.Buffering;
        }

        public void OnBufferingCompleted()
        {
            if (State != PlayerState.Buffering)
                return;

            player.Play();
            State = PlayerState.Playing;
        }

        #region getters

        TimeSpan IPlayerController.CurrentTime => currentTime;

        TimeSpan IPlayerController.ClipDuration => duration;

        #endregion

        public void Dispose()
        {
            Logger.Info("");
            // It is possible that streams waits for some events to complete
            // eg. drm initialization, and after unblock they will call disposed
            // player.
            // Remember to firstly dispose streams and later player
            foreach (var stream in streams.Values)
                stream.Dispose();

            subscriptions.Dispose();

            player?.Dispose();
        }
    }
}