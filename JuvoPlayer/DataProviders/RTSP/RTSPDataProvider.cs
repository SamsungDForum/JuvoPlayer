/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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
        private readonly IDemuxerController demuxerController;
        private readonly IRTSPClient rtpClient;
        private readonly ClipDefinition currentClip;

        public RTSPDataProvider(IDemuxerController demuxerController, IRTSPClient rtpClient, ClipDefinition currentClip)
        {
            this.demuxerController = demuxerController ??
                                     throw new ArgumentNullException(nameof(demuxerController),
                                         "demuxerController cannot be null");
            this.rtpClient =
                rtpClient ?? throw new ArgumentNullException(nameof(rtpClient), "rtpClient cannot be null");
            this.currentClip =
                currentClip ?? throw new ArgumentNullException(nameof(currentClip), "clip cannot be null");
        }

        public IObservable<TimeSpan> ClipDurationChanged()
        {
            return demuxerController.ClipDurationFound();
        }

        public IObservable<DRMInitData> DRMInitDataFound()
        {
            return demuxerController.DrmInitDataFound();
        }

        public IObservable<DRMDescription> SetDrmConfiguration()
        {
            return Observable.Empty<DRMDescription>();
        }

        public IObservable<StreamConfig> StreamConfigReady()
        {
            return demuxerController.StreamConfigReady();
        }

        public IObservable<Packet> PacketReady()
        {
            return demuxerController.PacketReady();
        }

        public IObservable<string> StreamError()
        {
            return demuxerController.DemuxerError();
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

        public void OnStateChanged(PlayerState state)
        {
            switch (state)
            {
                case PlayerState.Paused:
                    rtpClient?.Pause();
                    break;
                case PlayerState.Playing:
                    rtpClient?.Play();
                    break;
            }
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
            demuxerController.StartForEs();
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
            demuxerController.Dispose();
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            return new List<StreamDescription>();
        }
    }
}