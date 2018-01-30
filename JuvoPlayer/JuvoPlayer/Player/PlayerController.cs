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

using JuvoPlayer.Common;
using JuvoPlayer.Common.Delegates;
using JuvoPlayer.DRM;
using System;
using System.Collections.Generic;
using Tizen;

namespace JuvoPlayer.Player
{
    public class PlayerController : IPlayerController
    {
        private enum PlayerState
        {
            Unitialized,
            Ready,
            Paused,
            Playing,
            Finished,
            Error = -1
        };

        private PlayerState state = PlayerState.Unitialized;
        private TimeSpan currentTime;
        private TimeSpan duration;

        private IDRMManager drmManager;
        private IPlayerAdapter playerAdapter;
        private Dictionary<StreamType, IPacketStream> Streams = new Dictionary<StreamType, IPacketStream>();

        public event Pause Paused;
        public event Play Played;
        public event Seek Seek;
        public event Stop Stopped;

        public event PlaybackCompleted PlaybackCompleted;
        public event PlaybackError PlaybackError;
        public event PlayerInitialized PlayerInitialized;
        public event ShowSubtitile ShowSubtitle;
        public event TimeUpdated TimeUpdated;

        public PlayerController(IPlayerAdapter player, IDRMManager drmManager)
        {
            this.drmManager = drmManager ?? throw new ArgumentNullException("drmManager cannot be null");
            this.playerAdapter = player ?? throw new ArgumentNullException("player cannot be null");

            playerAdapter.PlaybackCompleted += OnPlaybackCompleted;
            playerAdapter.PlaybackError += OnPlaybackError;
            playerAdapter.PlayerInitialized += OnPlayerInitialized;
            playerAdapter.ShowSubtitle += OnShowSubtitle;
            playerAdapter.TimeUpdated += OnTimeUpdated;

            Streams[StreamType.Audio] = new AudioPacketStream(playerAdapter, drmManager);
            Streams[StreamType.Video] = new VideoPacketStream(playerAdapter, drmManager);
        }

        private void OnPlaybackCompleted()
        {
            state = PlayerState.Finished;

            PlaybackCompleted?.Invoke();
        }

        private void OnPlaybackError(string error)
        {
            state = PlayerState.Finished;

            PlaybackError?.Invoke(error);
        }

        private void OnPlayerInitialized()
        {
            state = PlayerState.Ready;

            PlayerInitialized?.Invoke();
        }

        private void OnShowSubtitle(Subtitle subtitle)
        {
            ShowSubtitle?.Invoke(subtitle);
        }

        private void OnTimeUpdated(TimeSpan time)
        {
            currentTime = time;

            TimeUpdated?.Invoke(time);
        }

        public void ChangeRepresentation(int pid)
        {
        }
        public void OnClipDurationChanged(TimeSpan duration)
        {
            this.duration = duration;
        }

        public void OnDRMInitDataFound(DRMInitData data)
        {
            if (!Streams.ContainsKey(data.StreamType))
                return;

            Streams[data.StreamType].OnDRMFound(data);
        }

        public void OnSetDrmConfiguration(DRMDescription description)
        {
            drmManager?.UpdateDrmConfiguration(description);
        }

        public void OnPause()
        {
            if (state != PlayerState.Playing)
                return;

            playerAdapter.Pause();

            state = PlayerState.Paused;

            Paused?.Invoke();
        }

        public void OnPlay()
        {
            if (state < PlayerState.Ready)
                return;

            playerAdapter.Play();

            state = PlayerState.Playing;

            Played?.Invoke();
        }

        public void OnSeek(TimeSpan time)
        {
            playerAdapter.Seek(time);

            Seek?.Invoke(time);
        }

        public void OnStop()
        {
            playerAdapter.Stop();

            state = PlayerState.Finished;

            Stopped?.Invoke();
        }

        public void OnStreamConfigReady(StreamConfig config)
        {
            if (!Streams.ContainsKey(config.StreamType()))
                return;

            Streams[config.StreamType()].OnStreamConfigChanged(config);
        }

        public void OnStreamPacketReady(StreamPacket packet)
        {
            if (!Streams.ContainsKey(packet.StreamType))
                return;

            while (((packet.Pts / 1000000) - currentTime.TotalMilliseconds) > 3000) { }

            Streams[packet.StreamType].OnAppendPacket(packet);
        }

        public void OnStreamsFound(List<StreamDefinition> streams)
        {

        }

        public void OnSetExternalSubtitles(string path)
        {
            playerAdapter.SetExternalSubtitles(path);
        }

        public void OnSetPlaybackRate(float rate)
        {
            playerAdapter.SetPlaybackRate(rate);
        }

        public void OnSetSubtitleDelay(int offset)
        {
            playerAdapter.SetSubtitleDelay(offset);
        }

        #region getters
        TimeSpan IPlayerController.CurrentTime => currentTime;

        TimeSpan IPlayerController.ClipDuration => duration;
        #endregion

        public void Dispose()
        {
        }
    }
}