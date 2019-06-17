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
    internal class DataSynchronizer : IDisposable
    {
        public enum SynchronizationMode
        {
            PlayStart,
            SeekStart,
            Play
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly ClockSynchronizer[] clockSyncs = new ClockSynchronizer[(int)StreamType.Count];
        private readonly long[] lastClocksTicks = new long[(int)StreamType.Count];
        private readonly PlayerClock playerClock = new PlayerClock();
        private readonly bool[] ignoreSync = new bool[(int)StreamType.Count];

        private IDisposable clockSubscription;

        private SynchronizationMode syncMode = SynchronizationMode.PlayStart;

        private TimeSpan playerGeneratedClock;
        private TimeSpan accumulatedStartDuration;
        private int invalidClockReSyncCount;

        public void Initialize(StreamType stream)
        {
            clockSyncs[(int) stream] = new ClockSynchronizer(stream);
        }

        private long GetLastTicks(StreamType stream) =>
            Volatile.Read(ref lastClocksTicks[(int) stream]);

        public void UpdateClock(Packet packet)
        {
            // Non data packet - Ignore sync as they carry no clock.
            if (!packet.ContainsData())
            {
                ignoreSync[(int) packet.StreamType] = true;
                return;
            }
            
            var clock = packet.Dts;

            if (syncMode != SynchronizationMode.Play)
            {
                // In play start mode, stream may begin with non zero clock.
                // Sync with first packet clock.
                if (syncMode == SynchronizationMode.PlayStart &&
                    lastClocksTicks[(int) packet.StreamType] == long.MaxValue)
                {
                    clockSyncs[(int)packet.StreamType].UpdateReferenceClock(clock);
                }

                Interlocked.Exchange(ref lastClocksTicks[(int) packet.StreamType], clock.Ticks);

                //if(packet.StreamType == StreamType.Video && packet.IsKeyFrame)
                //    Logger.Info($"*** key Frame: {clock} {packet.Pts}");
            }

            clockSyncs[(int)packet.StreamType].UpdateClock(clock);
        }

        private void InitializeOperation()
        {
            playerGeneratedClock = TimeSpan.MaxValue;
            accumulatedStartDuration = TimeSpan.Zero;
            invalidClockReSyncCount = InvalidClockReSyncCount;

            for (var i = 0; i < (int) StreamType.Count; i++)
                Interlocked.Exchange(ref lastClocksTicks[i],long.MinValue);
        }

        private void CreateStartClockSubscription()
        {
            clockSubscription = playerClock
                .ClockSource()
                .Sample(TimeSpan.FromSeconds(0.1))
                .Subscribe(ClockStartMonitor, SynchronizationContext.Current);
        }

        private void CreatePlayClockSubscription()
        {
            clockSubscription = playerClock
                .ClockSource()
                .ValidClock()
                .Sample(ClockSampleInterval)             
                .Subscribe(UpdateReferenceClock, SynchronizationContext.Current);
        }

        public void SeekStartSynchronization(TimeSpan clock)
        {
            Logger.Info(clock.ToString());

            syncMode = SynchronizationMode.SeekStart;

            InitializeOperation();
            SetThresholds(StartClockDifferencePause, StartClockDifferenceResume);
            UpdateReferenceClock(clock);
            CreateStartClockSubscription();
        }

        public void PlayStartSynchronization()
        {
            Logger.Info("");

            syncMode = SynchronizationMode.PlayStart;

            InitializeOperation();
            SetThresholds(StartClockDifferencePause, StartClockDifferenceResume);
            CreateStartClockSubscription();
        }

        public void PlaySynchronization(TimeSpan startClock)
        {
            Logger.Info("");

            syncMode = SynchronizationMode.Play;

            DisposeClockSubscription();
            SetThresholds(PlayClockDifferencePause,PlayClockDifferenceResume);
            UpdateReferenceClock(startClock);
            CreatePlayClockSubscription();
        }

        public bool Synchronize(StreamType stream, CancellationToken token)
        {
            if (ignoreSync[(int) stream] || clockSyncs[(int) stream].IsSynchronized)
            {
                ignoreSync[(int) stream] = false;
                return false;
            }

            var clock = clockSyncs[(int) stream].Clock;
            var reference = clockSyncs[(int) stream].ReferenceClock;

            Logger.Info($"{stream}: {syncMode} sync {clock} with {reference}");
            clockSyncs[(int)stream].Wait(token);

            return true;
        }


        private void ClockStartMonitor(TimeSpan clock)
        {
            if (syncMode == SynchronizationMode.Play)
                return;
            
            if (clock < playerGeneratedClock)
            {
                playerGeneratedClock = clock;
                accumulatedStartDuration = TimeSpan.Zero;
            }

            accumulatedStartDuration += clock - playerGeneratedClock;

            if (accumulatedStartDuration >= ClockRunningThreshold)
            {
                PlaySynchronization(clock);
                return;
            }

            // Self clock streams if clock has not started. 
            var audioSync = clockSyncs[(int) StreamType.Video].IsSynchronized;
            var videoSync = clockSyncs[(int) StreamType.Audio].IsSynchronized;
            if (audioSync || videoSync) 
                return;

            // Self clock occurs only if BOTH streams have seen valid packets
            // preventing streams getting out of sync
            var audioTicks = GetLastTicks(StreamType.Audio);
            var videoTicks = GetLastTicks(StreamType.Video);
            if (audioTicks == long.MinValue || videoTicks == long.MinValue)
                return;

            if (!PlayerClock.IsClockValid(clock))
            {
                if (invalidClockReSyncCount <= 0)
                    return;

                invalidClockReSyncCount--;
            }

            var audioClock = TimeSpan.FromTicks(audioTicks);
            var videoClock = TimeSpan.FromTicks(videoTicks);

            Logger.Info($"Clock ReSync. Audio {audioClock} Video {videoClock}");
            clockSyncs[(int) StreamType.Video].UpdateReferenceClock(videoClock);
            clockSyncs[(int) StreamType.Audio].UpdateReferenceClock(audioClock);
        }

        private void UpdateReferenceClock(TimeSpan clock)
        {
            foreach (var syncEntry in clockSyncs)
            {
                syncEntry?.UpdateReferenceClock(clock);
            }
        }

        private void SetThresholds(TimeSpan haltOn, TimeSpan haltOff)
        {
            foreach (var syncElement in clockSyncs)
            {
                syncElement?.SetThresholds(haltOn, haltOff);
            }
        }

        private void InitializeSyncElements()
        {
            foreach (var syncElement in clockSyncs)
            {
                syncElement?.Initialize();
            }
        }

        private void DisposeClockSubscription()
        {
            playerClock.Reset();

            clockSubscription?.Dispose();
            clockSubscription = null;
        }

        public void Reset()
        {
            Logger.Info("");

            DisposeClockSubscription();

            InitializeSyncElements();
        }
        
        public void Dispose()
        {
            DisposeClockSubscription();
            playerClock.Dispose();
        }
    }
}
