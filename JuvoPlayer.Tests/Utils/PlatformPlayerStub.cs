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
using System.Diagnostics;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Players;

namespace JuvoPlayer.Tests.Utils
{
    public class PlatformPlayerStub : IPlatformPlayer
    {
        private readonly Stopwatch _positionStopwatch = new Stopwatch();
        private TimeSpan _start = TimeSpan.Zero;
        private PlayerState _state = PlayerState.None;

        public void Dispose()
        {
            Close();
        }

        public void Open(IWindow window, IEnumerable<StreamConfig> streamConfigs)
        {
            _state = PlayerState.Idle;
        }

        public void Close()
        {
            _state = PlayerState.None;
            _positionStopwatch.Restart();
        }

        public Task PrepareAsync(
            Action<ContentType> onReadyToPrepare,
            CancellationToken token)
        {
            if (_state != PlayerState.Idle)
                throw new InvalidOperationException();
            onReadyToPrepare.Invoke(ContentType.Audio);
            onReadyToPrepare.Invoke(ContentType.Video);
            _state = PlayerState.Ready;
            return Task.CompletedTask;
        }

        public async Task SeekAsync(
            TimeSpan targetTime,
            Action<ContentType> onReadyToSeek,
            CancellationToken token)
        {
            _positionStopwatch.Restart();
            await Task.Run(() =>
            {
                onReadyToSeek.Invoke(ContentType.Audio);
                onReadyToSeek.Invoke(ContentType.Video);
            }, token);

            _start = targetTime;
        }

        public void Start()
        {
            if (_state == PlayerState.Playing)
                return;
            if (_state != PlayerState.Ready)
                throw new InvalidOperationException();
            _state = PlayerState.Playing;
            _positionStopwatch.Start();
        }

        public void Pause()
        {
            if (_state == PlayerState.Paused)
                return;
            if (_state != PlayerState.Playing)
                throw new InvalidOperationException();
            _state = PlayerState.Paused;
            _positionStopwatch.Stop();
        }

        public void Resume()
        {
            if (_state == PlayerState.Playing)
                return;
            if (_state != PlayerState.Paused)
                throw new InvalidOperationException();
            _state = PlayerState.Playing;
            _positionStopwatch.Start();
        }

        public SubmitResult SubmitPacket(Packet packet)
        {
            return SubmitResult.Success;
        }

        public PlayerState GetState()
        {
            return _state;
        }

        public TimeSpan GetPosition()
        {
            return _start + _positionStopwatch.Elapsed;
        }

        public IObservable<Unit> OnEos()
        {
            throw new NotImplementedException();
        }

        public void Open(IWindow window, VideoStreamConfig videoConfig, AudioStreamConfig audioConfig)
        {
            _state = PlayerState.Idle;
        }
    }
}
