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
using System.Reactive;
using System.Threading;
using static Configuration.ClockProviderConfig;

namespace JuvoPlayer.Player.EsPlayer
{
    public delegate void ClockDelegate(out TimeSpan clock);

    internal static class ClockProvider
    {
        // Clock source observable & player clock retriever are
        // shared among ALL instances of clock filter

        public static readonly TimeSpan InvalidClock = TimeSpan.MinValue;

        private static ClockDelegate sourceClock;
        private static readonly Lazy<IObservable<long>> ClockGenerator;
        public static IObservable<long> ClockTick() => ClockGenerator.Value;

        public static IObservable<TimeSpan> ClockValue() =>
            ClockTick()
                .Where(_ => sourceClock != null)
                .Select(_ => GetClockValue());

        static ClockProvider()
        {
            ClockGenerator = new Lazy<IObservable<long>>(() =>
                Observable.Interval(ClockInterval)
                    .Publish()
                    .RefCount());
        }

        public static void SetSourceClock(ClockDelegate clockFunc) =>
            sourceClock = clockFunc;

        private static TimeSpan GetClockValue()
        {
            var currentClock = InvalidClock;

            try
            {
                sourceClock?.Invoke(out currentClock);
            }
            catch (Exception)
            {
                // Ignore errors.
            }

            return currentClock;
        }
    }

    internal class ClockFilter
    {
        private static readonly long DefaultEmit = default(DateTimeOffset).Ticks;
        private static readonly long InvalidClock = ClockProvider.InvalidClock.Ticks;

        private long lastEmit = DefaultEmit;
        private long initialClock = InvalidClock;
        private volatile bool enabled;

        public IObservable<TimeSpan> ClockValue() => ClockProvider.ClockValue();
        public IObservable<long> ClockTick() => ClockProvider.ClockTick();
        public void SetClockSource(ClockDelegate clockFunc) =>
            ClockProvider.SetSourceClock(clockFunc);

        public ClockFilter()
        {
            Reset();
        }

        public void Reset()
        {
            Volatile.Write(ref initialClock, InvalidClock);
            Volatile.Write(ref lastEmit, DefaultEmit);
        }

        public void SetInitialClock(TimeSpan initClock)
        {
            Volatile.Write(ref initialClock, initClock.Ticks);
        }

        public static bool IsClockValid(TimeSpan clock) =>
            clock != ClockProvider.InvalidClock;

        public TimeSpan GetClockMarkEmission(Timestamped<TimeSpan> ts)
        {
            Volatile.Write(ref lastEmit, ts.Timestamp.Ticks);
            return ts.Value;
        }

        public TimeSpan GetClockOrInitial(TimeSpan clock) =>
            clock.Ticks >= Volatile.Read(ref initialClock) ? clock : TimeSpan.FromTicks(initialClock);

        public bool IsSamplePeriodExpired(Timestamped<TimeSpan> ts, TimeSpan samplePeriod) =>
            ts.Timestamp.Ticks - lastEmit >= samplePeriod.Ticks;

        public bool IsEnabled() => enabled;
        public bool Enable() => enabled = true;
        public bool Disable() => enabled = false;
    }

    internal static class ClockProviderEx
    {
        public static IObservable<TimeSpan> EnabledClock(this IObservable<TimeSpan> source, ClockFilter cf) =>
            source
                .Where(_ => cf.IsEnabled());

        public static IObservable<TimeSpan> ValidClock(this IObservable<TimeSpan> source) =>
            source
                .Where(ClockFilter.IsClockValid);

        public static IObservable<TimeSpan> ClockOrInitial(this IObservable<TimeSpan> source, ClockFilter cf) =>
            source
                .Select(cf.GetClockOrInitial);

        public static IObservable<TimeSpan> EmitThenSample(this IObservable<TimeSpan> source, ClockFilter cf, TimeSpan samplePeriod) =>
            source
                .Timestamp()
                .Where(ts => cf.IsSamplePeriodExpired(ts, samplePeriod))
                .Select(cf.GetClockMarkEmission);
    }
}
