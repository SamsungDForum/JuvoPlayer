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
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Subtitles;
using static Configuration.RTSPDataProvider;

namespace JuvoPlayer.DataProviders.RTSP
{
    internal class RTSPDataProvider : IDataProvider
    {
        private readonly IDemuxerController demuxerController;
        private readonly IRTSPClient rtspClient;
        private readonly ClipDefinition currentClip;
        private CancellationTokenSource _startCancellationTokenSource;

        public Cue CurrentCue { get; }

        public RTSPDataProvider(IDemuxerController demuxerController, IRTSPClient rtpClient, ClipDefinition currentClip)
        {
            this.demuxerController = demuxerController ??
                                     throw new ArgumentNullException(nameof(demuxerController),
                                         "demuxerController cannot be null");
            this.rtspClient =
                rtpClient ?? throw new ArgumentNullException(nameof(rtpClient), "rtpClient cannot be null");
            this.currentClip =
                currentClip ?? throw new ArgumentNullException(nameof(currentClip), "clip cannot be null");
        }

        public void Resume()
        {
            throw new NotImplementedException();
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
            return demuxerController.PacketReady()
                .SelectMany(packet =>
                {
                    if (packet != null)
                        return Observable.Return(packet);
                    // found empty packet which means EOS. We need to send two fake
                    // eos packets, one for audio and one for video
                    return Observable.Return(EOSPacket.Create(StreamType.Audio))
                        .Merge(Observable.Return(EOSPacket.Create(StreamType.Video)));
                });
        }

        public IObservable<string> StreamError()
        {
            return demuxerController.DemuxerError()
                .Merge(rtspClient.RTSPError());
        }

        public void OnDataClock(TimeSpan dataClock)
        {
            // RTSP Data Provider does not support data dataClock events
            // Input handler empty
        }

        public bool ChangeActiveStream(StreamDescription stream)
        {
            throw new NotImplementedException();
        }

        public void OnDeactivateStream(StreamType streamType)
        {
            throw new NotImplementedException();
        }

        public void OnStateChanged(PlayerState state)
        {
            if (rtspClient == null || rtspClient.IsStarted == false)
                return;

            switch (state)
            {
                case PlayerState.Paused:
                    rtspClient.Pause();
                    break;
                case PlayerState.Playing:
                    rtspClient.Play();
                    break;
            }
        }

        public Task<TimeSpan> Seek(TimeSpan time, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public bool IsDataAvailable()
        {
            return true;
        }

        public bool IsSeekingSupported()
        {
            return false;
        }

        public void Stop()
        {
            _startCancellationTokenSource?.Cancel();
        }

        public void Start()
        {
            if (rtspClient == null)
                return;

            // Start demuxer before client. Demuxer start clears
            // underlying buffer. We do not want that to happen after client
            // puts something in there.
            demuxerController.StartForEs();

            // start RTSP client
            _startCancellationTokenSource = new CancellationTokenSource();
            _startCancellationTokenSource.CancelAfter(ConnectionTimeout);
            rtspClient.Start(currentClip, _startCancellationTokenSource.Token);
        }

        public void OnStopped()
        {
        }

        public void OnTimeUpdated(TimeSpan time)
        {
        }

        public void Dispose()
        {
            rtspClient?.Stop();
            demuxerController.Dispose();
        }

        public List<StreamDescription> GetStreamsDescription(StreamType streamType)
        {
            return new List<StreamDescription>();
        }

        public void Pause()
        {
            throw new NotImplementedException();
        }
    }
}
