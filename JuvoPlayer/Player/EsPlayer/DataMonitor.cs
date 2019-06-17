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
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using static JuvoPlayer.Player.EsPlayer.DataEvents;
using static Configuration.DataMonitorConfig;

namespace JuvoPlayer.Player.EsPlayer
{
    internal static class DataEvents
    {
        [Flags]
        public enum DataEvent
        {
            None = 0,
            Buffering = 1,
            DataRequest = 2,
            All = Buffering | DataRequest
        }
    }

    // TODO: Extend class functionality to provide:
    // TODO:    - Prebuffering on stream start (initial) to accomodate
    // TODO:      MPD's minBufferTime if present. minBufferTime = minimum buffer period
    // TODO:      before playback start assuring smooth content delivery as defined in
    // TODO:      ISO/IEC 23009-1 5.3.1.2 Table 3

    internal class DataMonitor : IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly DataBuffer[] dataBuffers = new DataBuffer[(int)StreamType.Count];
        
        private readonly Subject<bool> bufferingSubject = new Subject<bool>();
        private readonly Subject<DataRequest> bufferConfigurationSubject = new Subject<DataRequest>();
        private readonly Subject<DataRequest> publishSubject = new Subject<DataRequest>();

        public DataSynchronizer DataSynchronizer { get; }

        private volatile DataEvent allowedEventsFlag = DataEvent.All;

        private readonly IDisposable playerStateSubscription;
        private volatile PlayerState playerState;
        private readonly PlayerClock playerClock = new PlayerClock();

        private IObservable<(List<DataRequest> DataRequests, bool? BufferingNeeded)> NotificationSource() =>
            playerClock
                .TickSource()
                .Sample(DataStatePublishInterval)
                .Select(_ => GetNotifications(DataEvent.All));

        private IObservable<DataRequest> DataNotificationSource() =>
            NotificationSource()
                .Where(request => request.DataRequests != null)
                .SelectMany(request => request.DataRequests);

        private IObservable<bool> BufferingNotificationSource() =>
            NotificationSource()
                .Where(request => request.BufferingNeeded.HasValue)
                .Select(request => request.BufferingNeeded.Value);

        public IObservable<bool> BufferingStateChanged() =>
            BufferingNotificationSource()
                .Merge(bufferingSubject)
                .StartWith(true)
                .DistinctUntilChanged()
                .Skip(1)
                .Where(_ =>
                {
                    var currentState = playerState;
                    return currentState == PlayerState.Playing ||
                           currentState == PlayerState.Paused;
                });

        public IObservable<DataRequest> DataNeededStateChanged() =>
            DataNotificationSource()
                .Merge(publishSubject)
                .Merge(bufferConfigurationSubject);

        public IDataBuffer GetDataBuffer(StreamType stream) =>
            dataBuffers[(int)stream];

        public async Task PublishState()
        {
            var (dataRequests, bufferingNeeded) = GetNotifications(DataEvent.All);

            if (dataRequests != null)
            {
                foreach (var request in dataRequests)
                {
                    publishSubject.OnNext(request);
                }
            }

            if (bufferingNeeded.HasValue)
                bufferingSubject.OnNext(bufferingNeeded.Value);

            await Task.Yield();
        }

        public void Initialize(StreamType stream)
        {
            logger.Info(stream.ToString());

            var index = (int)stream;

            if (dataBuffers[index] != null)
                throw new ArgumentException($"Stream buffer {stream} already initialized");

            dataBuffers[index] = new DataBuffer(stream);
            DataSynchronizer.Initialize(stream);
        }

        public DataMonitor(IObservable<PlayerState> playerStateSource)
        {
            playerStateSubscription =
                playerStateSource.Subscribe(state => playerState = state, SynchronizationContext.Current);

            DataSynchronizer = new DataSynchronizer();
        }

        public async Task SeekInitialize()
        {
            logger.Info("");

            AllowedEvents(DataEvent.DataRequest);
            ReportFullBuffer();
            await PublishState();
            AllowedEvents(DataEvent.None);
        }

        public async Task SeekStart(TimeSpan clock)
        {
            logger.Info("");
            
            DataSynchronizer.SeekStartSynchronization(clock);
            
            ReportActualBuffer();
            AllowedEvents(DataEvent.DataRequest);
            await PublishState();
        }

        public void SeekComplete()
        {
            logger.Info("");

            AllowedEvents(DataEvent.All);
        }

        public void DataIn(Packet packet)
        {
            var index = (int)packet.StreamType;

            dataBuffers[index].DataIn(packet);
        }

        public void DataOut(Packet packet)
        {
            dataBuffers[(int)packet.StreamType].DataOut(packet);
            DataSynchronizer.UpdateClock(packet);
        }

        private (List<DataRequest> DataRequests, bool? BufferingNeeded) GetNotifications(DataEvent pushRequest, StreamType forStream = StreamType.Count)
        {
            var result = new List<DataRequest>();
            var currentFlags = allowedEventsFlag;

            logger.Debug(currentFlags + " " + (forStream == StreamType.Count ? "All Streams" : forStream.ToString()));

            if (currentFlags == DataEvent.None)
                return (null, null);

            var bufferingNeeded = false;
            foreach (var buffer in dataBuffers)
            {
                if (buffer == null || (forStream != StreamType.Count && buffer.StreamType != forStream))
                    continue;

                var request = buffer.GetDataRequest();

                if (DataEvent.DataRequest == (currentFlags & pushRequest & DataEvent.DataRequest))
                {
                    logger.Debug(request.ToString());
                    result.Add(request);
                }

                bufferingNeeded |= request.IsBufferEmpty;
            }

            bool? bufferResult = null;
            if (DataEvent.Buffering == (currentFlags & pushRequest & DataEvent.Buffering))
                bufferResult = bufferingNeeded;

            return (result, bufferResult);
        }

        public void AllowedEvents(DataEvent events) =>
            allowedEventsFlag = events;

        public void ReportFullBuffer()
        {
            foreach (var buffer in dataBuffers)
                buffer?.ReportFullBuffer();
        }

        public void ReportActualBuffer()
        {
            foreach (var buffer in dataBuffers)
                buffer?.ReportActualBuffer();
        }

        public void SetMetaDataConfiguration(MetaDataStreamConfig config)
        {
            if (!dataBuffers[(int) config.Stream].UpdateBufferConfiguration(config))
                return;

            var dataRequests = GetNotifications(DataEvent.DataRequest, config.Stream).DataRequests;
            if (dataRequests == null)
                return;

            foreach (var request in dataRequests)
                bufferConfigurationSubject.OnNext(request);
        }

        public void Reset()
        {
            logger.Info("");

            foreach (var buffer in dataBuffers)
                buffer?.Reset();

            DataSynchronizer.Reset();
        }

        public void Dispose()
        {
            logger.Info("");

            playerStateSubscription.Dispose();
            bufferConfigurationSubject.Dispose();
            bufferingSubject.Dispose();
            publishSubject.Dispose();
            DataSynchronizer.Dispose();
            playerClock.Dispose();
        }
    }
}
