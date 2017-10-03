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
using System;
using System.Collections.Generic;
using Tizen;

namespace JuvoPlayer.Player
{
    public class PlayerController : IPlayerController
    {
        private IPlayerAdapter playerAdapter;
        private Dictionary<StreamType, IPacketStream> Streams = new Dictionary<StreamType, IPacketStream>();

        public PlayerController(IPlayerAdapter player)
        {
            playerAdapter = player ?? throw new ArgumentNullException("player cannot be null");

            playerAdapter.PlaybackCompleted += OnPlaybackCompleted;
            playerAdapter.ShowSubtitle += OnShowSubtitle;
        }

        public event Pause Pause;
        public event Play Play;
        public event Seek Seek;
        public event Stop Stop;

        public event ShowSubtitile ShowSubtitle;
        public event PlaybackCompleted PlaybackCompleted;

        private void OnShowSubtitle(Subtitle subtitle)
        {
            ShowSubtitle?.Invoke(subtitle);
        }

        private void OnPlaybackCompleted()
        {
            PlaybackCompleted?.Invoke();
        }

        public void ChangeRepresentation(int pid)
        {
        }

        public void OnDrmDataFound(DRMData data)
        {
        }

        public void OnPause()
        {
            playerAdapter.Pause();
            Pause();
        }

        public void OnPlay()
        {
            playerAdapter.Play();
            Play();
        }

        public void OnSeek(double time)
        {
            playerAdapter.Seek(time);
            Seek(time);
        }

        public void OnStop()
        {
            playerAdapter.Stop();
            Stop();
        }

        public void OnStreamConfigReady(StreamConfig config)
        {
            Log.Info("JuvoPlayer", "OnStreamConfigReady");
            Streams[config.StreamType()] = CreatePacketStream(config);
        }

        public void OnStreamPacketReady(StreamPacket packet)
        {
            Log.Info("JuvoPlayer", "OnStreamPacketReady");

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
    }
}