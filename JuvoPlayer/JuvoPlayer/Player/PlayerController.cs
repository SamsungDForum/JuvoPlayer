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

        private IPlayerAdapter playerAdapter;
        private IDataProvider dataProvider;
        private Dictionary<StreamType, IPacketStream> Streams = new Dictionary<StreamType, IPacketStream>();

        public event Pause Pause;
        public event Play Play;
        public event Seek Seek;
        public event Stop Stop;

        public event PlaybackCompleted PlaybackCompleted;
        public event ShowSubtitile ShowSubtitle;
        public event TimeUpdated TimeUpdated;

        public PlayerController(IPlayerAdapter player)
        {
            playerAdapter = player ?? throw new ArgumentNullException("player cannot be null");

            playerAdapter.PlaybackCompleted += OnPlaybackCompleted;
            playerAdapter.ShowSubtitle += OnShowSubtitle;
            playerAdapter.TimeUpdated += OnTimeUpdated;
        }

        private void OnPlaybackCompleted()
        {
            state = PlayerState.Finished;

            PlaybackCompleted?.Invoke();
        }

        private void OnShowSubtitle(Subtitle subtitle)
        {
            ShowSubtitle?.Invoke(subtitle);
        }

        private void OnTimeUpdated(double time)
        {
            TimeUpdated?.Invoke(time);
        }

        public void ChangeRepresentation(int pid)
        {
        }

        public void OnDrmDataFound(DRMData data)
        {
        }

        public void OnPause()
        {
            if (state != PlayerState.Playing)
                return;

            playerAdapter.Pause();

            state = PlayerState.Paused;

            Pause?.Invoke();
        }

        public void OnPlay()
        {
            if (state < PlayerState.Ready)
                return;

            playerAdapter.Play();

            state = PlayerState.Playing;

            Play?.Invoke();
        }

        public void OnSeek(double time)
        {
            playerAdapter.Seek(time);
            Seek?.Invoke(time);
        }

        public void OnStop()
        {
            playerAdapter.Stop();

            state = PlayerState.Finished;

            Stop?.Invoke();
        }

        public void OnStreamConfigReady(StreamConfig config)
        {
            Log.Info("JuvoPlayer", "OnStreamConfigReady");
            Streams[config.StreamType()] = CreatePacketStream(config);
        }

        public void OnStreamPacketReady(StreamPacket packet)
        {
//            Log.Info("JuvoPlayer", "OnStreamPacketReady");

            if (!Streams.ContainsKey(packet.StreamType))
                throw new Exception("Received packet for not configured stream");

            Streams[packet.StreamType].OnAppendPacket(packet);
        }

        public void OnStreamsFound(List<StreamDefinition> streams)
        {

        }

        public void OnSetExternalSubtitles(string path)
        {
            playerAdapter.SetExternalSubtitles(path);
        }

        private IPacketStream CreatePacketStream(StreamConfig config)
        {
            switch (config.StreamType())
            {
                case StreamType.Audio:
                    return new AudioPacketStream(playerAdapter, config);
                case StreamType.Video:
                    return new VideoPacketStream(playerAdapter, config);
                default:
                    {
                        Log.Info("JuvoPlayer", "unknown config type");

                        throw new Exception("unknown config type");
                    }
            }
        }

        public void OnSetPlaybackRate(float rate)
        {
            playerAdapter.SetPlaybackRate(rate);
        }

        public void OnSetSubtitleDelay(int offset)
        {
            playerAdapter.SetSubtitleDelay(offset);
        }

        public void SetDataProvider(IDataProvider dataProvider)
        {
            Stop?.Invoke();

            if (this.dataProvider != null)
            {
                this.dataProvider.DRMDataFound -= OnDrmDataFound;
                this.dataProvider.StreamConfigReady -= OnStreamConfigReady;
                this.dataProvider.StreamPacketReady -= OnStreamPacketReady;
                this.dataProvider.StreamsFound -= OnStreamsFound;
            }

            this.dataProvider = dataProvider;

            if (this.dataProvider != null)
            {
                this.dataProvider.DRMDataFound += OnDrmDataFound;
                this.dataProvider.StreamConfigReady += OnStreamConfigReady;
                this.dataProvider.StreamPacketReady += OnStreamPacketReady;
                this.dataProvider.StreamsFound += OnStreamsFound;
            }
        }

        public void Dispose()
        {
        }
    }
}