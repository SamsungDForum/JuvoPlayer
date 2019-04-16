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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Player.EsPlayer.Stream.Buffering;
using static Configuration.StreamSynchronizerConfig;

namespace JuvoPlayer.Player.EsPlayer.Stream.Synchronization
{
    internal class StreamSynchronizer
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly StreamBuffer[] streamBuffers;
        private volatile StreamSynchronizationBarrier[] streamSyncTraps = new StreamSynchronizationBarrier[(int)StreamType.Count];

        private readonly TimeSpan[] streamDurationInPlayer = new TimeSpan[(int)StreamType.Count];
        private readonly TimeSpan[] lastSynchronizedPlayerClock = new TimeSpan[(int)StreamType.Count];
        public delegate TimeSpan PlayerClockDelegate();

        public PlayerClockDelegate PlayerClock { get; set; } = null;

        public StreamSynchronizer(StreamBuffer[] buffers)
        {
            streamBuffers = buffers;
            for (var i = 0; i < streamDurationInPlayer.Length; i++)
            {
                switch (i)
                {
                    case (int)StreamType.Audio:
                        streamDurationInPlayer[i] = AudioStreamDurationInPlayer;
                        break;
                    case (int)StreamType.Video:
                        streamDurationInPlayer[i] = VideoStreamDurationInPlayer;
                        break;
                    default:
                        streamDurationInPlayer[i] = DefaultStreamDurationInPlayer;
                        break;
                }
            }
        }

        private IEnumerable<StreamType> GetSyncStreams(StreamType stream, TimeSpan streamClock) =>
            streamBuffers
                .Where(buffer => buffer != null &&
                                 buffer.StreamType != stream &&
                                 streamClock - buffer.StreamClockOut > MaxStreamDifference)
                .Select(buffer => buffer.StreamType);

        public bool IsStreamSyncNeeded(StreamType stream)
        {
            var streamClock = streamBuffers[(int)stream].StreamClockOut;

            return GetSyncStreams(stream, streamClock).Any();
        }

        private string DumpStreams(IEnumerable<StreamType> source) =>
            source.Aggregate("", ((s, type) => s + type + " " + streamBuffers[(int)type].StreamClockOut + " "));

        public async Task SynchronizeStreams(StreamType stream, CancellationToken token)
        {
            var streamClock = streamBuffers[(int)stream].StreamClockOut;

            var syncWithStreams = GetSyncStreams(stream, streamClock);

            if (!syncWithStreams.Any())
                return;

            try
            {
                var syncTrap = new StreamSynchronizationBarrier(streamClock, syncWithStreams.Count());
                streamSyncTraps[(int)stream] = syncTrap;
                logger.Info($"{stream}: {streamClock} Sync with {DumpStreams(syncWithStreams)}");
                await syncTrap.WaitForSynchronization(token);
            }
            finally
            {
                streamSyncTraps[(int)stream] = null;
            }
        }

        public void UpdateSynchronizationTraps(StreamType stream)
        {
            var streamClock = streamBuffers[(int)stream].StreamClockOut;

            foreach (var trap in streamSyncTraps)
                trap?.UpdateSynchronization(stream, streamClock);
        }

        public async Task SynchronizePlayerClock(StreamType stream, CancellationToken token)
        {
            var playerClock = PlayerClock();
            var streamClock = streamBuffers[(int)stream].StreamClockOut;
            var delay = streamClock - playerClock - streamDurationInPlayer[(int)stream];

            if (delay <= TimeSpan.Zero)
                return;

            lastSynchronizedPlayerClock[(int)stream] = playerClock;
            logger.Info($"{stream}: {streamClock} Sync with player {playerClock} Delay {delay}");

            try
            {
                await Task.Delay(delay, token);
            }
            catch (TaskCanceledException)
            {
                throw new OperationCanceledException();
            }

            logger.Info($"{stream}: {streamClock} Sync with player done {PlayerClock()}");
        }

        public bool IsPlayerClockSyncNeeded(StreamType stream)
        {
            var playerClock = PlayerClock();

            if (lastSynchronizedPlayerClock[(int)stream] == playerClock)
                return false;

            var streamClock = streamBuffers[(int)stream].StreamClockOut;
            var clockDiff = streamClock - playerClock;
            return clockDiff > streamDurationInPlayer[(int)stream] + PlayerClockSyncNeededOverhead;
        }
    }
}
