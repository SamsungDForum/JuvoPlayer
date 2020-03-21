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

using JuvoLogger;
using JuvoPlayer.Common;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using JuvoPlayer.Utils;
using static Configuration.DataSynchronizerConfig;
using TimeSpan = System.TimeSpan;

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
            public DateTimeOffset BeginTime;
            public DateTimeOffset EndTime;
            public TimeSpan Dts;
            public TimeSpan Pts;
            public TimeSpan NeededDuration;
            public TimeSpan TransferredDuration;
            public StreamType StreamType;
            public SynchronizationState SyncState;
            public bool KeyFrameSeen;
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly SynchronizationData[] _streamSyncData = new SynchronizationData[(int)StreamType.Count];
        private readonly AsyncBarrier<bool> _streamSyncBarrier = new AsyncBarrier<bool>();
        private readonly PlayerClockProvider _playerClockSource;
        private readonly Subject<TimeSpan> _ptsSubject = new Subject<TimeSpan>();
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

        private static void ResetTransferChunk(SynchronizationData streamState, TimeSpan delay, bool keyFramesSeen)
        {
            streamState.TransferredDuration = TimeSpan.Zero;
            streamState.NeededDuration = keyFramesSeen ? PostKeyFrameTransferDuration : PreKeyFrameTransferDuration;
            streamState.BeginTime = DateTimeOffset.Now + delay;
        }

        private static void UpdateTransferData(SynchronizationData streamState, Packet packet)
        {
            var lastClock = streamState.Dts;
            streamState.Dts = packet.Dts;
            streamState.Pts = packet.Pts;

            var clockDiff = streamState.Dts - lastClock;

            // Ignore clock discontinuities
            if (clockDiff > StreamClockDiscontinuityThreshold)
                clockDiff = StreamClockDiscontinuityThreshold;
            else if (clockDiff < TimeSpan.Zero)
                clockDiff = TimeSpan.Zero;

            streamState.TransferredDuration += clockDiff;

            if (streamState.KeyFrameSeen || !packet.IsKeyFrame) return;

            streamState.KeyFrameSeen = true;
            Logger.Info($"{packet.StreamType}: KeyFrame {packet.Dts}/{packet.Pts}");
        }

        private static async Task DelayStream(SynchronizationData streamState, bool keyFramesSeen, CancellationToken token)
        {
            var transferTime = streamState.EndTime - streamState.BeginTime;
            var transferredDuration = streamState.TransferredDuration;
            var delay = transferTime >= transferredDuration
                ? TimeSpan.Zero
                : transferredDuration - transferTime;

            ResetTransferChunk(streamState, delay, keyFramesSeen);
            Logger.Info($"{streamState.StreamType}: Delaying {delay}");
            if (delay == TimeSpan.Zero)
                return;

            await Task.Delay(delay, token).ConfigureAwait(false);
        }

        private async Task StreamSync(SynchronizationData streamState, CancellationToken token)
        {
            var playerClock = _playerClockSource.LastClock;

            var clockDiff = streamState.Dts - playerClock - StreamClockMinimumOverhead;
            if (clockDiff <= TimeSpan.Zero)
                return;

            var desiredClock = playerClock + clockDiff;

            Logger.Info($"{streamState.StreamType}: DTS {streamState.Dts} to {playerClock} Restart ({desiredClock})");

            await _playerClockSource.PlayerClockObservable()
                .FirstAsync(pClock => pClock >= desiredClock)
                .ToTask(token)
                .ConfigureAwait(false);
        }

        private bool IsPlayerClockRunning() =>
            _playerClockSource.LastClock != PlayerClockProviderConfig.InvalidClock;

        private bool IsTransferredDurationCompleted(SynchronizationData streamState)
        {
            return (streamState.SyncState == SynchronizationState.PlayerClockSynchronize)
                ? streamState.Pts - _playerClockSource.LastClock >= StreamClockMaximumOverhead
                : streamState.TransferredDuration >= streamState.NeededDuration;
        }

        public async Task Synchronize(StreamType streamType, CancellationToken token)
        {
            var streamState = _streamSyncData[(int)streamType];

            if (!IsTransferredDurationCompleted(streamState))
                return;

            switch (streamState.SyncState)
            {
                case SynchronizationState.ClockStart:
                    // Grab end time before going into sync barrier. Otherwise first stream entering barrier
                    // will have overly long transfer time due to wait for second stream.
                    streamState.EndTime = DateTimeOffset.Now;
                    Logger.Info($"{streamState.StreamType}: Sync. Pushed/Needed/Pts {streamState.TransferredDuration}/{streamState.NeededDuration}/{streamState.Pts}");
                    var keyFrames = await _streamSyncBarrier.Signal(streamState.KeyFrameSeen, token);

                    // Use key frame to determine drip feed value.
                    // Before all streams report key frame, use PreKeyFrameTransferDuration otherwise
                    // use PostKeyFrameTransferDuration
                    var allKeyFramesSeen = keyFrames.All(keyFrameSeen => keyFrameSeen);
                    await DelayStream(streamState, allKeyFramesSeen, token);

                    // Push pts out
                    if (streamState.StreamType == StreamType.Video)
                        _ptsSubject.OnNext(streamState.Pts);

                    // Pushing +300ms post key frame may not complete async ops on certain streams
                    // Async op completion also does not guarantee playback will commence.
                    // Use running clock to switch from ClockStart to PlayerClock synchronization
                    if (!IsPlayerClockRunning()) return;

                    Logger.Info($"{streamState.StreamType}: Clock started {_playerClockSource.LastClock}");
                    _streamSyncBarrier.RemoveParticipant();
                    streamState.SyncState = SynchronizationState.PlayerClockSynchronize;
                    return;

                case SynchronizationState.PlayerClockSynchronize:
                    await StreamSync(streamState, token);
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

            if (streamState.SyncState == SynchronizationState.PlayerClockSynchronize)
            {
                streamState.Dts = packet.Dts;
                streamState.Pts = packet.Pts;
                return;
            }

            UpdateTransferData(streamState, packet);
        }

        public void Prepare()
        {
            _streamSyncBarrier.Reset();

            var initClock = DateTimeOffset.Now;

            foreach (var state in _streamSyncData)
            {
                if (state == null)
                    continue;

                state.SyncState = SynchronizationState.ClockStart;
                state.BeginTime = initClock;
                state.EndTime = initClock;
                state.TransferredDuration = TimeSpan.Zero;
                state.NeededDuration = PreKeyFrameTransferDuration;
                state.Pts = PlayerClockProviderConfig.InvalidClock;
                state.Dts = PlayerClockProviderConfig.InvalidClock;
                state.KeyFrameSeen = false;
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
