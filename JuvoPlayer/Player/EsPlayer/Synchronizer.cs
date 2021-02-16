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
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Utils;
using static Configuration.DataSynchronizerConfig;


namespace JuvoPlayer.Player.EsPlayer
{
    internal class Synchronizer : IDisposable
    {
        private enum SynchronizationState
        {
            ClockStart,
            PlayerClockSynchronize
        }
        private class SynchronizationData
        {
            public TimeSpan Pts;
            public StreamType StreamType;
            public SynchronizationState SyncState;
            public TimeSpan LastSync;
        }

        private class RunningTime
        {
            private readonly DateTime _start;
            public readonly TimeSpan Offset;
            public TimeSpan Position
            {
                get { return DateTime.UtcNow - _start + Offset; }
            }

            public TimeSpan Elapsed
            {
                get { return DateTime.UtcNow - _start; }
            }

            public RunningTime(TimeSpan offsetValue = default)
            {
                Offset = offsetValue;
                _start = DateTime.UtcNow;
            }
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly SynchronizationData[] _streamSyncData = new SynchronizationData[(int)StreamType.Count];
        private readonly AsyncBarrier<bool> _streamSyncBarrier = new AsyncBarrier<bool>();
        private readonly PlayerClockProvider _playerClockSource;
        private readonly Subject<TimeSpan> _ptsSubject = new Subject<TimeSpan>();
        private RunningTime _syncClock;

        public Synchronizer(PlayerClockProvider playerClockSource)
        {
            _playerClockSource = playerClockSource;
        }

        public void Initialize(StreamType stream)
        {
            _streamSyncData[(int)stream] = new SynchronizationData
            {
                StreamType = stream
            };
        }

        public TimeSpan GetPts(StreamType stream)
        {
            return _streamSyncData[(int)stream].Pts;
        }

        private Task SyncStream(SynchronizationData streamState, CancellationToken token)
        {
            var playerClock = _playerClockSource.Clock;
            var targetClock = streamState.Pts - StreamClockMinimumOverhead;

            if (targetClock - playerClock >= MinimumDelayDuration)
            {
                Logger.Info($"{streamState.StreamType}: Pts {streamState.Pts} to {playerClock} resume {targetClock}");

                return _playerClockSource.PlayerClockObservable()
                    .FirstAsync(pClock => pClock >= targetClock)
                    .ToTask(token);
            }

            return Task.CompletedTask;
        }

        private static Task DelayStream(SynchronizationData streamState, TimeSpan referencePosition, CancellationToken token)
        {
            var delay = streamState.Pts - referencePosition - StreamClockMaximumOverhead;
            if (delay >= MinimumDelayDuration)
            {
                Logger.Info($"{streamState.StreamType}: Pts {streamState.Pts} Reference {referencePosition} Delay {delay}");
                return Task.Delay(delay, token);
            }

            return Task.CompletedTask;
        }

        private bool IsSyncRequired(SynchronizationData streamState)
        {
            if (streamState.SyncState == SynchronizationState.PlayerClockSynchronize)
                return streamState.Pts - _playerClockSource.Clock >= StreamClockMaximumOverhead;

            // _syncClock may not have been started yet.
            if (_syncClock == null)
                return false;

            // Force sync if there was none for MaximumSynchronisationInterval.
            // Or if stream position is ahead of reference clock by StreamClockMaximumOverhead.
            return _syncClock.Elapsed - streamState.LastSync >= MaximumSynchronisationInterval
                   || streamState.Pts - _syncClock.Position >= StreamClockMaximumOverhead;
        }

        public async ValueTask Synchronize(StreamType streamType, CancellationToken token)
        {
            var streamState = _streamSyncData[(int)streamType];

            if (!IsSyncRequired(streamState))
                return;

            switch (streamState.SyncState)
            {
                case SynchronizationState.ClockStart:
                    // Push pts out to keep data clock running.
                    if (streamState.StreamType == StreamType.Video)
                        _ptsSubject.OnNext(streamState.Pts);

                    var streamMsg = await _streamSyncBarrier.Signal(_playerClockSource.IsRunning, token);

                    // switch to player clock sync when all streams see it running.
                    if (streamMsg.All(msg => msg))
                    {
                        Logger.Info($"{streamState.StreamType}: Clock started {_playerClockSource.Clock}");
                        _streamSyncBarrier.RemoveParticipant();
                        streamState.SyncState = SynchronizationState.PlayerClockSynchronize;
                        return;
                    }

                    streamState.LastSync = _syncClock.Elapsed;
                    await DelayStream(streamState, _syncClock.Position, token);
                    return;

                case SynchronizationState.PlayerClockSynchronize:
                    await SyncStream(streamState, token);
                    return;
            }
        }

        public void DataOut(Packet packet)
        {
            var streamState = _streamSyncData[(int)packet.StreamType];

            if (!packet.ContainsData())
            {
                if (!(packet is EOSPacket))
                    return;

                Logger.Info($"{streamState.StreamType}: '{streamState.SyncState}' EOS {streamState.Pts}");

                if (streamState.SyncState != SynchronizationState.PlayerClockSynchronize)
                    _streamSyncBarrier.RemoveParticipant();

                return;
            }

            streamState.Pts = packet.Pts;

            // First packet starts the show.
            if (_syncClock == null && Interlocked.CompareExchange(ref _syncClock, new RunningTime(packet.Pts), null) == null)
                Logger.Info($"{packet.StreamType}: Sync clock offset {_syncClock.Offset}");
        }

        public void Prepare()
        {
            _streamSyncBarrier.Reset();
            _syncClock = null;

            foreach (var state in _streamSyncData)
            {
                if (state == null)
                    continue;

                state.SyncState = SynchronizationState.ClockStart;
                state.Pts = TimeSpan.Zero;
                state.LastSync = TimeSpan.Zero;
                _streamSyncBarrier.AddParticipant();
            }

            Logger.Info("");
        }

        public IObservable<TimeSpan> Pts()
        {
            return _ptsSubject.AsObservable();
        }

        public void Dispose()
        {
            Logger.Info("");
            _ptsSubject.OnCompleted();
            _ptsSubject.Dispose();
            _streamSyncBarrier.Reset();
        }
    }
}
