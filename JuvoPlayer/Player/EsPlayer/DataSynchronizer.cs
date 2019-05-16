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
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly ClockSynchronizer[] clockSync = new ClockSynchronizer[(int)StreamType.Count];

        private readonly ClockFilter clockFilter = new ClockFilter();
        private IDisposable clockSubscription;

        private bool videoKeyFrameFound;

        public void Initialize(StreamType stream) =>
            clockSync[(int)stream] = new ClockSynchronizer(StreamClockDifferencePause, StreamClockDifferenceResume);

        public void DataIn(Packet packet)
        {
            var clock = packet.Dts;
            var idx = (int)packet.StreamType;

            clockSync[idx].DataIn(clock);

            if (videoKeyFrameFound)
                return;

            switch (packet.StreamType)
            {
                case StreamType.Audio:
                    clockSync[(int)StreamType.Video]?.ReferenceIn(clock);
                    break;
                case StreamType.Video:
                    clockSync[(int)StreamType.Audio]?.ReferenceIn(clock);
                    if (!packet.IsKeyFrame)
                        break;

                    // Key frame found.
                    // Switch from stream-stream synch to stream-player synch
                    SwitchSynchronizationSource(clock);
                    break;
            }
        }

        private void SwitchSynchronizationSource(TimeSpan referenceClock)
        {
            Logger.Info("");

            UpdateThreasholds(PlayerClockDifferencePause, PlayerClockDifferenceResume);
            ReferenceIn(referenceClock);

            clockSubscription = clockFilter
                .ClockValue()
                .EnabledClock(clockFilter)
                .ValidClock()
                .ClockOrInitial(clockFilter)
                .Sample(ClockSampleInterval)
                .DistinctUntilChanged()
                .Subscribe(ReferenceIn, SynchronizationContext.Current);

            videoKeyFrameFound = true;
            clockFilter.Enable();
        }

        public bool IsSynchronized(StreamType stream) =>
            clockSync[(int)stream].IsSynchronized;

        public void Synchronize(StreamType stream, CancellationToken token)
        {
            Logger.Info($"{stream}: {clockSync[(int)stream].Data} {(videoKeyFrameFound ? "Sync with player" : "Sync with stream")} {clockSync[(int)stream].Reference}");
            clockSync[(int)stream].Wait(token);
        }

        private void ReferenceIn(TimeSpan clock)
        {
            foreach (var clockSynchronizer in clockSync)
                clockSynchronizer?.ReferenceIn(clock);
        }

        private void UpdateThreasholds(TimeSpan haltOn, TimeSpan haltOff)
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

            UpdateThreasholds(StreamClockDifferencePause, StreamClockDifferenceResume);

            videoKeyFrameFound = false;
        }

        public void SetInitialClock(TimeSpan initialClock) =>
            clockFilter.SetInitialClock(initialClock);

        public void Dispose()
        {
            clockSubscription?.Dispose();
        }
    }
}
