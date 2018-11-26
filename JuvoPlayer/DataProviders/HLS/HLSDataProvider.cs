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
using System.Reactive;
using System.Reactive.Linq;
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
            this.currentClip =
                currentClip ?? throw new ArgumentNullException(nameof(currentClip), "clip cannot be null");
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

        public void OnSeekStarted(TimeSpan time, uint seekId)
        {
            throw new NotImplementedException();
        }

        public void OnSeekCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnStopped()
        {
        }

        public bool IsSeekingSupported()
        {
            return false;
        }

        public void Stop()
        {
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

        public IObservable<TimeSpan> ClipDurationChanged()
        {
            return demuxer.ClipDurationChanged();
        }

        public IObservable<DRMInitData> DRMInitDataFound()
        {
            return demuxer.DRMInitDataFound();
        }

        public IObservable<DRMDescription> SetDrmConfiguration()
        {
            return Observable.Empty<DRMDescription>();
        }

        public IObservable<StreamConfig> StreamConfigReady()
        {
            return demuxer.StreamConfigReady();
        }

        public IObservable<Packet> PacketReady()
        {
            return demuxer.PacketReady()
                .Do(packet =>
                {
                    if (packet == null) return;
                    lastReceivedPts = packet.Pts;
                    if (ShouldPauseDemuxer())
                        demuxer.Pause();
                }).SelectMany(packet =>
                {
                    if (packet != null)
                        return Observable.Return(packet);
                    // found empty packet which means EOS. We need to send two fake
                    // eos packets, one for audio and one for video
                    return Observable.Return(Packet.CreateEOS(StreamType.Audio))
                        .Merge(Observable.Return(Packet.CreateEOS(StreamType.Video)));
                });
        }

        public IObservable<string> StreamError()
        {
            return Observable.Empty<string>();
        }

        public IObservable<Unit> BufferingStarted()
        {
            return Observable.Empty<Unit>();
        }

        public IObservable<Unit> BufferingCompleted()
        {
            return Observable.Empty<Unit>();
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