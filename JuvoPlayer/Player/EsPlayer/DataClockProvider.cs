/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2019, Samsung Electronics Co., Ltd
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
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Configuration;
using JuvoLogger;

namespace JuvoPlayer.Player.EsPlayer
{
    internal class DataClockProvider : IDisposable
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private TimeSpan _bufferLimit = DataClockProviderConfig.TimeBufferDepthDefault;
        private TimeSpan _clock;
        private TimeSpan _synchronizerClock;

        // Start / Stop may be called from multiple threads.
        private volatile IDisposable _intervalConnection;

        private readonly IObservable<TimeSpan> _dataClockSource;
        private readonly IConnectableObservable<long> _intervalSource;
        private readonly Subject<TimeSpan> _dataClockSubject = new Subject<TimeSpan>();
        private readonly PlayerClockProvider _playerClock;

        private IObservable<TimeSpan> _synchronizerClockSource;
        private IDisposable _synchronizerSubscription;

        private bool _isDisposed;
        private readonly IScheduler _scheduler;

        public TimeSpan BufferLimit { set => _bufferLimit = value; }
        public TimeSpan Clock { set => _clock = value; }
        public IObservable<TimeSpan> SynchronizerClock { set => _synchronizerClockSource = value; }

        public DataClockProvider(IScheduler scheduler, PlayerClockProvider playerClock)
        {
            _playerClock = playerClock;
            _scheduler = scheduler;

            _intervalSource = Observable.Interval(DataClockProviderConfig.ClockInterval, _scheduler)
                .Publish();

            _dataClockSource = _intervalSource
                .Select(GetDataClock)
                .Multicast(_dataClockSubject)
                .RefCount();
        }

        public IObservable<TimeSpan> DataClock()
        {
            return _dataClockSource;
        }

        private TimeSpan GetDataClock(long _)
        {
            var nextClock = _playerClock.Clock;
            if (nextClock == TimeSpan.Zero)
                nextClock = _synchronizerClock;

            if (nextClock > _clock)
                _clock = nextClock;

            return _clock + _bufferLimit;
        }

        private void SetSynchronizerClock(TimeSpan clock) =>
            _synchronizerClock = clock;

        public void Stop()
        {
            _dataClockSubject.OnNext(TimeSpan.Zero);

            _synchronizerSubscription?.Dispose();
            _synchronizerSubscription = null;
            _intervalConnection?.Dispose();
            _intervalConnection = null;

            _clock = TimeSpan.Zero;
            _synchronizerClock = TimeSpan.Zero;

            Logger.Info("End");
        }

        public void Start()
        {
            if (_intervalConnection != null) return;

            Logger.Info($"Clock {_clock} + Limit {_bufferLimit} = {_clock + _bufferLimit}");

            _intervalConnection = _intervalSource.Connect();
            _synchronizerSubscription = _synchronizerClockSource.ObserveOn(_scheduler).Subscribe(SetSynchronizerClock);

            Logger.Info("End");
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Stop();
            _dataClockSubject.Dispose();
            _isDisposed = true;

            Logger.Info("End");
        }
    }
}
