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
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common.Utils.IReferenceCountable;
using JuvoPlayer.Common.Utils.IReferenceCountableExtensions;
using static Configuration.ClockProviderConfig;

namespace JuvoPlayer.Player.EsPlayer
{
    internal interface IClockSource
    {
        void GetClock(out TimeSpan clock);
    }

    internal class ClockProvider : IDisposable, IReferenceCountable
    {
        private readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        public static readonly TimeSpan InvalidClock = TimeSpan.MaxValue;

        private IClockSource clockProvider;

        private readonly IConnectableObservable<long> tickSource;
        private readonly IConnectableObservable<TimeSpan> clockSource;
        private Subject<long> tickPipe = new Subject<long>();
        private BehaviorSubject<TimeSpan> clockPipe = new BehaviorSubject<TimeSpan>(InvalidClock);

        public IConnectableObservable<long> TickSource() =>
            tickSource;

        public IConnectableObservable<TimeSpan> ClockSource() =>
            clockSource;

        private IDisposable clockSourceConnection;
        private IDisposable tickSourceConnection;

        private static volatile ClockProvider clockProviderInstance;
        private static volatile object instanceLock = new object();

        private int counter;
        public ref int Count => ref counter;

        public static ClockProvider GetClockProvider()
        {
            lock (instanceLock)
            {
                if (clockProviderInstance != null)
                {
                    clockProviderInstance.Share();
                    return clockProviderInstance;
                }

                var clockProvider = new ClockProvider();
                clockProvider.InitializeReferenceCounting();
                clockProviderInstance = clockProvider;

                return clockProviderInstance;
            }
        }

        public ClockProvider()
        {
            tickSource = Observable.Interval(ClockInterval)
                .Multicast(tickPipe);

            clockSource = Observable.Interval(ClockInterval)
                .Select(_ => GetClockValue())
                .Multicast(clockPipe);

            Volatile.Write(ref tickSourceConnection, tickSource.Connect());
        }

        public void SetSourceClock(IClockSource clock) =>
            Interlocked.Exchange(ref clockProvider, clock);

        public void ConnectClockSource()
        {
            Logger.Info("");

            var newConnection = clockSource.Connect();

            var result = Interlocked.CompareExchange(ref clockSourceConnection, newConnection, null);
            if (result?.Equals(newConnection) == false)
                newConnection.Dispose();
        }

        public void DisconnectClockSource()
        {
            Logger.Info("");
            Volatile.Read(ref clockSourceConnection)?.Dispose();
            Interlocked.Exchange(ref clockSourceConnection, null);
        }

        private TimeSpan GetClockValue()
        {
            var currentClock = InvalidClock;

            try
            {
                Volatile.Read(ref clockProvider)?.GetClock(out currentClock);
            }
            catch (Exception e) when (e is ObjectDisposedException || e is InvalidOperationException)
            {
                // Ignore exception when clock is accessed when unavailable
            }

            return currentClock;
        }

        public void Dispose()
        {
            Logger.Info("");
            lock (instanceLock)
            {
                Volatile.Read(ref clockSourceConnection)?.Dispose();
                Interlocked.Exchange(ref clockProvider, null);

                Volatile.Read(ref tickSourceConnection)?.Dispose();
                Interlocked.Exchange(ref tickSourceConnection, null);

                Volatile.Read(ref tickPipe)?.Dispose();
                Volatile.Read(ref clockPipe)?.Dispose();

                Interlocked.Exchange(ref clockProviderInstance, null);
            }
        }
    }

    internal class PlayerClock : IDisposable
    {
        private static readonly long DefaultEmit = default(DateTimeOffset).Ticks;

        private long lastEmit = DefaultEmit;
        private long initialClockTicks = ClockProvider.InvalidClock.Ticks;
        private volatile bool enabled;
        private readonly ClockProvider clockProvider;

        public IConnectableObservable<TimeSpan> ClockSource() =>
            clockProvider
                .ClockSource();

        public IConnectableObservable<long> TickSource() =>
            clockProvider
                .TickSource();

        public void SetClockSource(IClockSource clockSource) =>
            clockProvider.SetSourceClock(clockSource);

        public void ConnectClockSource() =>
            clockProvider.ConnectClockSource();

        public void DisconnectClockSource() =>
            clockProvider.DisconnectClockSource();

        public PlayerClock()
        {
            clockProvider = ClockProvider.GetClockProvider();
            Reset();
        }

        public void Reset()
        {
            enabled = false;
            Interlocked.Exchange(ref initialClockTicks, ClockProvider.InvalidClock.Ticks);
            Interlocked.Exchange(ref lastEmit, DefaultEmit);
        }

        public void SetInitialClock(TimeSpan initClock)
        {
            Interlocked.Exchange(ref initialClockTicks, initClock.Ticks);
        }

        public static bool IsClockValid(TimeSpan clock) =>
            clock != ClockProvider.InvalidClock;

        public TimeSpan GetClockOrInitial(TimeSpan clock) =>
            clock.Ticks >= Volatile.Read(ref initialClockTicks) ? clock : TimeSpan.FromTicks(initialClockTicks);

        public bool ClockWhenInitialReached(TimeSpan clock) =>
            clock.Ticks >= Volatile.Read(ref initialClockTicks);

        public bool IsSamplePeriodExpired(TimeSpan samplePeriod)
        {
            var now = DateTimeOffset.Now.Ticks;
            if (now - Volatile.Read(ref lastEmit) < samplePeriod.Ticks)
                return false;

            Interlocked.Exchange(ref lastEmit, now);
            return true;
        }

        public bool IsEnabled() => enabled;
        public bool Enable() => enabled = true;
        public bool Disable() => enabled = false;

        public void Dispose()
        {
            clockProvider.Release();
        }
    }

    internal static class ClockProviderEx
    {
        public static IObservable<TimeSpan> EnabledClock(this IObservable<TimeSpan> source, PlayerClock cf) =>
            source
                .Where(_ => cf.IsEnabled());

        public static IObservable<long> EnabledClock(this IObservable<long> source, PlayerClock cf) =>
            source
                .Where(_ => cf.IsEnabled());

        public static IObservable<TimeSpan> ValidClock(this IObservable<TimeSpan> source) =>
            source
                .Where(PlayerClock.IsClockValid);

        public static IObservable<TimeSpan> ClockOrInitial(this IObservable<TimeSpan> source, PlayerClock cf) =>
            source
                .Select(cf.GetClockOrInitial);

        public static IObservable<TimeSpan> ClockWhenInitialReached(this IObservable<TimeSpan> source, PlayerClock cf) =>
            source
                .Where(cf.ClockWhenInitialReached);

        public static IObservable<TimeSpan> EmitThenSample(this IObservable<TimeSpan> source, PlayerClock cf, TimeSpan samplePeriod) =>
            source
                .Where(_ => cf.IsSamplePeriodExpired(samplePeriod));
    }
}
