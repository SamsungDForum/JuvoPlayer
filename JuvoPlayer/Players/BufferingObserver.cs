/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.Players
{
    public class BufferingObserver : IDisposable
    {
        public bool IsObserving { get; private set; }
        public bool IsStarving { get; private set; }
        private TimeSpan? _lastSeenPacketTime;
        private bool _eosReceived;
        private readonly Subject<bool> _bufferingSubject;
        private readonly Clock _clock;
        private readonly TimeSpan _starvingThreshold;
        private readonly TimeSpan _filledThreshold;
        private Segment _segment;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayer;

        public BufferingObserver(
            Clock clock,
            TimeSpan starvingThreshold,
            TimeSpan filledThreshold,
            Func<TimeSpan, CancellationToken, Task> delayer = default)
        {
            _bufferingSubject = new Subject<bool>();
            _clock = clock;
            _starvingThreshold = starvingThreshold;
            _filledThreshold = filledThreshold;
            _delayer = delayer ?? Task.Delay;
        }

        public void Reset(Segment segment)
        {
            Stop();
            _segment = segment;
            IsStarving = false;
            _eosReceived = false;
            _lastSeenPacketTime = null;
        }

        public void Start()
        {
            if (IsObserving)
                return;
            IsObserving = true;
            Update(_lastSeenPacketTime);
        }

        public void Stop()
        {
            if (!IsObserving)
                return;
            IsObserving = false;
            _cancellationTokenSource?.Cancel();
        }

        public void Update(Packet packet)
        {
            _eosReceived = packet is EosPacket;
            _lastSeenPacketTime = packet.Pts;
            if (IsObserving)
                Update(_lastSeenPacketTime);
        }

        private async void Update(TimeSpan? packetTime)
        {
            _cancellationTokenSource?.Cancel();
            var clockTime = _clock.Elapsed;
            var playbackTime =
                _segment.ToPlaybackTime(clockTime);
            var timeDiff = packetTime - playbackTime;
            if (timeDiff >= _filledThreshold || _eosReceived)
            {
                if (IsStarving)
                {
                    IsStarving = false;
                    _bufferingSubject.OnNext(false);
                }
            }

            if (_eosReceived)
                return;

            var timeToPublishBufferingEvent =
                timeDiff - _starvingThreshold;
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;
            try
            {
                if (timeToPublishBufferingEvent > TimeSpan.Zero)
                {
                    await _delayer.Invoke(
                        timeToPublishBufferingEvent.Value,
                        cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!IsStarving)
                {
                    IsStarving = true;
                    _bufferingSubject.OnNext(true);
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
        }

        public IObservable<bool> OnBuffering()
        {
            return _bufferingSubject.AsObservable();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _bufferingSubject?.Dispose();
        }
    }
}