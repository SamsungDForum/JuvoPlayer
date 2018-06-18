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
using System.Linq;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Subtitles;

namespace JuvoPlayer.DataProviders.HLS
{
    internal class HLSDataProvider : IDataProvider
    {
        private static readonly TimeSpan MaxBufferHealth = TimeSpan.FromSeconds(10);

        private readonly IDemuxer demuxer;
        private readonly ClipDefinition currentClip;

        private TimeSpan lastReceivedPts;
        private TimeSpan currentTime;

        private CuesMap cuesMap;

        public HLSDataProvider(IDemuxer demuxer, ClipDefinition currentClip)
        {
            this.demuxer = demuxer ?? throw new ArgumentNullException(nameof(demuxer), "demuxer cannot be null");
            this.currentClip = currentClip ?? throw new ArgumentNullException(nameof(currentClip), "clip cannot be null");

            this.demuxer.ClipDuration += OnClipDurationChanged;
            this.demuxer.StreamConfigReady += OnStreamConfigReady;
            this.demuxer.PacketReady += OnPacketReady;
        }

        public event ClipDurationChanged ClipDurationChanged;
        public event DRMInitDataFound DRMInitDataFound;
        public event SetDrmConfiguration SetDrmConfiguration;
        public event StreamConfigReady StreamConfigReady;
        public event PacketReady PacketReady;
        public event StreamError StreamError;

        private void OnClipDurationChanged(TimeSpan clipDuration)
        {
            ClipDurationChanged?.Invoke(clipDuration);
        }

        private void OnStreamConfigReady(StreamConfig config)
        {
            StreamConfigReady?.Invoke(config);
        }

        private void OnPacketReady(Packet packet)
        {
            if (packet != null)
            {
                lastReceivedPts = packet.Pts;

                if (ShouldPauseDemuxer())
                    demuxer.Pause();

                PacketReady?.Invoke(packet);
                return;
            }

            // found empty packet which means EOS. We need to send two fake 
            // eos packets, one for audio and one for video
            PacketReady?.Invoke(Packet.CreateEOS(StreamType.Audio));
            PacketReady?.Invoke(Packet.CreateEOS(StreamType.Video));
        }

        private bool ShouldPauseDemuxer()
        {
            return lastReceivedPts - currentTime > MaxBufferHealth;
        }

        public void OnChangeActiveStream(StreamDescription stream)
        {
            if (stream.StreamType == StreamType.Subtitle)
            {
                OnChangeActiveSubtitleStream(stream);
                return;
            }
            throw new NotImplementedException();
        }

        public void OnDeactivateStream(StreamType streamType)
        {
            if (streamType == StreamType.Subtitle)
            {
                OnDeactivateSubtitleStream();
                return;
            }

            throw new NotImplementedException();
        }

        private void OnDeactivateSubtitleStream()
        {
            cuesMap = null;
        }

        public void OnPaused()
        {
            demuxer.Pause();
        }

        public void OnPlayed()
        {
            demuxer.Resume();
        }

        public void OnSeek(TimeSpan time)
        {
        }

        public void OnStopped()
        {
        }

        public bool IsSeekingSupported()
        {
            return false;
        }

        public void Start()
        {
            demuxer.StartForUrl(currentClip.Url);
        }

        public Cue CurrentCue => cuesMap?.Get(currentTime);

        public void OnTimeUpdated(TimeSpan time)
        {
            currentTime = time;
            ResumeDemuxerIfNecessary();
        }

        private void ResumeDemuxerIfNecessary()
        {
            if (demuxer.IsPaused && !ShouldPauseDemuxer())
            {
                demuxer.Resume();
            }
        }

        public void Dispose()
        {
            demuxer.Dispose();
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            if (streamType == StreamType.Subtitle)
                return GetSubtitleStreamsDescription();
            return new List<StreamDescription>();
        }

        private List<StreamDescription> GetSubtitleStreamsDescription()
        {
            var subtitles = currentClip.Subtitles ?? new List<SubtitleInfo>();
            return subtitles.Select(info => info.ToStreamDescription()).ToList();
        }

        private void OnChangeActiveSubtitleStream(StreamDescription description)
        {
            var found = currentClip.Subtitles?.First(info => info.Id == description.Id);
            if (found == null)
                throw new ArgumentException();
            cuesMap = new SubtitleFacade().LoadSubtitles(found);
        }
    }
}
