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
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using static Configuration.DataSynchronizerConfig;

namespace JuvoPlayer.Player.EsPlayer
{
    internal class Synchronizer : IDisposable
    {
        private enum SynchronizationState
        {
            Initialized,
            Started,
            Synchronizing
        }
        private class SynchronizationData
        {
            public bool SkipClockDetection;
            public DateTimeOffset BeginTime;
            public TimeSpan CurrentClock;
            public TimeSpan RequestedDuration;
            public TimeSpan TransferredDuration;
            public StreamType StreamType;
            public SynchronizationState SyncState;
            public IDisposable ClockDetectSubscription;
            public bool IsClockDetected;
            public bool IsBarrierInUse;
            public bool IsSuspended;
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly SynchronizationData[] streamSyncData = new SynchronizationData[(int)StreamType.Count];
        private readonly Barrier streamSyncBarrier = new Barrier(0);
        private static readonly ValueTask CompletedValueTask = new ValueTask();
        private readonly ClockProvider PlayerClockSource = ClockProvider.GetClockProvider();
        private readonly SynchronizationContext syncContext = SynchronizationContext.Current;

        private ValueTask<IObservable<Unit>> DetectPlayerClockObservable(CancellationToken token)
        {
            return new ValueTask<IObservable<Unit>>(
                PlayerClockSource
                .PlayerClockObservable()
                .TakeUntil(_ => token.IsCancellationRequested)
                .Buffer(ClockDetectionConsecutiveValidClockCount, 1)
                .Where(args =>
                {
                    var lastClock = TimeSpan.Zero;

                    foreach (var clock in args)
                    {
                        if (clock <= lastClock)
                            return false;

                        lastClock = clock;
                    }

                    return true;
                })
                .Select(_ => Unit.Default)
                .TakeUntil(_ => true)
                .TakeLast(1));
        }

        public void Initialize(StreamType stream)
        {
            streamSyncData[(int)stream] = new SynchronizationData
            {
                StreamType = stream
            };
        }

        public void ResumeStream(StreamType stream)
        {
            var streamState = streamSyncData[(int)stream];

            if (!streamState.IsSuspended)
                return;

            Logger.Info($"{stream}: '{streamState.SyncState}'");

            streamState.IsSuspended = false;

            if (streamState.SyncState == SynchronizationState.Synchronizing)
                return;

            streamSyncBarrier.AddParticipant();
            streamState.IsBarrierInUse = true;

            // Resuming "started" stream needs to go back to init stage.
            // No token at this point to restart clock detection.
            streamState.SyncState = SynchronizationState.Initialized;
        }

        public void SuspendStream(StreamType stream)
        {
            var streamState = streamSyncData[(int)stream];
            if (streamState.IsSuspended)
                return;

            streamState.IsSuspended = true;

            LeaveBarrier(streamState);

            streamState.ClockDetectSubscription?.Dispose();
            streamState.ClockDetectSubscription = null;

            Logger.Info($"{stream}: '{streamState.SyncState}'");
        }

        private void LeaveBarrier(SynchronizationData streamState)
        {
            if (!streamState.IsBarrierInUse)
                return;

            streamSyncBarrier.RemoveParticipant();
            streamState.IsBarrierInUse = false;
        }

        private static void ResetTransferChunk(SynchronizationData streamState)
        {
            streamState.TransferredDuration = TimeSpan.Zero;
            streamState.RequestedDuration = DefaultChunkDuration;
            streamState.BeginTime = DateTimeOffset.Now;
        }

        private static void UpdateTransferChunk(SynchronizationData streamState, TimeSpan clock)
        {
            var lastClock = streamState.CurrentClock;
            streamState.CurrentClock = clock;

            var clockDiff = streamState.CurrentClock - lastClock;

            if (clockDiff > DataBufferConfig.StreamClockDiscontinuityThreshold || clockDiff <= TimeSpan.Zero)
            {
                Logger.Info($"{streamState.StreamType}: Clock Discontinuity {clockDiff} {streamState.CurrentClock}/{lastClock}");
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

        private void WaitForRemainingStreams(SynchronizationData streamState, CancellationToken token)
        {
            Logger.Info($"{streamState.StreamType}: '{streamState.SyncState}' {streamState.CurrentClock} Waiting for {streamSyncBarrier.ParticipantsRemaining - 1} streams");
            streamSyncBarrier.SignalAndWait(token);
        }

        private static Task DelayStream(SynchronizationData streamState, TimeSpan delay, CancellationToken token)
        {
            if (delay <= TimeSpan.Zero)
                return Task.CompletedTask;

            Logger.Info($"{streamState.StreamType}: Transferred {streamState.TransferredDuration} Delaying {delay}");

            return Task.Delay(delay, token).WithCancellation(token);
        }

        private async Task<bool> StreamSync(SynchronizationData streamState, CancellationToken token)
        {
            var playerClock = ClockProvider.LastClock;

            if (playerClock == ClockProviderConfig.InvalidClock)
            {
                Logger.Warn($"{streamState.StreamType}: No clock to sync with");
                return true;
            }

            var clockDiff = streamState.CurrentClock - playerClock - StreamClockMinimumOverhead;
            if (clockDiff <= TimeSpan.Zero)
                return false;

            var desiredClock = playerClock + clockDiff;

            Logger.Info($"{streamState.StreamType}: Sync {streamState.CurrentClock} to {playerClock} ({clockDiff}) Restart {desiredClock}");

            var clockArrived = new TaskCompletionSource<object>();
            using (PlayerClockSource.PlayerClockObservable()
                .TakeUntil(_ => token.IsCancellationRequested)
                .Subscribe(clockValue =>
            {
                if (clockValue >= desiredClock)
                    clockArrived.SetResult(null);
            }))
            {
                // Do not care on which thread/context this continuation gets executed.
                // Upon exit from SynchronizePlayingStream() calling thread/context will be used.
                await clockArrived.Task.WithCancellation(token).ConfigureAwait(false);
            }

            return true;
        }

        private static bool IsTransferCompleted(SynchronizationData streamState)
        {
            if (streamState.SyncState == SynchronizationState.Synchronizing)
                return streamState.CurrentClock - ClockProvider.LastClock >= StreamClockMaximumOverhead;

            return streamState.TransferredDuration >= streamState.RequestedDuration;
        }

        private async ValueTask StartClockDetection(SynchronizationData streamState, CancellationToken token)
        {
            streamState.ClockDetectSubscription = (await DetectPlayerClockObservable(token))
                .Subscribe(_ => streamState.IsClockDetected = true, syncContext);
        }

        private async ValueTask StopClockDetection(SynchronizationData streamState)
        {
            LeaveBarrier(streamState);
            await TerminateClockDetection(streamState);
        }

        private static ValueTask TerminateClockDetection(SynchronizationData state)
        {
            state.ClockDetectSubscription.Dispose();
            state.ClockDetectSubscription = null;
            Logger.Info($"{state.StreamType}: Running clock detected");
            return CompletedValueTask;
        }

        public async ValueTask<bool> Synchronize(StreamType streamType, CancellationToken token)
        {
            var streamState = streamSyncData[(int)streamType];

            if (!IsTransferCompleted(streamState))
                return false;

            switch (streamState.SyncState)
            {
                case SynchronizationState.Initialized:
                    WaitForRemainingStreams(streamState, token);

                    if (!streamState.SkipClockDetection)
                    {
#pragma warning disable 4014 // Execution to take place in "parallel" with stream delay
                        StartClockDetection(streamState, token);
#pragma warning restore 4014

                        streamState.SyncState = SynchronizationState.Started;
                        await DelayStream(streamState, GetGenericDelay(streamState), token);
                    }
                    else
                    {
                        streamState.SyncState = SynchronizationState.Synchronizing;
                        Logger.Info($"{streamState.StreamType}: Skipping clock detection");
                    }

                    Logger.Info($"{streamState.StreamType}: {streamState.SyncState}");
                    break;

                case SynchronizationState.Started:
                    WaitForRemainingStreams(streamState, token);

                    if (streamState.IsClockDetected)
                    {
#pragma warning disable 4014 // Execution to take place in "parallel" with stream delay
                        StopClockDetection(streamState);
#pragma warning restore 4014

                        streamState.SyncState = SynchronizationState.Synchronizing;
                        Logger.Info($"{streamState.StreamType}: {streamState.SyncState}");
                    }

                    await DelayStream(streamState, GetGenericDelay(streamState), token);
                    break;

                case SynchronizationState.Synchronizing:
                    return await StreamSync(streamState, token);
            }

            ResetTransferChunk(streamState);
            return false;
        }

        public void DataOut(Packet packet)
        {
            if (!packet.ContainsData())
                return;

            var streamState = streamSyncData[(int)packet.StreamType];

            if (streamState.SyncState == SynchronizationState.Synchronizing)
            {
                streamState.CurrentClock = packet.Dts;
                return;
            }

            UpdateTransferChunk(streamState, packet.Dts);
        }

        public void InitializeSynchronization(bool skipClockDetection)
        {
            if (streamSyncBarrier.ParticipantCount > 0)
                streamSyncBarrier.RemoveParticipants(streamSyncBarrier.ParticipantCount);

            var validStreamCount = 0;
            var initClock = DateTimeOffset.Now;

            foreach (var state in streamSyncData)
            {
                if (state == null)
                    continue;

                state.SyncState = SynchronizationState.Initialized;
                state.BeginTime = initClock;
                state.TransferredDuration = TimeSpan.Zero;
                state.RequestedDuration = InitialChunkDuration;
                state.CurrentClock = ClockProviderConfig.InvalidClock;

                state.SkipClockDetection = skipClockDetection;
                state.ClockDetectSubscription?.Dispose();
                state.ClockDetectSubscription = null;
                state.IsClockDetected = false;
                state.IsBarrierInUse = true;
                state.IsSuspended = false;

                validStreamCount++;
            }

            streamSyncBarrier.AddParticipants(validStreamCount);

            Logger.Info("");
        }

        public void Dispose()
        {
            Logger.Info("");

            if (streamSyncBarrier.ParticipantCount > 0)
                streamSyncBarrier.RemoveParticipants(streamSyncBarrier.ParticipantCount);

            streamSyncBarrier.Dispose();

            foreach (var state in streamSyncData)
                state?.ClockDetectSubscription?.Dispose();
        }
    }
}
