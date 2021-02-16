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
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using JuvoLogger;
using Configuration;

namespace JuvoPlayer.Player.EsPlayer
{
    internal delegate TimeSpan PlayerClockFn();

    internal class PlayerClockProvider : IDisposable
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private PlayerClockFn _playerClock = InvalidClockFn;
        private TimeSpan _currentClock = TimeSpan.Zero;
        private readonly IScheduler _scheduler;
        private volatile IDisposable _intervalConnection;
        private readonly IObservable<TimeSpan> _playerClockSource;
        private readonly Subject<TimeSpan> _playerClockSubject = new Subject<TimeSpan>();
        private readonly IConnectableObservable<long> _intervalSource;
        private bool _isDisposed;
        public TimeSpan Clock { get => _currentClock; }

        public bool IsRunning
        {
            get { return _intervalConnection != null; }
        }

        public PlayerClockProvider(IScheduler scheduler)
        {
            _scheduler = scheduler;

            // Use tick source disconnection over GetClock() substitution.
            // Circumstantial evidence: 
            // GetPlayingTime() while SeekAsync() may cause tz_video_appsrc
            // core dumps. It is possible substitution will not take place before calling SeekAsync.
            _intervalSource = Observable.Interval(PlayerClockProviderConfig.ClockInterval, _scheduler)
                .Publish();

            _playerClockSource = _intervalSource
                    .Select(_ => _playerClock())
                    .Where(nextClock => nextClock > _currentClock)
                    .Do(SetClock)
                    .Multicast(_playerClockSubject)
                    .RefCount();
        }

        private void SetClock(TimeSpan clock) => _currentClock = clock;

        public IObservable<TimeSpan> PlayerClockObservable()
        {
            return _playerClockSource;
        }

        public void SetPlayerClockSource(PlayerClockFn clockFn)
        {
            if (clockFn == null)
                clockFn = InvalidClockFn;

            _scheduler.Schedule(clockFn, SetClockFunction);
        }

        private IDisposable SetClockFunction(IScheduler scheduler, PlayerClockFn clockFn)
        {
            _playerClock = clockFn;
            Logger.Info($"Clock Set: {nameof(clockFn)}");
            return Disposable.Empty;
        }

        private static TimeSpan InvalidClockFn() =>
            PlayerClockProviderConfig.InvalidClock;

        public void Start()
        {
            if (_intervalConnection != null)
                return;

            var playerClock = _playerClock?.Invoke() ?? PlayerClockProviderConfig.InvalidClock;
            if (playerClock > _currentClock)
            {
                _currentClock = playerClock;
                _playerClockSubject.OnNext(_currentClock);
            }

            Logger.Info($"Player Clock: {Clock}");
            _intervalConnection = _intervalSource.Connect();

            Logger.Info("End");
        }

        public void Stop()
        {
            Logger.Info($"Running {_intervalConnection != null}");
            _intervalConnection?.Dispose();
            _intervalConnection = null;
            _currentClock = TimeSpan.Zero;
            Logger.Info("End");
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Stop();
            _playerClockSubject.Dispose();
            _isDisposed = true;

            Logger.Info("End");
        }
    }
}
