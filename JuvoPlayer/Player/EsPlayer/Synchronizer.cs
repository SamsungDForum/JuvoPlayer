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
            public bool IsKeyFrameSeen;
            // Represents a player asynchronous operation being executed
            // SeekAsync() / PrepareAsync(). 
            // Upon complition, running player clock is extected.
            public Task PlayerAsyncOperation;

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

        private static void UpdateTransferredDuration(SynchronizationData streamState, Packet packet)
        {
            if (streamState.FirstPts == default(TimeSpan))
                streamState.FirstPts = packet.Pts;

            if (!streamState.IsKeyFrameSeen && packet.IsKeyFrame)
            {
                // On first key frame, reset transfer duration to collect KeyFrameTransferDuration ammount
                // of data after key frame. Below this level, ESPlayer may not complete current async operations
                // (seek/prepare)
                streamState.IsKeyFrameSeen = true;

                streamState.RequestedDuration = streamState.TransferredDuration + KeyFrameTransferDuration;

                if (packet.StreamType == StreamType.Video)
                    Logger.Info($"{streamState.StreamType}: Key frame seen. Player clock alternative set to {streamState.FirstPts}");

            }

            var lastClock = streamState.Dts;
            streamState.Dts = packet.Dts;
            streamState.Pts = packet.Pts;

            var clockDiff = streamState.Dts - lastClock;

            if (clockDiff > DataBufferConfig.StreamClockDiscontinuityThreshold || clockDiff <= TimeSpan.Zero)
                return;

            streamState.TransferredDuration += clockDiff;
        }

        private static Task DelayStream(SynchronizationData streamState, CancellationToken token)
        {
            var transferTime = (DateTimeOffset.Now - streamState.BeginTime);
            if (transferTime >= streamState.TransferredDuration)
                return Task.CompletedTask;

            var delay = streamState.TransferredDuration - transferTime;

            Logger.Info($"{streamState.StreamType}: Delaying {delay}");

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

        private static bool IsTransferredDurationCompleted(SynchronizationData streamState)
        {
            if (streamState.SyncState == SynchronizationState.PlayerClockSynchronize)
                return streamState.Dts - ClockProvider.LastClock >= StreamClockMaximumOverhead;

            return streamState.TransferredDuration >= streamState.RequestedDuration;
        }

        public async ValueTask<bool> Synchronize(StreamType streamType, CancellationToken token)
        {
            var streamState = _streamSyncData[(int)streamType];

            if (!IsTransferredDurationCompleted(streamState))
                return false;

            switch (streamState.SyncState)
            {
                case SynchronizationState.KeyFrameSearch:
                    // Video stream controls transfer from KeyFrameSearch state to PlayerClockSynchronize
                    // When key frame gets detected, first video frame clock is sent to remaining streams.
                    // If player clock will not be available in initial stages of PlayerClockSynchronize, this
                    // value will be used as player clock
                    object msg = null;
                    if (streamType == StreamType.Video && streamState.IsKeyFrameSeen)
                        msg = streamState.FirstPts;

                    Task<object> streamWait = _streamSyncBarrier.Signal(msg);

                    // Synchronize A&V with each other. During KeyFrameSearch, they run independent.
                    Logger.Info($"{streamType}: Waiting for other streams");
                    await streamWait.WithCancellation(token);

                    // Check if transition to PlayerClockSynchronize is possible.
                    // If not, repeat KeyFrameSearch.
                    var waitMessage = streamWait.Result;
                    if (waitMessage != null)
                    {
                        // Video stream observed key frame
                        Logger.Info(
                            $"{streamState.StreamType}: '{streamState.SyncState}' {streamState.Dts} Waiting for AsyncOp completion");

                        streamState.FirstPts = (TimeSpan)waitMessage;
                        await streamState.PlayerAsyncOperation.WithCancellation(token).ConfigureAwait(false);
                        streamState.SyncState = SynchronizationState.PlayerClockSynchronize;
                        streamState.PlayerAsyncOperation = null;

                        Logger.Info($"{streamState.StreamType}: {streamState.SyncState}");
                        return true;
                    }

                    await DelayStream(streamState, token).ConfigureAwait(false);
                    ResetTransferChunk(streamState);
                    return false;

                case SynchronizationState.PlayerClockSynchronize:
                    await StreamSync(streamState, token).ConfigureAwait(false);
                    return true;
            }

            return false;
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

            UpdateTransferredDuration(streamState, packet);
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

                state.PlayerAsyncOperation = asyncOp;
            }
        }

        public void Dispose()
        {
            Logger.Info("");
            _streamSyncBarrier.Reset();
        }
    }
}
