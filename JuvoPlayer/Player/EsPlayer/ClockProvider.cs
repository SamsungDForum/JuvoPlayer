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

    internal class ClockProvider : IDisposable
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private static readonly PlayerClockFn InvalidPlayerClock = InvalidClockFn;
        private PlayerClockFn playerClock = InvalidClockFn;
        public static TimeSpan LastClock { get; private set; } = InvalidClock;
        private static ClockProvider clockProviderInstance;
        private static readonly object InstanceLock = new object();

        private readonly IScheduler scheduler = new EventLoopScheduler();
        private IDisposable playerClockSourceConnection;
        private readonly IConnectableObservable<TimeSpan> playerClockConnectable;

        public IObservable<TimeSpan> PlayerClockObservable() => playerClockConnectable
            .AsObservable();

        public ClockProvider()
        {
            playerClockConnectable = Observable.Interval(ClockInterval, scheduler)
                .Select(_ => playerClock())
                .Where(clkValue =>
                {
                    if (clkValue < LastClock)
                        return false;

                    LastClock = clkValue;
                    return true;
                })
                .Publish();
        }

        public static ClockProvider GetClockProvider()
        {
            lock (InstanceLock)
            {
                if (clockProviderInstance == null)
                    clockProviderInstance = new ClockProvider();
            }

            return clockProviderInstance;
        }

        public void SetPlayerClockSource(PlayerClockFn clockFn)
        {
            Logger.Info("");
            if (clockFn == null)
                clockFn = InvalidPlayerClock;

            scheduler.Schedule(clockFn,
                (args, _) => playerClock = args);
        }

        private static TimeSpan InvalidClockFn()
        {
            Logger.Info("");
            return NoClockReturnValue;
        }

        public void EnableClock()
        {
            if (playerClockSourceConnection != null)
                return;

            LastClock = playerClock();
            playerClockSourceConnection = playerClockConnectable.Connect();
        }

        public void DisableClock()
        {
            playerClockSourceConnection?.Dispose();
            playerClockSourceConnection = null;
            LastClock = InvalidClock;
        }

        public void Dispose()
        {
            lock (InstanceLock)
            {
                Logger.Info("");

                playerClockSourceConnection?.Dispose();
                playerClockSourceConnection = null;
                clockProviderInstance = null;
            }
        }
    }
}
