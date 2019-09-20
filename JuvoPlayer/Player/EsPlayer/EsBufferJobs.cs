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

using JuvoPlayer.Common;
using JuvoPlayer.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static Configuration.DataBufferConfig;
using Configuration;
using Nito.AsyncEx;

namespace JuvoPlayer.Player.EsPlayer
{
    internal partial class EsBuffer
    {
        private enum JobPriorities
        {
            High = 0,
            Normal,
            Count
        }

        private struct PacketStreamClock
        {
            public TimeSpan Clock;
            public StreamType Stream;
        }

        private struct PacketInData : PriorityJobChannel.IJob
        {
            public PacketStreamClock Packet;
            public int Size;
            public bool IsEos;
            public uint Priority => (uint)JobPriorities.Normal;
        }

        private struct PacketOutData : PriorityJobChannel.IJob
        {
            public PacketStreamClock Packet;
            public int Size;
            public uint Priority => (uint)JobPriorities.Normal;
        }

        private struct ResetData : PriorityJobChannel.IJob
        {
            public TaskCompletionSource<object> Notifier;
            public uint Priority => (uint)JobPriorities.Normal;
        }

        private struct BufferConfigurationData : PriorityJobChannel.IJob
        {
            public TimeSpan Duration;
            public TimeSpan MinBufferTime;
            public StreamType Stream;
            public uint Priority => (uint)JobPriorities.High;
        }

        private struct AllowedEventsData : PriorityJobChannel.IJob
        {
            public DataEvent Events;
            public uint Priority => (uint)JobPriorities.High;
        }

        private struct PeriodicUpdateData : PriorityJobChannel.IJob
        {
            public uint Priority => (uint)JobPriorities.High;
            public CancellationToken Token;
        }

        internal class BufferState
        {
            public TimeSpan Duration;
            public TimeSpan BufferedPeriod;
            public int BufferedSize;

            public TimeSpan ClockIn;
            public TimeSpan ClockOut;
            public StreamType StreamType;

            public BufferState Clone() => (BufferState)MemberwiseClone();

            public override string ToString() =>
                $"{StreamType}: D[{BufferedPeriod}/{Duration}] S[{BufferedSize / 1000} kB] C[{ClockIn}/{ClockOut}]";
        }

        private class StateHolder
        {
            public BufferState CurrentState;
            public TimeSpan EosClock;

            // ISO/IEC 23009-1 5.3.1.2 Table 3
            // minimum buffer period before playback start assuring smooth content delivery
            public TimeSpan MinBufferTime = TimeSpan.Zero;

            public void Reset()
            {
                CurrentState.BufferedPeriod = TimeSpan.Zero;
                CurrentState.BufferedSize = 0;
                CurrentState.ClockIn = ClockProviderConfig.InvalidClock;
                CurrentState.ClockOut = ClockProviderConfig.InvalidClock;

                EosClock = ClockProviderConfig.InvalidClock;
            }

            public override string ToString() => $"{CurrentState} Eos[{EosClock}]";
        }

        private readonly StateHolder[] stateHolder = new StateHolder[(int)StreamType.Count];
        private readonly BufferState[] bufferReports = new BufferState[(int)StreamType.Count];
        private readonly PriorityJobChannel jobChannel = new PriorityJobChannel((uint)JobPriorities.Count);
        private readonly Dictionary<PacketStreamClock, DateTimeOffset> observedClocks = new Dictionary<PacketStreamClock, DateTimeOffset>();
        private DataEvent allowedEvents = DataEvent.None;
        private readonly SynchronizationContext syncContext;
        private CancellationTokenSource periodicUpdateCts;
        private readonly AsyncAutoResetEvent updateNow = new AsyncAutoResetEvent();

        private void OnPacketIn(PacketInData data)
        {
            if (!data.IsEos && data.Size == 0)
                return;

            ref var packet = ref data.Packet;
            var streamState = stateHolder[(int)packet.Stream].CurrentState;
            if (data.IsEos)
            {
                stateHolder[(int)packet.Stream].EosClock = streamState.ClockIn;
                Logger.Info($"{packet.Stream}: EOS clock {streamState.ClockIn}");
                return;
            }

            if (observedClocks.TryGetValue(data.Packet, out var seenWhen))
            {
                Logger.Info($"{packet.Stream}: Duplicate packet {packet.Clock} {DateTimeOffset.Now - seenWhen} ago");
                return;
            }

            var clockDiff = packet.Clock - streamState.ClockIn;

            if (clockDiff > StreamClockDiscontinuityThreshold || clockDiff < TimeSpan.Zero)
            {
                Logger.Info($"{packet.Stream}: Clock Discontinuity {clockDiff} {packet.Clock}/{streamState.ClockIn}");
                clockDiff = TimeSpan.Zero;
            }

            observedClocks.Add(data.Packet, DateTimeOffset.Now);
            streamState.BufferedPeriod += clockDiff;
            streamState.BufferedSize += data.Size;
            streamState.ClockIn = packet.Clock;
        }

        private void OnPacketOut(PacketOutData data)
        {
            ref var packet = ref data.Packet;

            if (!observedClocks.ContainsKey(data.Packet))
            {
                Logger.Info($"{packet.Stream}: Packet {packet.Clock} not found");
                return;
            }

            var streamState = stateHolder[(int)packet.Stream].CurrentState;
            var clockDiff = packet.Clock - streamState.ClockOut;

            if (clockDiff > StreamClockDiscontinuityThreshold || clockDiff < TimeSpan.Zero)
            {
                Logger.Info($"{packet.Stream}: Clock Discontinuity {clockDiff} {packet.Clock}/{streamState.ClockOut}");
                clockDiff = TimeSpan.Zero;
            }

            observedClocks.Remove(data.Packet);
            streamState.BufferedPeriod -= clockDiff;
            streamState.BufferedSize -= data.Size;
            streamState.ClockOut = packet.Clock;
        }

        private void OnReset(ResetData data)
        {
            Stop();

            foreach (var stream in stateHolder)
                stream?.Reset();

            observedClocks.Clear();

            Start();
            data.Notifier.SetResult(null);

            Logger.Info("");
        }

        private void OnPeriodicUpdate(PeriodicUpdateData data)
        {
#pragma warning disable 4014 //background running periodic update
            PeriodicUpdate(data.Token);
#pragma warning restore 4014
        }

        private void OnSetBufferConfiguration(BufferConfigurationData data)
        {
            var state = stateHolder[(int)data.Stream];
            state.CurrentState.Duration = data.Duration;
            state.MinBufferTime = data.MinBufferTime;
        }

        private void OnSetAllowedEvents(AllowedEventsData data)
        {
            allowedEvents = data.Events;
            updateNow.Set();
            Logger.Info($"Allowed Events set {allowedEvents}");
        }

        private void RegisterJobHandlers()
        {
            jobChannel.RegisterJobHandler<PacketInData>(OnPacketIn);
            jobChannel.RegisterJobHandler<PacketOutData>(OnPacketOut);
            jobChannel.RegisterJobHandler<ResetData>(OnReset);
            jobChannel.RegisterJobHandler<BufferConfigurationData>(OnSetBufferConfiguration);
            jobChannel.RegisterJobHandler<AllowedEventsData>(OnSetAllowedEvents);
            jobChannel.RegisterJobHandler<PeriodicUpdateData>(OnPeriodicUpdate);
        }
    }
}
