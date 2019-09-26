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
            public Task<Unit> ClockDetector;
            public bool IsClockDetected => ClockDetector?.IsCompleted ?? false;
            public bool IsSuspended;
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly SynchronizationData[] streamSyncData = new SynchronizationData[(int)StreamType.Count];
        private readonly AsyncBarrier streamSyncBarrier = new AsyncBarrier();
        private readonly ClockProvider playerClockSource = ClockProvider.GetClockProvider();

        private Task<Unit> DetectPlayerClock(CancellationToken token) =>
            playerClockSource
                .PlayerClockObservable()
                .Buffer(ClockDetectionConsecutiveValidClockCount, 1)
                .FirstAsync(args =>
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
                .ToTask(token);

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

            streamState.IsSuspended = false;
            streamSyncBarrier.AddParticipant();
            streamState.SyncState = SynchronizationState.Initialized;

            Logger.Info($"{stream}:");
        }

        public void SuspendStream(StreamType stream)
        {
            var streamState = streamSyncData[(int)stream];
            if (streamState.IsSuspended)
                return;

            streamState.IsSuspended = true;
            streamSyncBarrier.RemoveParticipant();

            Logger.Info($"{stream}:");
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

        private static Task DelayStream(SynchronizationData streamState, TimeSpan delay, CancellationToken token)
        {
            if (delay <= TimeSpan.Zero)
                return Task.CompletedTask;

            Logger.Info($"{streamState.StreamType}: Transferred {streamState.TransferredDuration} Delaying {delay}");

            return Task.Delay(delay, token).WithCancellation(token);
        }

        private static bool IsSyncPointReached(TimeSpan playerClock, ref TimeSpan targetClock, SynchronizationData streamState)
        {
            if (targetClock != ClockProviderConfig.InvalidClock)
                return playerClock >= targetClock;

            if (playerClock == ClockProviderConfig.InvalidClock)
                return false;

            var clockDiff = streamState.CurrentClock - playerClock - StreamClockMinimumOverhead;
            if (clockDiff <= TimeSpan.Zero)
                return true;

            targetClock = playerClock + clockDiff;

            Logger.Info($"{streamState.StreamType}: Sync {streamState.CurrentClock} to {playerClock} ({clockDiff}) Restart {targetClock}");
            return false;
        }

        private Task StreamSync(SynchronizationData streamState, CancellationToken token)
        {
            var playerClock = ClockProvider.LastClock;
            var desiredClock = ClockProviderConfig.InvalidClock;

            // Check quick exit path for borderline clock cases. New clock might have arrived between
            // last IsTransferCompleted() and now.
            if (playerClock != ClockProviderConfig.InvalidClock)
            {
                var clockDiff = streamState.CurrentClock - playerClock - StreamClockMinimumOverhead;
                if (clockDiff <= TimeSpan.Zero)
                    return Task.CompletedTask;

                desiredClock = playerClock + clockDiff;

                Logger.Info($"{streamState.StreamType}: Sync {streamState.CurrentClock} to {playerClock} ({clockDiff}) Restart {desiredClock}");
            }
            else
            {
                Logger.Info($"{streamState.StreamType}: Sync {streamState.CurrentClock} to --:--:-- (--:--:--) Restart --:--:--");
            }

            return playerClockSource.PlayerClockObservable()
                .FirstAsync(pClock => IsSyncPointReached(pClock, ref desiredClock, streamState))
                .ToTask(token);
        }

        private static bool IsTransferCompleted(SynchronizationData streamState)
        {
            if (streamState.SyncState == SynchronizationState.Synchronizing)
                return streamState.CurrentClock - ClockProvider.LastClock >= StreamClockMaximumOverhead;

            return streamState.TransferredDuration >= streamState.RequestedDuration;
        }

        private static StreamType OtherStream(StreamType thisStream) =>
            thisStream == StreamType.Audio ? StreamType.Video : StreamType.Audio;

        public async ValueTask<bool> Synchronize(StreamType streamType, CancellationToken token)
        {
            var streamState = streamSyncData[(int)streamType];

            if (!IsTransferCompleted(streamState))
                return false;

            switch (streamState.SyncState)
            {
                case SynchronizationState.Initialized:
                    Logger.Info(
                        $"{streamState.StreamType}: '{streamState.SyncState}' {streamState.CurrentClock} Waiting for {OtherStream(streamState.StreamType)}");
                    await streamSyncBarrier.SignalAndWait(token);

                    if (!streamState.SkipClockDetection)
                    {
                        streamState.ClockDetector = DetectPlayerClock(token);

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
                    Logger.Info(
                        $"{streamState.StreamType}: '{streamState.SyncState}' {streamState.CurrentClock} Waiting for {OtherStream(streamState.StreamType)}");
                    await streamSyncBarrier.SignalAndWait(token);

                    if (streamState.IsClockDetected)
                    {
                        streamSyncBarrier.RemoveParticipant();
                        streamState.ClockDetector = null;
                        streamState.SyncState = SynchronizationState.Synchronizing;
                        Logger.Info($"{streamState.StreamType}: '{streamState.SyncState}'");
                    }

                    await DelayStream(streamState, GetGenericDelay(streamState), token);
                    break;

                case SynchronizationState.Synchronizing:
                    var isWaiting = StreamSync(streamState, token);
                    if (isWaiting.IsCompleted)
                        return false;
                    await isWaiting;
                    return true;
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
            streamSyncBarrier.Reset();

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
                state.IsSuspended = false;

                streamSyncBarrier.AddParticipant();
            }

            Logger.Info("");
        }

        public void Dispose()
        {
            Logger.Info("");
            streamSyncBarrier.Reset();
        }
    }
}
