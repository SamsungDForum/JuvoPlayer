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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers.FFmpeg.Interop;
using Nito.AsyncEx;
using static Configuration.StreamSynchronizerConfig;
using StreamBuffer = JuvoPlayer.Player.EsPlayer.Stream.Buffering.StreamBuffer;

namespace JuvoPlayer.Player.EsPlayer.Stream.Synchronization
{


    internal class StreamSynchronizer
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly StreamBuffer[] streamBuffers;
        private readonly StreamBufferSize[] streamSizes;
        private readonly TimeSpan[] maxDurationInPlayer = new TimeSpan[(int)StreamType.Count];
        private readonly int[] maxSizeInPlayer = new int[(int)StreamType.Count];

        private readonly Func<TimeSpan> getPlayerClock;
        private readonly TimeSpan[] lastSynchronizedPlayerClock = new TimeSpan[(int)StreamType.Count];
        private readonly AsyncManualResetEvent[] bufferSizeFull = new AsyncManualResetEvent[(int)StreamType.Count];
        

        public StreamSynchronizer(StreamBuffer[] buffers, StreamBufferSize[] sizes, Func<TimeSpan> timeGetter)
        {
            streamBuffers = buffers;
            getPlayerClock = timeGetter;
            streamSizes = sizes;
        }

        public void Initialize(StreamType stream)
        {
            switch (stream)
            {
                case StreamType.Audio:
                    maxDurationInPlayer[(int)StreamType.Audio] = AudioStreamDurationInPlayer;
                    maxSizeInPlayer[(int)StreamType.Audio] = AudioStreamSizeInPlayer;
                    break;
                case StreamType.Video:
                    maxDurationInPlayer[(int)StreamType.Video] = VideoStreamDurationInPlayer;
                    maxSizeInPlayer[(int)StreamType.Video] = VideoStreamSizeInPlayer;
                    break;
                default:
                    maxDurationInPlayer[(int)stream] = DefaultStreamDurationInPlayer;
                    maxSizeInPlayer[(int)stream] = DefaultStreamSizeInPlayer;
                    break;
            }

            bufferSizeFull[(int)stream] = new AsyncManualResetEvent(false);
        }

        public void StorePacketSize(Packet packet)
        {

        }

        public void Reset(StreamType stream)
        {
            lastSynchronizedPlayerClock[(int) stream] = getPlayerClock();
            logger.Info($"{stream} Last player clock set to {lastSynchronizedPlayerClock[(int) stream]}");
        }

        private (TimeSpan streamClock, TimeSpan playerClock, TimeSpan delay) GetSynchronizationParameters(StreamType stream)
        {
            var streamClock = streamBuffers[(int)stream].StreamClockOut;
            var playerClock = this.getPlayerClock();
            var delay = streamClock - playerClock - maxDurationInPlayer[(int)stream];

            return (streamClock, playerClock, delay);
        }

        public bool IsPlayerClockSynchronized(StreamType stream)
        {
            var syncData = GetSynchronizationParameters(stream);

            if (syncData.delay < MinimumStreamClockPlayerClockDifference)
                return false;

            return true;
        }

        /*
        public Task PlayerClockSynchronization(StreamType stream, CancellationToken token)
        {
            var syncParams = GetSynchronizationParameters(stream);

            if (syncParams.delay <= TimeSpan.Zero)
                return Task.CompletedTask;

            // Clock may report stale time information.
            // Expected delay is ~0.5 sec. If large, then its stale.
            var delay = syncParams.delay > PlayerClockSyncNeededOverhead
                ? PlayerClockSyncNeededOverhead
                : syncParams.delay;

            delay = syncParams.delay;

            logger.Info($"{stream}: {syncParams.streamClock} Sync with player {syncParams.playerClock} Delay {delay} Stream Clock {syncParams.streamClock}");

            lastSynchronizedPlayerClock[(int) stream] = syncParams.playerClock;

            return Task.Delay(delay, token);

        }

        private Task SynchronizeStreams(StreamType stream, CancellationToken token)
        {

        }
        */

        public Task Synchronize(StreamType stream, CancellationToken token)
        {
            var syncParams = GetSynchronizationParameters(stream);

            var delay = TimeSpan.Zero;
            
            if (syncParams.playerClock == lastSynchronizedPlayerClock[(int) stream] ||
                syncParams.streamClock == TimeSpan.Zero)
            {
                var otherStreams = streamBuffers
                    .Where(buffer => buffer != null &&
                                     buffer.StreamClockOut - syncParams.streamClock > TimeSpan.FromSeconds(1))
                    .OrderBy(buffer => buffer.StreamClockOut)
                    .ToList();

                if (otherStreams.Count == 0)
                    return Task.CompletedTask;

                var otherStreamClock = otherStreams[0].StreamClockOut;
                delay = otherStreamClock - syncParams.streamClock - TimeSpan.FromSeconds(1);
                logger.Info($"{stream}: {syncParams.streamClock} Sync with stream {otherStreams[0].StreamType} Clock {otherStreamClock} Delay {delay}");

            }
            else
            {
                if (syncParams.delay < MinimumStreamClockPlayerClockDifference)
                    return Task.CompletedTask;

                delay = syncParams.delay > MaximumStreamClockPlayerClockDifference
                    ? MaximumStreamClockPlayerClockDifference
                    : syncParams.delay;

                logger.Info($"{stream}: {syncParams.streamClock} Sync with player {syncParams.playerClock} Delay {delay} Stream Clock {syncParams.streamClock}");

            }

            lastSynchronizedPlayerClock[(int) stream] = syncParams.playerClock;

            return Task.Delay(delay, token);
        }

        public async Task SynchronizeWithSize(StreamType stream, CancellationToken token)
        {
            var index = (int) stream;
            var sizeInPlayer = streamSizes[index].GetSize;

            //logger.Info($"{stream} {sizeInPlayer} {maxSizeInPlayer[index]}");
            if (sizeInPlayer < maxSizeInPlayer[index])
                return;

            logger.Info($"{stream}: {sizeInPlayer/1024} kB exceeds {maxSizeInPlayer[index]/1024} kB {streamBuffers[index].StreamClockOut}");
            bufferSizeFull[index].Reset();
            await bufferSizeFull[index].WaitAsync(token);
            
                logger.Info($"{stream}: Sync Reached");
            
            return;
        }

        public void UpdateSizeSynchronization()
        {
            foreach (var stream in streamSizes)
            {
                if (stream == null)
                    continue;

                var index = (int)stream.StreamType;
                var currentSize = streamSizes[index].GetSize;
                if(currentSize < maxSizeInPlayer[index] && !bufferSizeFull[index].IsSet)
                    bufferSizeFull[index].Set();
                    

            }
        }
    }
}
