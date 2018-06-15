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
using JuvoPlayer.Common;
using JuvoPlayer.Drms;

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

        private bool seeking;
        private PlayerState state = PlayerState.Unitialized;
        private TimeSpan currentTime;
        private TimeSpan duration;

        private readonly IDrmManager drmManager;
        private readonly IPlayer player;
        private readonly Dictionary<StreamType, IPacketStream> streams = new Dictionary<StreamType, IPacketStream>();

        public event Pause Paused;
        public event Play Played;
        public event Seek Seek;
        public event Stop Stopped;

        public event PlaybackCompleted PlaybackCompleted;
        public event PlaybackError PlaybackError;
        public event PlayerInitialized PlayerInitialized;
        public event TimeUpdated TimeUpdated;
        public event SeekCompleted SeekCompleted;

        public PlayerController(IPlayer player, IDrmManager drmManager)
        {
            this.drmManager = drmManager ?? throw new ArgumentNullException(nameof(drmManager), "drmManager cannot be null");
            this.player = player ?? throw new ArgumentNullException(nameof(player), "player cannot be null");

            this.player.PlaybackCompleted += OnPlaybackCompleted;
            this.player.PlaybackError += OnPlaybackError;
            this.player.PlayerInitialized += OnPlayerInitialized;
            this.player.SeekCompleted += OnSeekCompleted;
            this.player.TimeUpdated += OnTimeUpdated;

            var audioCodecExtraDataHandler = new AudioCodecExtraDataHandler(player);
            var vidoeCodecExtraDataHandler = new VideoCodecExtraDataHandler(player);

            streams[StreamType.Audio] = new PacketStream(StreamType.Audio, this.player, drmManager, audioCodecExtraDataHandler);
            streams[StreamType.Video] = new PacketStream(StreamType.Video, this.player, drmManager, vidoeCodecExtraDataHandler);
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

        private void OnTimeUpdated(TimeSpan time)
        {
            currentTime = time;

            TimeUpdated?.Invoke(time);
        }

        public void OnClipDurationChanged(TimeSpan duration)
        {
            player.SetDuration(duration);

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
            if (state != PlayerState.Playing)
                return;

            player.Pause();

            state = PlayerState.Paused;

            Paused?.Invoke();
        }

        public void OnPlay()
        {
            if (state < PlayerState.Ready)
                return;

            player.Play();

            state = PlayerState.Playing;

            Played?.Invoke();
        }

        public void OnSeek(TimeSpan time)
        {
            if (seeking)
                return;

            player.Seek(time);

            // prevent simultaneously seeks
            seeking = true;

            Seek?.Invoke(time);
        }

        public void OnSeekCompleted()
        {
            seeking = false;

            SeekCompleted?.Invoke();
        }

        public void OnStop()
        {
            foreach (var stream in streams.Values)
                stream.OnClearStream();

            player.Stop();

            state = PlayerState.Finished;

            Stopped?.Invoke();
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

        public void OnSetPlaybackRate(float rate)
        {
            player.SetPlaybackRate(rate);
        }

        #region getters
        TimeSpan IPlayerController.CurrentTime => currentTime;

        TimeSpan IPlayerController.ClipDuration => duration;
        #endregion

        public void Dispose()
        {
            // It is possible that streams waits for some events to complete
            // eg. drm initialization, and after unblock they will call disposed
            // player.
            // Remember to firstly dispose streams and later player
            foreach (var stream in streams.Values)
                stream.Dispose();

            player?.Dispose();
        }
    }
}
