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
using System.Threading;
using JuvoPlayer.Common;

namespace JuvoPlayer.Player.EsPlayer
{
    internal class ClockSynchronizer : IDisposable
    {
        private static readonly TimeSpan InitialClock = TimeSpan.Zero;

        private long _clockTicks;
        private long _referenceClockTicks;

        private long GetClockTicks() =>
            Volatile.Read(ref _clockTicks);

        private long GetReferenceClockTicks() =>
            Volatile.Read(ref _referenceClockTicks);

        public bool ClockSeen() =>
            GetClockTicks() != InitialClock.Ticks;

        public TimeSpan Clock
        {
            get => TimeSpan.FromTicks(GetClockTicks());
            private set => Interlocked.Exchange(ref _clockTicks, value.Ticks);
        }

        public TimeSpan ReferenceClock
        {
            get => TimeSpan.FromTicks(GetReferenceClockTicks());
            private set => Interlocked.Exchange(ref _referenceClockTicks, value.Ticks);
        }

        private long haltOnDifference;
        private long haltOffDifference;

        private readonly ManualResetEventSlim syncWait = new ManualResetEventSlim(false);

        public StreamType StreamType { get; }
        public bool IsSynchronized => syncWait.IsSet;

        public ClockSynchronizer(StreamType stream)
        {
            StreamType = stream;
            Initialize();
        }

        public void Set() =>
            syncWait.Set();

        public void Reset() =>
            syncWait.Reset();

        public void Wait(CancellationToken token) =>
            syncWait.Wait(token);

        public void SetThresholds(TimeSpan haltOn, TimeSpan haltOff)
        {
            Interlocked.Exchange(ref haltOnDifference, haltOn.Ticks);
            Interlocked.Exchange(ref haltOffDifference, haltOff.Ticks);
        }

        public void UpdateClock(TimeSpan clock)
        {
            var referenceTicks = GetReferenceClockTicks();
            Clock = clock;

            ResetSyncElement(clock.Ticks, referenceTicks);
        }

        public void UpdateReferenceClock(TimeSpan reference)
        {
            var clockTicks = GetClockTicks();
            ReferenceClock = reference;

            SetSyncElement(clockTicks, reference.Ticks);
        }

        public void SetReferenceClockToClock()
        {
            var clockTicks = GetClockTicks();
            Interlocked.Exchange(ref _referenceClockTicks, clockTicks);

            syncWait.Set();
        }

        private void ResetSyncElement(long clockTicks, long referenceTicks)
        {
            var haltOnDiff = Volatile.Read(ref haltOnDifference);

            if (clockTicks - referenceTicks >= haltOnDiff && syncWait.IsSet)
                syncWait.Reset();
        }

        private void SetSyncElement(long clockTicks, long referenceTicks)
        {
            var haltOffDiff = Volatile.Read(ref haltOffDifference);

            if (clockTicks - referenceTicks <= haltOffDiff && !syncWait.IsSet)
                syncWait.Set();
        }

        public void Initialize()
        {
            Clock = InitialClock;
            ReferenceClock = InitialClock;
            syncWait.Set();
        }

        public void Dispose()
        {
            syncWait.Dispose();
        }
    }
}
