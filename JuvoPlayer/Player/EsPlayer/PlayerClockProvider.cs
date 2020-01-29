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
using static Configuration.PlayerClockProviderConfig;

namespace JuvoPlayer.Player.EsPlayer
{
    internal delegate TimeSpan PlayerClockFn();

    internal class PlayerClockProvider : IDisposable
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private PlayerClockFn _playerClock = InvalidClockFn;
        public TimeSpan LastClock { get; private set; } = InvalidClock;

        private readonly IScheduler _scheduler;
        private IDisposable _playerClockSourceConnection;
        private readonly IConnectableObservable<TimeSpan> _playerClockConnectable;
        private bool _isDisposed;

        public PlayerClockProvider(IScheduler scheduler)
        {
            _scheduler = scheduler;

            _playerClockConnectable = Observable.Interval(ClockInterval, _scheduler)
                    .TakeWhile(_ => !_isDisposed)
                    .Select(_ => _playerClock())
                    .Where(clkValue => clkValue >= LastClock)
                    .Do(clkValue => LastClock = clkValue)
                    .Publish();
        }

        public IObservable<TimeSpan> PlayerClockObservable()
        {
            return _playerClockConnectable.AsObservable();
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

        private static TimeSpan InvalidClockFn()
        {
            Logger.Info("");
            return NoClockReturnValue;
        }

        public void Start()
        {
            if (_playerClockSourceConnection != null)
                return;

            var currentClock = _playerClock();
            if (currentClock > LastClock)
                LastClock = currentClock;

            _playerClockSourceConnection = _playerClockConnectable.Connect();
            Logger.Info($"{currentClock}");
        }

        public void Stop()
        {
            _playerClockSourceConnection?.Dispose();
            _playerClockSourceConnection = null;
            LastClock = InvalidClock;
            Logger.Info("");
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            SetPlayerClockSource(null);
            Stop();
        }
    }
}
