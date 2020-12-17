/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
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
using Nito.AsyncEx;

namespace JuvoPlayer.Demuxers
{
    public class DemuxerController : IDemuxerController
    {
        private readonly Subject<TimeSpan> clipDurationSubject = new Subject<TimeSpan>();
        private readonly IDemuxer demuxer;
        private readonly Subject<string> demuxerErrorSubject = new Subject<string>();
        private readonly Subject<DrmInitData> drmInitDataSubject = new Subject<DrmInitData>();
        private readonly Subject<Packet> packetReadySubject = new Subject<Packet>();
        private readonly Subject<StreamConfig> streamConfigSubject = new Subject<StreamConfig>();

        private CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
        private IObservable<byte[]> dataSource;
        private IDisposable dataSourceSub;
        private bool isDisposed;
        private ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private bool paused;

        public DemuxerController(IDemuxer demuxer)
        {
            this.demuxer = demuxer;
        }

        public void StartForUrl(string url)
        {
            demuxer.InitForUrl(url)
                .ContinueWith(OnDemuxerInitialized,
                    cancelTokenSource.Token,
                    TaskContinuationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext());

            ListenForEos();
        }

        private void ListenForEos()
        {
            demuxer.Completion?.ContinueWith(OnEos,
                cancelTokenSource.Token,
                TaskContinuationOptions.None,
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void StartForEs()
        {
            demuxer.InitForEs()
                .ContinueWith(OnDemuxerInitialized,
                    cancelTokenSource.Token,
                    TaskContinuationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void Reset()
        {
            paused = false;
            demuxer.Reset();
            CancelContinuations();
            ResetDataSourceSubscription();
        }

        public async Task Flush()
        {
            var token = cancelTokenSource.Token;

            // Make sure that all pending chunks are delivered before calling Complete
            await Task.Yield();
            token.ThrowIfCancellationRequested();

            demuxer.Complete();
            await demuxer.Completion.WaitAsync(token);
            token.ThrowIfCancellationRequested();

            demuxer.Reset();
        }

        public void Pause()
        {
            paused = true;
        }

        public void Resume()
        {
            if (!paused)
                return;
            paused = false;
            ScheduleNextPacketToDemux();
        }

        public void SetDataSource(IObservable<byte[]> ds)
        {
            dataSource = ds;
            ResetDataSourceSubscription();
        }

        public IObservable<StreamConfig> StreamConfigReady()
        {
            return streamConfigSubject.AsObservable();
        }

        public IObservable<TimeSpan> ClipDurationFound()
        {
            return clipDurationSubject.AsObservable();
        }

        public IObservable<DrmInitData> DrmInitDataFound()
        {
            return drmInitDataSubject.AsObservable();
        }

        public IObservable<Packet> PacketReady()
        {
            return packetReadySubject.AsObservable();
        }

        public IObservable<string> DemuxerError()
        {
            return demuxerErrorSubject.AsObservable();
        }

        public async Task<TimeSpan> Seek(TimeSpan time, CancellationToken token)
        {
            CancelContinuations();
            var seekTime = await demuxer.Seek(time, token);
            ListenForEos();
            ScheduleNextPacketToDemux();
            return seekTime;
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            dataSourceSub?.Dispose();
            demuxer?.Dispose();

            streamConfigSubject.Dispose();
            clipDurationSubject.Dispose();
            drmInitDataSubject.Dispose();
            packetReadySubject.Dispose();
            demuxerErrorSubject.Dispose();

            isDisposed = true;
        }

        private void OnDemuxerInitialized(Task<ClipConfiguration> initTask)
        {
            if (initTask.Status == TaskStatus.RanToCompletion)
            {
                PublishClipConfig(initTask.Result);
                ScheduleNextPacketToDemux();
                return;
            }

            MaybePublishError(initTask);
        }

        private void PublishClipConfig(ClipConfiguration configuration)
        {
            if (configuration.Duration > TimeSpan.Zero)
                clipDurationSubject.OnNext(configuration.Duration);

            foreach (var streamConfig in configuration.StreamConfigs ?? new List<StreamConfig>())
                streamConfigSubject.OnNext(streamConfig);

            foreach (var drmInitData in configuration.DrmInitDatas ?? new List<DrmInitData>())
                drmInitDataSubject.OnNext(drmInitData);
        }

        private void ScheduleNextPacketToDemux()
        {
            if (demuxer.IsInitialized() && !paused)
                demuxer.NextPacket()
                    .ContinueWith(OnPacketReady,
                        cancelTokenSource.Token,
                        TaskContinuationOptions.None,
                        TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnPacketReady(Task<Packet> packetTask)
        {
            if (packetTask.Status == TaskStatus.RanToCompletion && packetTask.Result != null)
            {
                packetReadySubject.OnNext(packetTask.Result);
                ScheduleNextPacketToDemux();
                return;
            }

            MaybePublishError(packetTask);
        }

        private void MaybePublishError(Task task)
        {
            if (task.IsFaulted)
                demuxerErrorSubject.OnNext(task.Exception.Message);
        }

        private void CancelContinuations()
        {
            cancelTokenSource.Cancel();
            cancelTokenSource = new CancellationTokenSource();
        }

        private void ResetDataSourceSubscription()
        {
            dataSourceSub?.Dispose();
            if (dataSource != null)
                dataSourceSub =
                    dataSource.Subscribe(OnChunkReady, SynchronizationContext.Current);
        }

        private void OnChunkReady(byte[] chunk)
        {
            if (chunk == null)
            {
                OnDataSourceCompleted();
                return;
            }
            demuxer.PushChunk(chunk);
        }

        private void OnDataSourceCompleted()
        {
            demuxer.Completion?.ContinueWith(OnEos, TaskScheduler.FromCurrentSynchronizationContext());
            demuxer.Complete();
        }

        private void OnEos(Task _)
        {
            demuxer.Reset();
            packetReadySubject.OnNext(null);
        }
    }
}
