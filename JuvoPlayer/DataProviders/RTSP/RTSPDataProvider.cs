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
using System.Reactive.Linq;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Subtitles;

namespace JuvoPlayer.DataProviders.RTSP
{
    internal class RTSPDataProvider : IDataProvider
    {
        private readonly IDemuxer demuxer;
        private readonly IRTSPClient rtpClient;
        private readonly ClipDefinition currentClip;
        public RTSPDataProvider(IDemuxer demuxer, IRTSPClient rtpClient, ClipDefinition currentClip)
        {
            this.demuxer = demuxer ?? throw new ArgumentNullException(nameof(demuxer), "demuxer cannot be null");
            this.rtpClient = rtpClient ?? throw new ArgumentNullException(nameof(rtpClient), "rtpClient cannot be null");
            this.currentClip = currentClip ?? throw new ArgumentNullException(nameof(currentClip), "clip cannot be null");
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
                .SelectMany(packet =>
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
            return demuxer.DemuxerError();
        }

        public IObservable<Unit> BufferingStarted()
        {
            return Observable.Empty<Unit>();
        }

        public IObservable<Unit> BufferingCompleted()
        {
            return Observable.Empty<Unit>();
        }

        public void OnChangeActiveStream(StreamDescription stream)
        {
            throw new NotImplementedException();
        }

        public void OnDeactivateStream(StreamType streamType)
        {
            throw new NotImplementedException();
        }

        public void OnPaused()
        {
            rtpClient?.Pause();
        }

        public void OnPlayed()
        {
            rtpClient?.Play();
        }

        public void OnSeekStarted(TimeSpan time, uint seekId)
        {
            throw new NotImplementedException();
        }

        public void OnSeekCompleted()
        {
            throw new NotImplementedException();
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
            if (rtpClient == null)
                return;

            // Start demuxer before client. Demuxer start clears
            // underlying buffer. We do not want that to happen after client
            // puts something in there.
            demuxer.StartForExternalSource(InitializationMode.Full);
            rtpClient.Start(currentClip);
        }

        public Cue CurrentCue { get; }

        public void OnStopped()
        {
        }

        public void OnTimeUpdated(TimeSpan time)
        {
        }

        public void Dispose()
        {
            rtpClient?.Stop();
            demuxer.Dispose();
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            return new List<StreamDescription>();
        }
    }
}
