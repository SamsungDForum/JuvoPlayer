/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2021, Samsung Electronics Co., Ltd
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers.FFmpeg;
using Nito.AsyncEx;

namespace JuvoPlayer.FFmpeg
{
    public class FFmpegStreamHandler
    {
        private FFmpegDemuxer _demuxer;
        private Dictionary<int, Queue<Packet>> _pendingPackets;
        private Dictionary<int, TaskCompletionSource<Packet>> _pendingGetPacketTasks;
        private IList<StreamConfig> _streamConfigs;
        private Task _nextPacketTask;
        private AsyncManualResetEvent _allStreamsEnabled;

        public int TotalNumberOfStreams { get; set; }

        public FFmpegStreamHandler(FFmpegDemuxer demuxer)
        {
            _demuxer = demuxer;
            _pendingPackets = new Dictionary<int, Queue<Packet>>();
            _pendingGetPacketTasks = new Dictionary<int, TaskCompletionSource<Packet>>();
            _streamConfigs = new List<StreamConfig>();
            _allStreamsEnabled = new AsyncManualResetEvent();
        }

        public async Task EnableStream(
            StreamConfig streamConfig,
            TimeSpan position)
        {
            if (_streamConfigs.Contains(streamConfig))
                return;
            _streamConfigs.Add(streamConfig);
            var index = streamConfig.GetIndex();
            if (!_pendingPackets.ContainsKey(index))
                _pendingPackets[index] = new Queue<Packet>();
            var numberOfEnabledStreams = _streamConfigs.Count;
            if (numberOfEnabledStreams == TotalNumberOfStreams)
            {
                await _demuxer.EnableStreams(_streamConfigs);
                await Seek(position);
                _allStreamsEnabled.Set();
            }
        }

        public async Task DisableStream(StreamConfig streamConfig)
        {
            if (!_streamConfigs.Contains(streamConfig))
                return;
            _streamConfigs.Remove(streamConfig);
            var index = streamConfig.GetIndex();
            var pendingPackets = _pendingPackets[index];
            foreach (var packet in pendingPackets)
                packet.Dispose();
            pendingPackets.Clear();
            var numberOfEnabledStreams = _streamConfigs.Count;
            if (numberOfEnabledStreams == 0)
            {
                await _demuxer.EnableStreams(_streamConfigs);
                _allStreamsEnabled.Reset();
            }
        }

        private Task Seek(TimeSpan position)
        {
            return _demuxer.Seek(
                position,
                CancellationToken.None);
        }

        public async Task<Packet> GetPacket(
            StreamConfig streamConfig,
            CancellationToken token)
        {
            await _allStreamsEnabled.WaitAsync(token);
            token.ThrowIfCancellationRequested();
            var index = streamConfig.GetIndex();
            var pendingPackets = _pendingPackets[index];
            if (pendingPackets.Count > 0)
                return pendingPackets.Dequeue();
            await _allStreamsEnabled.WaitAsync(token);
            var tcs = new TaskCompletionSource<Packet>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingGetPacketTasks[index] = tcs;
            EnsureNextPacketLoopRunning(token);
            return await tcs.Task;
        }

        private async void EnsureNextPacketLoopRunning(CancellationToken token)
        {
            if (_nextPacketTask != null)
                return;

            try
            {
                _nextPacketTask = RunNextPacketLoop(token);
                await _nextPacketTask;
            }
            finally
            {
                _nextPacketTask = null;
            }
        }

        private async Task RunNextPacketLoop(CancellationToken token)
        {
            Packet packet = null;
            try
            {
                while (!token.IsCancellationRequested &&
                       _pendingGetPacketTasks.Count > 0)
                {
                    packet = await _demuxer.NextPacket()
                        .WaitAsync(token);
                    token.ThrowIfCancellationRequested();
                    if (packet != null)
                    {
                        HandleDataPacket(packet);
                        packet = null;
                    }
                    else
                    {
                        HandleEosPacket();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                CancelPendingGetPacketTasks();
            }
            catch (Exception e)
            {
                Log.Error(e);
                SetExceptionOnFirstPendingGetPacketTask(e);
                CancelPendingGetPacketTasks();
            }
            finally
            {
                packet?.Dispose();
            }
        }

        private void HandleDataPacket(Packet packet)
        {
            var storage = (FFmpegDataStorage) packet.Storage;
            var index = storage
                .Packet
                .stream_index;
            HandlePacket(
                index,
                packet);
        }

        private void HandleEosPacket()
        {
            foreach (var streamConfig in _streamConfigs)
            {
                var index = streamConfig.GetIndex();
                var streamType = streamConfig.StreamType();
                var eosPacket = new EosPacket(streamType);
                HandlePacket(
                    index,
                    eosPacket);
            }
        }

        private void HandlePacket(
            int index,
            Packet packet)
        {
            if (_pendingGetPacketTasks.ContainsKey(index))
            {
                var tcs = _pendingGetPacketTasks[index];
                _pendingGetPacketTasks.Remove(index);
                tcs.SetResult(packet);
            }
            else if (_streamConfigs.Any(streamConfig => streamConfig.GetIndex() == index))
            {
                _pendingPackets[index].Enqueue(packet);
            }
            else
            {
                packet.Dispose();
            }
        }

        private void SetExceptionOnFirstPendingGetPacketTask(Exception e)
        {
            if (_pendingGetPacketTasks.Count <= 0) return;
            var keyValuePair = _pendingGetPacketTasks.First();
            var key = keyValuePair.Key;
            var getPacketTask = keyValuePair.Value;
            _pendingGetPacketTasks.Remove(key);
            getPacketTask.SetException(e);
        }

        private void CancelPendingGetPacketTasks()
        {
            foreach (var getPacketTask in _pendingGetPacketTasks.Values)
                getPacketTask.SetCanceled();
            _pendingGetPacketTasks.Clear();
        }

        public void Dispose()
        {
            foreach (var packets in _pendingPackets.Values)
            {
                while (packets.Count > 0)
                {
                    packets
                        .Dequeue()
                        .Dispose();
                }
            }
        }

        public async Task<TimeSpan> GetAdjustedSeekPosition(
            StreamConfig streamConfig,
            TimeSpan position)
        {
            await _demuxer.EnableStreams(new List<StreamConfig> { streamConfig });
            await _demuxer.Seek(
                position,
                CancellationToken.None);
            var packet = await _demuxer.NextPacket();
            if (packet == null)
                return position;
            var pts = packet.Pts;
            packet.Dispose();
            return pts;
        }
    }
}