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
using System.Threading;
using System.Threading.Tasks;
using Tizen;
using Tizen.Multimedia;

namespace JuvoPlayer.Player
{
    public class MultimediaPlayerAdapter : IPlayerAdapter
    {
        private ElmSharp.Window playerContainer;

        private Tizen.Multimedia.Player player;
        private MediaStreamSource source = null;
        private AudioMediaFormat audioFormat = null;
        private VideoMediaFormat videoFormat = null;

        public event ShowSubtitile ShowSubtitle;
        public event PlaybackCompleted PlaybackCompleted;

        public MultimediaPlayerAdapter()
        {
            player = new Tizen.Multimedia.Player();
            player.BufferingProgressChanged += OnBufferingProgressChanged;
            player.ErrorOccurred += OnErrorOccured;
            player.PlaybackCompleted += OnPlaybackCompleted;
            player.PlaybackInterrupted += OnPlaybackInterrupted;
            player.SubtitleUpdated += OnSubtitleUpdated;

            playerContainer = new ElmSharp.Window("player");
            player.Display = new Display(playerContainer);
            player.DisplaySettings.Mode = PlayerDisplayMode.FullScreen;
            playerContainer.Show();
            playerContainer.BringDown();
        }

        private void OnSubtitleUpdated(object sender, SubtitleUpdatedEventArgs e)
        {
            Log.Info("JuvoPlayer", "OnSubtitleUpdated");
            Subtitle subtitle = new Subtitle
            {
                Duration = e.Duration,
                Text = e.Text
            };

            ShowSubtitle(subtitle);
        }

        private void OnBufferingProgressChanged(object sender, BufferingProgressChangedEventArgs e)
        {
            Log.Info("JuvoPlayer", "OnBufferingProgressChanged: " + e.Percent);
        }

        private void OnPlaybackInterrupted(object sender, PlaybackInterruptedEventArgs e)
        {
            Log.Info("JuvoPlayer", "OnPlaybackInterrupted: " + e.Reason);
        }

        private void OnPlaybackCompleted(object sender, EventArgs e)
        {
            Log.Info("JuvoPlayer", "OnPlaybackCompleted");

            PlaybackCompleted();
        }

        private void OnErrorOccured(object sender, PlayerErrorOccurredEventArgs e)
        {
            Log.Info("JuvoPlayer", "OnErrorOccured: " + e.Error.ToString());
        }

        public void AppendPacket(StreamPacket packet)
        {
            if (packet.StreamType == StreamType.Audio)
                AppendPacket(audioFormat, packet);
            else if (packet.StreamType == StreamType.Video)
                AppendPacket(videoFormat, packet);
        }

        private void AppendPacket(MediaFormat format, StreamPacket packet)
        {
            if (source == null)
            {
                Log.Info("JuvoPlayer", "stream has not been properly configured");
                return;
            }

            try
            {
                Log.Info("JuvoPlayer", "Append packet " + packet.StreamType.ToString());

                var mediaPacket = MediaPacket.Create(format);
                mediaPacket.Dts = packet.Dts;
                mediaPacket.Pts = packet.Pts;
                if (packet.IsKeyFrame)
                    mediaPacket.BufferFlags = MediaPacketBufferFlags.SyncFrame;

                var buffer = mediaPacket.Buffer;
                buffer.CopyFrom(packet.Data, 0, packet.Data.Length);

                source.Push(mediaPacket);
            }
            catch (Exception e)
            {
                Log.Error("JuvoPlayer", "error on append packet: " + e.GetType().ToString() + " " + e.Message);
            }
        }

        public void Play()
        {
            if (source == null)
            {
                Log.Info("JuvoPlayer", "stream has not been properly configured");
                return;
            }

            Log.Info("JuvoPlayer", "Play");

            try
            { 
                player.Start();
            }
            catch (Exception e)
            {
                Log.Info("JuvoPlayer", "Play exception: " + e.Message);
            }

        }

        public void Seek(double time)
        {
            if (source == null)
            {
                Log.Info("JuvoPlayer", "stream has not been properly configured");
                return;
            }

            player.SetPlayPositionAsync((int)time, false);
        }

        public void SetAudioStreamConfig(AudioStreamConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config cannot be null");

            audioFormat = new AudioMediaFormat(config.Codec, config.ChannelLayout, config.SampleRate, config.BitsPerChannel, config.BitRate);

            StreamInitialized();
        }

        public void SetDuration(double duration)
        {
        }

        public void SetExternalSubtitles(string file)
        {
            player.SetSubtitle(file);
        }

        public void SetPlaybackRate(float rate)
        {
            player.SetPlaybackRate(rate);
        }

        public void SetVideoStreamConfig(VideoStreamConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config cannot be null");

            videoFormat = new VideoMediaFormat(config.Codec, config.Size, config.FrameRate, config.BitRate);

            StreamInitialized();
        }

        public void Stop()
        {
            player.Stop();
        }

        private void StreamInitialized()
        {
            Log.Info("JuvoPlayer", "Stream initialized");

            if (audioFormat == null || videoFormat == null)
                return;

            source = new MediaStreamSource(audioFormat, videoFormat);

            player.SetSource(source);
            var task = Task.Run(async () => { await player.PrepareAsync(); });
            task.Wait();
            if (task.IsFaulted)
            {
                Log.Info("JuvoPlayer", task.Exception.Flatten().InnerException.Message);
            }
            Log.Info("JuvoPlayer", "Stream initialized 222");
        }

        public void TimeUpdated(double time)
        {

        }

        public void SetSubtitleDelay(int offset)
        {
            player.SetSubtitleOffset(offset);
        }

        public void Pause()
        {
            player.Pause();
        }
    }

}