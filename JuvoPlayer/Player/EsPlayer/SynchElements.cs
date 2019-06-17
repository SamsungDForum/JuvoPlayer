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
        private long clockTicks;
        private long referenceClockTicks;

        public TimeSpan Clock
        {
            get => TimeSpan.FromTicks(Volatile.Read(ref clockTicks));
            private set => Interlocked.Exchange(ref clockTicks, value.Ticks);
        }

        public TimeSpan ReferenceClock
        {
            get => TimeSpan.FromTicks(Volatile.Read(ref referenceClockTicks));
            private set => Interlocked.Exchange(ref referenceClockTicks, value.Ticks);
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
            var reference = ReferenceClock;
            var haltOnDiff = Volatile.Read(ref haltOnDifference);

            if (clock.Ticks - reference.Ticks >= haltOnDiff && syncWait.IsSet)
                syncWait.Reset();

            Clock = clock;
        }

        public void UpdateReferenceClock(TimeSpan reference)
        {
            var clock = Clock;
            var haltOffDiff = Volatile.Read(ref haltOffDifference);

            if (clock.Ticks - reference.Ticks <= haltOffDiff && !syncWait.IsSet)
                syncWait.Set();

            ReferenceClock = reference;
        }


        public void Initialize()
        {
            Clock = TimeSpan.Zero;
            ReferenceClock = TimeSpan.Zero;
            syncWait.Set();
        }

        public void Dispose()
        {
            syncWait.Dispose();
        }
    }
}
