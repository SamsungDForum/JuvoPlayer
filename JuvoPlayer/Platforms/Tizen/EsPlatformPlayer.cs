/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2020, Samsung Electronics Co., Ltd
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
 *
 */

using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Players;
using Nito.AsyncEx;
using Tizen.TV.Multimedia;

namespace JuvoPlayer.Platforms.Tizen
{
    public class EsPlatformPlayer : IPlatformPlayer
    {
        private readonly ESPlayer _esPlayer;

        public EsPlatformPlayer()
        {
            Log.Debug();
            _esPlayer = new ESPlayer();
        }

        public void Open(IWindow window, IEnumerable<StreamConfig> streamConfigs)
        {
            Log.Debug();
            _esPlayer.Open();
            EsPlayerExtensions.SetDisplay(window, _esPlayer);
            if (SupportsDrms())
                _esPlayer.SetTrustZoneUse(true);
            foreach (var streamConfig in streamConfigs)
            {
                switch (streamConfig)
                {
                    case AudioStreamConfig audioStreamConfig:
                        _esPlayer.SetStream(audioStreamConfig.EsAudioStreamInfo());
                        break;
                    case VideoStreamConfig videoStreamConfig:
                        _esPlayer.SetStream(videoStreamConfig.EsVideoStreamInfo());
                        break;
                }
            }
        }

        public void Close()
        {
            Log.Debug();
            _esPlayer.Close();
        }

        public Task PrepareAsync(
            Action<ContentType> onReadyToPrepare,
            CancellationToken token)
        {
            Log.Debug();
            return _esPlayer.PrepareAsync(type =>
                {
                    if (!token.IsCancellationRequested)
                        onReadyToPrepare.Invoke(type.ToContentType());
                })
                .WaitAsync(token);
        }

        public Task SeekAsync(
            TimeSpan targetTime,
            Action<ContentType> onReadyToSeek,
            CancellationToken token)
        {
            Log.Debug();
            return _esPlayer.SeekAsync(targetTime,
                    (type, span) =>
                    {
                        if (!token.IsCancellationRequested)
                            onReadyToSeek.Invoke(type.ToContentType());
                    })
                .WaitAsync(token);
        }

        public void Start()
        {
            Log.Debug();
            _esPlayer.Start();
        }

        public void Pause()
        {
            Log.Debug();
            _esPlayer.Pause();
        }

        public void Resume()
        {
            Log.Debug();
            _esPlayer.Resume();
        }

        public SubmitResult SubmitPacket(Packet packet)
        {
            Log.Debug();
            SubmitStatus esStatus;
            switch (packet)
            {
                case DecryptedPacket decryptedPacket:
                    var esHandlePacket = decryptedPacket.ToEsHandlePacket();
                    esStatus = _esPlayer.SubmitPacket(esHandlePacket);
                    if (esStatus == SubmitStatus.Success)
                        decryptedPacket.ResetHandle();
                    break;
                case EosPacket eosPacket:
                    var esStreamType = eosPacket
                        .StreamType
                        .EsStreamType();
                    esStatus = _esPlayer.SubmitEosPacket(esStreamType);
                    break;
                default:
                    var esPacket = packet.ToEsPacket();
                    esStatus = _esPlayer.SubmitPacket(esPacket);
                    break;
            }

            return esStatus.ToSubmitResult();
        }

        public PlayerState GetState()
        {
            var esState = _esPlayer.GetState();
            return esState.ToPlayerState();
        }

        public TimeSpan GetPosition()
        {
            _esPlayer.GetPlayingTime(out var position);
            return position;
        }

        public IObservable<Unit> OnEos()
        {
            return Observable.FromEventPattern<EOSEventArgs>(
                    x => _esPlayer.EOSEmitted += x,
                    x => _esPlayer.EOSEmitted -= x)
                .Select(args => Unit.Default);
        }

        public void Dispose()
        {
            Log.Debug();
            _esPlayer.Dispose();
        }

        private static bool SupportsDrms()
        {
            var platform = Platform.Current;
            var capabilities = platform.Capabilities;
            return capabilities.SupportsDrms;
        }
    }
}