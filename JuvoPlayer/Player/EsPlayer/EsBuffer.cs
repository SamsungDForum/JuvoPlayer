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
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using Configuration;
using static Configuration.DataBufferConfig;

namespace JuvoPlayer.Player.EsPlayer
{
    internal partial class EsBuffer : IDisposable
    {
        [Flags]
        public enum DataEvent
        {
            None = 0,
            DataBuffering = 1,
            DataRequest = 2,
            All = DataBuffering | DataRequest
        }

        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private delegate void DataRequestDelegate(DataRequest state);
        private event DataRequestDelegate DataRequestEvent;
        private event DataRequestDelegate DataRequestInjectorEvent;
        private IObservable<DataRequest> dataRequestSource;
        private IObservable<DataRequest> dataRequestInjectorSource;

        private delegate void BufferingRequestDelegate(bool buffering);
        private event BufferingRequestDelegate BufferingRequestEvent;
        private event BufferingRequestDelegate BufferingRequestInjectorEvent;
        private IObservable<bool> bufferingRequestSource;
        private IObservable<bool> bufferingRequestInjectorSource;

        private CompositeDisposable disposables;
        private readonly Lazy<EsBuffer> dataBufferLazy;

        public IObservable<DataRequest> DataRequestObservable() => dataBufferLazy.Value
            .dataRequestSource
            .Merge(dataRequestInjectorSource);

        public IObservable<bool> BufferingRequestObservable() => dataBufferLazy.Value
            .bufferingRequestSource
            .DistinctUntilChanged()
            .Merge(bufferingRequestInjectorSource);

        public EsBuffer()
        {
            syncContext = SynchronizationContext.Current;
            dataBufferLazy = new Lazy<EsBuffer>(LazyInit);
        }

        private EsBuffer LazyInit()
        {
            RegisterJobHandlers();
            Start();

            dataRequestInjectorSource =
                Observable.FromEvent<DataRequestDelegate, DataRequest>(h => DataRequestInjectorEvent += h, h => DataRequestInjectorEvent -= h);
            dataRequestSource =
                Observable.FromEvent<DataRequestDelegate, DataRequest>(h => DataRequestEvent += h, h => DataRequestEvent -= h);

            bufferingRequestInjectorSource =
                Observable.FromEvent<BufferingRequestDelegate, bool>(h => BufferingRequestInjectorEvent += h, h => BufferingRequestInjectorEvent -= h);
            bufferingRequestSource =
                Observable.FromEvent<BufferingRequestDelegate, bool>(h => BufferingRequestEvent += h, h => BufferingRequestEvent -= h);

            disposables = new CompositeDisposable
            {
                dataRequestInjectorSource.Subscribe()
            };

            return this;
        }

        public void Initialize(StreamType stream)
        {
            dataBufferLazy.Value.stateHolder[(int)stream] = new StateHolder
            {
                CurrentState = new BufferState
                {
                    StreamType = stream,
                    Duration = TimeBufferDepthDefault
                },
                EosClock = ClockProviderConfig.InvalidClock
            };
            stateHolder[(int)stream].Reset();
            bufferReports[(int)stream] = stateHolder[(int)stream].CurrentState.Clone();

            Logger.Info($"{stream}");
        }

        public void RequestBuffering(bool onOff) =>
            BufferingRequestInjectorEvent?.Invoke(onOff);

        public void SetAllowedEvents(DataEvent events) =>
            jobChannel.EnqueueJob(new AllowedEventsData { Events = events });

        public BufferState GetStreamStateReport(StreamType stream) =>
            bufferReports[(int)stream];

        public void SendBufferFullDataRequest(StreamType stream) =>
            DataRequestInjectorEvent?.Invoke(new DataRequest
            {
                MaxBufferDuration = bufferReports[(int)stream].Duration,
                RequestPeriod = TimeSpan.Zero,
                StreamType = stream
            });

        public void UpdateBufferConfiguration(MetaDataStreamConfig streamConfig) =>
            jobChannel.EnqueueJob(new BufferConfigurationData
            {
                Duration = streamConfig.BufferDuration,
                MinBufferTime = streamConfig.MinBufferTime ?? TimeSpan.Zero,
                Stream = streamConfig.StreamType()
            });

        public void DataIn(Packet packet) =>
            jobChannel.EnqueueJob(new PacketInData
            {
                IsEos = packet is EOSPacket,
                Size = packet.Storage?.Length ?? 0,
                Packet = new PacketStreamClock { Clock = packet.Dts, Stream = packet.StreamType }
            });

        public void DataOut(Packet packet)
        {
            if (!packet.ContainsData())
                return;

            jobChannel.EnqueueJob(new PacketOutData
            {
                Size = packet.Storage.Length,
                Packet = new PacketStreamClock { Clock = packet.Dts, Stream = packet.StreamType }
            });
        }

        public Task Reset()
        {
            var request = new ResetData { Notifier = new TaskCompletionSource<object>() };
            jobChannel.EnqueueJob(request);

            Logger.Info($"");

            return request.Notifier.Task;
        }

        private static TimeSpan GetAvailableDuration(BufferState bufferState)
        {
            var maxDuration = bufferState.Duration;
            var duration = maxDuration - bufferState.BufferedPeriod;

            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;
            else if (duration > maxDuration)
                duration = maxDuration;

            return duration;
        }

        private static DataRequest GetDataRequest(BufferState state) => new DataRequest
        {
            StreamType = state.StreamType,
            RequestPeriod = GetAvailableDuration(state),
            MaxBufferDuration = state.Duration
        };

        private void CopyStreamReport(BufferState bufferReport, CancellationToken token) =>
            syncContext.Post(args =>
            {
                var (cancelToken, newState) = (Tuple<CancellationToken, BufferState>)args;
                if (cancelToken.IsCancellationRequested)
                    return;

                bufferReports[(int)newState.StreamType] = newState;

            }, Tuple.Create(token, bufferReport.Clone()));

        private static bool IsEventAllowed(DataEvent requestedEvent, DataEvent allowedEvents) =>
            requestedEvent == (allowedEvents & requestedEvent);

        private async Task NextUpdate(CancellationToken token)
        {
            try
            {
                using (var timeOut = new CancellationTokenSource(DataStatePublishInterval))
                using (var combo = CancellationTokenSource.CreateLinkedTokenSource(timeOut.Token, token))
                    await updateNow.WaitAsync(combo.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {// Do not care which token caused exception. Caller will exit on input token cancellation.
            }
        }

        private async Task PeriodicUpdate(CancellationToken token)
        {
            Logger.Info("Started");

            while (!token.IsCancellationRequested)
            {
                // Use same version of events flag for entire update.
                var currentEventsFlag = allowedEvents;
                var dataEventsAllowed = IsEventAllowed(DataEvent.DataRequest, currentEventsFlag);
                var bufferingNeeded = false;

                foreach (var stateEntry in stateHolder)
                {
                    if (stateEntry == null)
                        continue;

                    var streamState = stateEntry.CurrentState;

                    CopyStreamReport(streamState, token);

                    // Notifications are no longer required if EOS clock fits within current buffer duration
                    if (stateEntry.EosClock != ClockProviderConfig.InvalidClock &&
                        stateEntry.EosClock - streamState.ClockOut <= streamState.Duration)
                        continue;

                    // Do not generate buffering events before first packets arrive.
                    bufferingNeeded |= streamState.BufferedPeriod <= TimeSpan.Zero &&
                                       streamState.ClockIn != ClockProviderConfig.InvalidClock;

                    // Latency is introduced when collecting data through single thread.
                    // Generate data request when below threshold to reduce buffer overflows
                    if (dataEventsAllowed && streamState.BufferedPeriod < DataRequestOutputThreshold)
                        DataRequestEvent?.Invoke(GetDataRequest(streamState));
                }

                if (IsEventAllowed(DataEvent.DataBuffering, currentEventsFlag))
                    BufferingRequestEvent?.Invoke(bufferingNeeded);

                await NextUpdate(token);
            }

            Logger.Info("Completed");
        }

        private void Start()
        {
            if (jobChannel.IsRunning())
                return;

            periodicUpdateCts?.Dispose();
            periodicUpdateCts = new CancellationTokenSource();

            jobChannel.Start().Wait(periodicUpdateCts.Token);
            jobChannel.EnqueueJob(new PeriodicUpdateData { Token = periodicUpdateCts.Token });
        }

        private void Stop()
        {
            if (!jobChannel.IsRunning())
                return;

            periodicUpdateCts.Cancel();
            jobChannel.Stop();
        }

        public void Dispose()
        {
            jobChannel.Stop();
            periodicUpdateCts?.Cancel();
            periodicUpdateCts?.Dispose();
            disposables?.Dispose();

            Logger.Info("");
        }
    }
}
