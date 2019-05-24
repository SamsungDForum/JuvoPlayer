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
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common;
using static Configuration.DataSynchronizerConfig;

namespace JuvoPlayer.Player.EsPlayer
{
    public class DataSynchronizer : IDisposable
    {
        private enum SynchronizationType
        {
            Uninitialized,
            Start,
            Play
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly ClockSynchronizer[] clockSync = new ClockSynchronizer[(int)StreamType.Count];

        private readonly ClockFilter clockFilter = new ClockFilter();
        private IDisposable clockSubscription;

        private SynchronizationType synchType = SynchronizationType.Uninitialized;

        public void Initialize(StreamType stream) =>
            clockSync[(int)stream] = new ClockSynchronizer(StartClockDifferencePause, StartClockDifferenceResume);

        private TimeSpan? startClock;

        public void DataIn(Packet packet)
        {
            var clock = packet.Dts;
            var idx = (int)packet.StreamType;

            clockSync[idx].DataIn(clock);

            switch (synchType)
            {
                case SynchronizationType.Play:
                    return;
                case SynchronizationType.Uninitialized:
                    throw new InvalidOperationException("Synchronization mode not set");
            }

            // Synch mode is Start
            if (packet.StreamType != StreamType.Video)
                return;

            if (!startClock.HasValue)
                startClock = clock;

            if (clock - startClock >= StartClockLimit)
                return;

            ReferenceIn(clock);
        }

        public void SetStartSynchronization()
        {
            if (synchType == SynchronizationType.Start)
                return;

            // Start synch mode is driven by video clock.
            // Audio push is initially disabled forcing video data to be 
            // fed to player as first, followed by audio up to StartClockLimit
            // Once StartClockLimit is reached, no further data will be passed till
            // switch to PlaySynchronization is made via SetPlaySynchronization()
            Logger.Info("");

            UpdateThresholds(StartClockDifferencePause, StartClockDifferenceResume);

            clockSync[(int)StreamType.Video].Set();
            clockSync[(int)StreamType.Audio].Reset();
            startClock = null;
            synchType = SynchronizationType.Start;
        }

        public void SetPlaySynchronization()
        {
            if (synchType == SynchronizationType.Play)
                return;

            Logger.Info("");

            clockSubscription = clockFilter
                .ClockValue()
                .EnabledClock(clockFilter)
                .ValidClock()
                .ClockOrInitial(clockFilter)
                .Sample(ClockSampleInterval)
                .DistinctUntilChanged()
                .Subscribe(ReferenceIn, SynchronizationContext.Current);

            UpdateThresholds(PlayClockDifferencePause, PlayClockDifferenceResume);
            clockFilter.Enable();
            synchType = SynchronizationType.Play;
        }

        public bool IsSynchronized(StreamType stream) =>
            clockSync[(int)stream].IsSynchronized;

        public void Synchronize(StreamType stream, CancellationToken token)
        {
            Logger.Info($"{stream}: {clockSync[(int)stream].Data} Sync {synchType} {clockSync[(int)stream].Reference}");
            clockSync[(int)stream].Wait(token);
        }

        private void ReferenceIn(TimeSpan clock)
        {
            foreach (var clockSynchronizer in clockSync)
                clockSynchronizer?.ReferenceIn(clock);
        }

        private void UpdateThresholds(TimeSpan haltOn, TimeSpan haltOff)
        {
            foreach (var syncElement in clockSync)
            {
                syncElement?.SetThresholds(haltOn, haltOff);
            }
        }

        public void Reset()
        {
            Logger.Info("");

            clockSubscription?.Dispose();
            clockSubscription = null;

            clockFilter.Disable();
            clockFilter.Reset();

            foreach (var syncElement in clockSync)
            {
                syncElement?.Initialize();
            }
        }

        public void SetInitialClock(TimeSpan initClock) =>
            clockFilter.SetInitialClock(initClock);

        public void Dispose()
        {
            clockSubscription?.Dispose();
        }
    }
}
