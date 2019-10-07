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
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using JuvoPlayer.Utils;
using static Configuration.DataSynchronizerConfig;

namespace JuvoPlayer.Player.EsPlayer
{
    internal class Synchronizer : IDisposable
    {
        private enum SynchronizationState
        {
            KeyFrameSearch,
            PlayerClockSynchronize
        }
        private class SynchronizationData
        {
            public TimeSpan FirstPts;
            public DateTimeOffset BeginTime;
            public TimeSpan Dts;
            public TimeSpan Pts;
            public TimeSpan RequestedDuration;
            public TimeSpan TransferredDuration;
            public StreamType StreamType;
            public SynchronizationState SyncState;
            public Task AsyncOperation;
            public bool IsKeyFrameSeen;
            public bool IsEosSeen;
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly SynchronizationData[] _streamSyncData = new SynchronizationData[(int)StreamType.Count];
        private readonly AsyncBarrier _streamSyncBarrier = new AsyncBarrier();
        private readonly ClockProvider _playerClockSource = new ClockProvider();

        public void Initialize(StreamType stream)
        {
            _streamSyncData[(int)stream] = new SynchronizationData
            {
                StreamType = stream
            };
        }

        private static void ResetTransferChunk(SynchronizationData streamState)
        {
            streamState.TransferredDuration = TimeSpan.Zero;
            streamState.RequestedDuration = DefaultTransferDuration;
            streamState.BeginTime = DateTimeOffset.Now;
        }

        private static void UpdateTransferdDuration(SynchronizationData streamState, Packet packet)
        {
            if (streamState.FirstPts == default(TimeSpan))
                streamState.FirstPts = packet.Pts;

            if (!streamState.IsKeyFrameSeen && packet.IsKeyFrame)
            {
                // On first key frame, reset transfer duration to collect KeyFrameTransferDuration ammount
                // of data after key frame. Below this level, ESPlayer may not complete current async operations
                // (seek/prepare)
                streamState.IsKeyFrameSeen = true;
                streamState.TransferredDuration = TimeSpan.Zero;
                streamState.RequestedDuration = KeyFrameTransferDuration;

                if (packet.StreamType == StreamType.Video && packet.IsKeyFrame)
                    Logger.Info($"{streamState.StreamType}: Key frame seen {packet.Pts}/{packet.Dts}. First PTS {streamState.FirstPts}");

            }

            var lastClock = streamState.Dts;
            streamState.Dts = packet.Dts;
            streamState.Pts = packet.Pts;

            var clockDiff = streamState.Dts - lastClock;

            if (clockDiff > DataBufferConfig.StreamClockDiscontinuityThreshold || clockDiff <= TimeSpan.Zero)
            {
                Logger.Info($"{streamState.StreamType}: Clock Discontinuity {clockDiff} {streamState.Dts}/{lastClock}");
                return;
            }

            streamState.TransferredDuration += clockDiff;
        }


        private static TimeSpan GetGenericDelay(SynchronizationData streamState)
        {
            var transferTime = (DateTimeOffset.Now - streamState.BeginTime);
            if (transferTime >= streamState.TransferredDuration)
                return TimeSpan.Zero;

            return streamState.TransferredDuration - transferTime;
        }

        private static Task DelayStream(StreamType streamType, TimeSpan transferred, TimeSpan delay, CancellationToken token)
        {
            if (delay <= TimeSpan.Zero)
                return Task.CompletedTask;

            Logger.Info($"{streamType}: Transferred {transferred} Delaying {delay}");

            return Task.Delay(delay, token);
        }


        private Task StreamSync(SynchronizationData streamState, CancellationToken token)
        {
            var playerClock = ClockProvider.LastClock;
            if (playerClock < TimeSpan.Zero)
                playerClock = streamState.FirstPts;

            var clockDiff = streamState.Pts - playerClock - StreamClockMinimumOverhead;
            if (clockDiff <= TimeSpan.Zero)
                return Task.CompletedTask;

            var desiredClock = playerClock + clockDiff;

            Logger.Info($"{streamState.StreamType}: Sync {streamState.Dts} to {playerClock} ({clockDiff}) Restart {desiredClock}");

            return _playerClockSource.PlayerClockObservable()
                .FirstAsync(pClock => pClock >= desiredClock)
                .ToTask(token);
        }

        private static bool IsTransferedDurationCompleted(SynchronizationData streamState)
        {
            // Once EOS is seen
            if (streamState.IsEosSeen)
                return false;

            if (streamState.SyncState == SynchronizationState.PlayerClockSynchronize)
                return streamState.Dts - ClockProvider.LastClock >= StreamClockMaximumOverhead;

            return streamState.TransferredDuration >= streamState.RequestedDuration;
        }

        public async ValueTask<bool> Synchronize(StreamType streamType, CancellationToken token)
        {
            var streamState = _streamSyncData[(int)streamType];

            if (!IsTransferedDurationCompleted(streamState))
                return false;

            switch (streamState.SyncState)
            {
                case SynchronizationState.KeyFrameSearch:
                    Logger.Info(
                        $"{streamState.StreamType}: '{streamState.SyncState}' {streamState.Dts} Waiting for AsyncOp completion");

                    object msg = null;

                    // Video stream controls transfer from KeyFrameSearch state to PlayerClockSynchronize
                    // When key frame gets detected, first video frame clock is sent to remaining streams.
                    // If player clock will not be available in initial stages of PlayerClockSynchronize, this
                    // value will be used as player clock
                    if (streamType == StreamType.Video && streamState.IsKeyFrameSeen)
                        msg = streamState.FirstPts;

                    // Synchronize A&V. At this point they run independant of each other.
                    var waitTask = _streamSyncBarrier.Signal(msg);
                    await waitTask.WithCancellation(token);

                    // Check if transition to PlayerClockSynchronize is possible.
                    // If not, repeat KeyFrameSearch.
                    var waitMessage = waitTask.Result;
                    if (waitMessage == null)
                        break;

                    // Video stream observed key frame
                    streamState.FirstPts = (TimeSpan)waitMessage;
                    await streamState.AsyncOperation.WithCancellation(token);
                    streamState.SyncState = SynchronizationState.PlayerClockSynchronize;
                    streamState.AsyncOperation = null;

                    Logger.Info($"{streamState.StreamType}: {streamState.SyncState}");
                    break;

                case SynchronizationState.PlayerClockSynchronize:
                    var isWaiting = StreamSync(streamState, token);
                    if (isWaiting.IsCompleted)
                        return false;
                    await isWaiting.ConfigureAwait(false);
                    return true;
            }

            var transferred = streamState.TransferredDuration;
            var delay = GetGenericDelay(streamState);
            ResetTransferChunk(streamState);
            await DelayStream(streamState.StreamType, transferred, delay, token).ConfigureAwait(false);

            return false;
        }

        public void DataOut(Packet packet)
        {
            var streamState = _streamSyncData[(int)packet.StreamType];

            if (!packet.ContainsData())
            {
                if (streamState.IsEosSeen || !(packet is EOSPacket))
                    return;

                // Don't sychronize after EOS.
                Logger.Info($"{streamState.StreamType}: 'streamState.SyncState' EOS {streamState.Pts}");
                streamState.IsEosSeen = true;
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

            UpdateTransferdDuration(streamState, packet);
        }

        public void Prepare()
        {
            _streamSyncBarrier.Reset();

            var initClock = DateTimeOffset.Now;

            foreach (var state in _streamSyncData)
            {
                if (state == null)
                    continue;

                state.FirstPts = default(TimeSpan);
                state.SyncState = SynchronizationState.KeyFrameSearch;
                state.BeginTime = initClock;
                state.TransferredDuration = TimeSpan.Zero;
                state.RequestedDuration = DefaultTransferDuration;
                state.Dts = ClockProviderConfig.InvalidClock;
                state.IsKeyFrameSeen = false;
                state.IsEosSeen = false;

                _streamSyncBarrier.AddParticipant();
            }

            Logger.Info("");
        }

        public void SetAsyncOperation(Task asyncOp)
        {
            foreach (var state in _streamSyncData)
            {
                if (state == null)
                    continue;

                state.AsyncOperation = asyncOp;
            }
        }

        public void Dispose()
        {
            Logger.Info("");
            _streamSyncBarrier.Reset();
        }
    }
}
