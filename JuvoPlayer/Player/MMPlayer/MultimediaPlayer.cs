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
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoLogger;
using Tizen.Multimedia;

namespace JuvoPlayer.Player.MMPlayer
{
    public class MultimediaPlayer : IPlayer
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private ElmSharp.Window playerContainer;

        private readonly Tizen.Multimedia.Player player;
        private MediaStreamSource source = null;
        private AudioMediaFormat audioFormat = null;
        private VideoMediaFormat videoFormat = null;

        public event PlaybackCompleted PlaybackCompleted;
        public event PlaybackError PlaybackError;
        public event PlayerInitialized PlayerInitialized;
        public event ShowSubtitile ShowSubtitle;
        public event TimeUpdated TimeUpdated;

        public MultimediaPlayer()
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
            Logger.Info("OnSubtitleUpdated");
            Subtitle subtitle = new Subtitle
            {
                Duration = e.Duration,
                Text = e.Text
            };

            ShowSubtitle?.Invoke(subtitle);
        }

        private void OnBufferingProgressChanged(object sender, BufferingProgressChangedEventArgs e)
        {
            Logger.Info("OnBufferingProgressChanged: " + e.Percent);
        }

        private void OnPlaybackInterrupted(object sender, PlaybackInterruptedEventArgs e)
        {
            Logger.Info("OnPlaybackInterrupted: " + e.Reason);
        }

        private void OnPlaybackCompleted(object sender, EventArgs e)
        {
            Logger.Info("OnPlaybackCompleted");

            PlaybackCompleted?.Invoke();
        }

        private void OnErrorOccured(object sender, PlayerErrorOccurredEventArgs e)
        {
            Logger.Info("OnErrorOccured: " + e.Error.ToString());
        }

        public void AppendPacket(Packet packet)
        {
            if (packet.StreamType == StreamType.Audio)
                AppendPacket(audioFormat, packet);
            else if (packet.StreamType == StreamType.Video)
                AppendPacket(videoFormat, packet);
        }

        private void AppendPacket(MediaFormat format, Packet packet)
        {
            if (source == null)
            {
                Logger.Info("stream has not been properly configured");
                return;
            }

            try
            {
                Logger.Info("Append packet " + packet.StreamType.ToString());

                var mediaPacket = MediaPacket.Create(format);
                mediaPacket.Dts = (ulong)(packet.Dts.TotalMilliseconds * 1000);
                mediaPacket.Pts = (ulong)(packet.Pts.TotalMilliseconds * 1000);
                if (packet.IsKeyFrame)
                    mediaPacket.BufferFlags = MediaPacketBufferFlags.SyncFrame;

                var buffer = mediaPacket.Buffer;
                buffer.CopyFrom(packet.Data, 0, packet.Data.Length);

                source.Push(mediaPacket);
            }
            catch (Exception e)
            {
                Logger.Error("error on append packet: " + e.GetType().ToString() + " " + e.Message);
            }
        }

        public void Play()
        {
            if (source == null)
            {
                Logger.Info("stream has not been properly configured");
                return;
            }

            Logger.Info("Play");

            try
            {
                player.Start();
            }
            catch (Exception e)
            {
                Logger.Info("Play exception: " + e.Message);
            }

        }

        public void Seek(TimeSpan time)
        {
            if (source == null)
            {
                Logger.Info("stream has not been properly configured");
                return;
            }

            player.SetPlayPositionAsync((int)time.TotalMilliseconds, false);
        }

        private void SetAudioStreamConfig(AudioStreamConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "config cannot be null");

            audioFormat = new AudioMediaFormat(CastAudioCodedToAudioMimeType(config.Codec), config.ChannelLayout, config.SampleRate, config.BitsPerChannel, config.BitRate);

            StreamInitialized();
        }

        private MediaFormatAudioMimeType CastAudioCodedToAudioMimeType(AudioCodec audioCodec)
        {
            switch (audioCodec)
            {
                case AudioCodec.AAC:
                    return MediaFormatAudioMimeType.Aac;
                case AudioCodec.MP3:
                    return MediaFormatAudioMimeType.MP3;
                case AudioCodec.PCM:
                    return MediaFormatAudioMimeType.Pcm;
                case AudioCodec.VORBIS:
                    return MediaFormatAudioMimeType.Vorbis;
                case AudioCodec.FLAC:
                    return MediaFormatAudioMimeType.Flac;
                case AudioCodec.AMR_NB:
                    return MediaFormatAudioMimeType.AmrNB;
                case AudioCodec.AMR_WB:
                    return MediaFormatAudioMimeType.AmrWB;
                case AudioCodec.WMAV1:
                    return MediaFormatAudioMimeType.Wma1;
                case AudioCodec.WMAV2:
                    return MediaFormatAudioMimeType.Wma2;
                case AudioCodec.PCM_MULAW:
                case AudioCodec.GSM_MS:
                case AudioCodec.PCM_S16BE:
                case AudioCodec.PCM_S24BE:
                case AudioCodec.OPUS:
                case AudioCodec.AC3:
                case AudioCodec.EAC3:
                case AudioCodec.MP2:
                case AudioCodec.DTS:
                    throw new NotImplementedException();
            }
            return new MediaFormatAudioMimeType();
        }

        public void SetDuration(TimeSpan duration)
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

        private void SetVideoStreamConfig(VideoStreamConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "config cannot be null");

            videoFormat = new VideoMediaFormat(CastVideoCodedToAudioMimeType(config.Codec), config.Size, config.FrameRateNum/config.FrameRateDen, config.BitRate);

            StreamInitialized();
        }

        private MediaFormatVideoMimeType CastVideoCodedToAudioMimeType(VideoCodec videoCodec)
        {
            switch (videoCodec)
            {
                case VideoCodec.H263:
                    return MediaFormatVideoMimeType.H263P;
                case VideoCodec.H264:
                    return MediaFormatVideoMimeType.H264HP; // TODO(g.skowinski): H264HP? H264MP? H264SP?
                case VideoCodec.H265:
                    throw new NotImplementedException(); // TODO(g.skowinski): ???
                case VideoCodec.MPEG2:
                    return MediaFormatVideoMimeType.Mpeg2HP; // TODO(g.skowinski): HP? MP? SP?
                case VideoCodec.MPEG4:
                    return MediaFormatVideoMimeType.Mpeg4SP; // TODO(g.skowinski): Asp? SP?
                case VideoCodec.THEORA:
                case VideoCodec.VC1:
                case VideoCodec.VP8:
                case VideoCodec.VP9:
                case VideoCodec.WMV1:
                case VideoCodec.WMV2:
                case VideoCodec.WMV3:
                case VideoCodec.INDEO3:
                default:
                    throw new NotImplementedException();
            }
        }

        public void Stop()
        {
            player.Stop();
        }

        private void StreamInitialized()
        {
            Logger.Info("Stream initialized");

            if (audioFormat == null || videoFormat == null)
                return;

            source = new MediaStreamSource(audioFormat, videoFormat);

            player.SetSource(source);
            var task = Task.Run(async () => { await player.PrepareAsync(); });
            task.Wait();
            if (task.IsFaulted)
            {
                Logger.Info(task.Exception.Flatten().InnerException.Message);
            }
            Logger.Info("Stream initialized 222");
        }

        public void OnTimeUpdated(double time)
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

        public void Dispose()
        {
            player?.Dispose();
        }

        public void SetStreamConfig(StreamConfig config)
        {
            switch (config.StreamType())
            {
                case Common.StreamType.Audio:
                    SetAudioStreamConfig(config as AudioStreamConfig);
                    break;
                case Common.StreamType.Video:
                    SetVideoStreamConfig(config as VideoStreamConfig);
                    break;
                default:
                    break;
            }
        }
    }
}