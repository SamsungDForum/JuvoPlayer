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
using JuvoLogger;
using static Configuration.ClockProviderConfig;

namespace JuvoPlayer.Player.EsPlayer
{
    internal delegate TimeSpan PlayerClockFn();

    internal class ClockProvider
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private static PlayerClockFn _playerClock = InvalidClockFn;
        public static TimeSpan LastClock { get; private set; } = InvalidClock;

        private static readonly IScheduler Scheduler = new EventLoopScheduler();
        private static IDisposable _playerClockSourceConnection;
        private static IConnectableObservable<TimeSpan> PlayerClockConnectable =>
            PlayerClockConnectableLazy.Value;

        private static readonly Lazy<IConnectableObservable<TimeSpan>> PlayerClockConnectableLazy =
            new Lazy<IConnectableObservable<TimeSpan>>(() =>
                Observable.Interval(ClockInterval, Scheduler)
                .Select(_ => _playerClock())
                .Where(clkValue => clkValue >= LastClock)
                .Do(clkValue => LastClock = clkValue)
                .Publish());

        public IObservable<TimeSpan> PlayerClockObservable() => PlayerClockConnectable
            .AsObservable();

        public void SetPlayerClockSource(PlayerClockFn clockFn)
        {
            Logger.Info("");
            if (clockFn == null)
                clockFn = InvalidClockFn;

            Scheduler.Schedule(clockFn,
                (args, _) => _playerClock = args);
        }

        private static TimeSpan InvalidClockFn()
        {
            Logger.Info("");
            return NoClockReturnValue;
        }

        public void EnableClock()
        {
            if (_playerClockSourceConnection != null)
                return;

            var currentClock = _playerClock();
            if (currentClock > LastClock)
                LastClock = currentClock;

            _playerClockSourceConnection = PlayerClockConnectable.Connect();
            Logger.Info($"{currentClock}");
        }

        public void DisableClock()
        {
            _playerClockSourceConnection?.Dispose();
            _playerClockSourceConnection = null;
            LastClock = InvalidClock;
        }
    }
}
