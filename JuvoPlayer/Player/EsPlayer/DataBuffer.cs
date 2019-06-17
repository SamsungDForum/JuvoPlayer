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
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common;
using static Configuration.DataBufferConfig;

namespace JuvoPlayer.Player.EsPlayer
{
    internal interface IDataBuffer
    {
        TimeSpan Duration();
        double FillLevel();
    }

    internal class DataBuffer : IDataBuffer
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private long maxBufferDuration;
        private long bufferAvailableTicks;
        private long eosTicks;
        private long lastTicksIn;
        private long lastTicksOut;

        private volatile bool reportFull;

        private long GetAvailableBufferTicks() =>
            reportFull ? 0 : Volatile.Read(ref bufferAvailableTicks);
        
        private long GetLastTicksIn() =>
            Volatile.Read(ref lastTicksIn);

        private long GetLastTicksOut() =>
            Volatile.Read(ref lastTicksOut);

        private long GetMaxBufferDuration() =>
            Volatile.Read(ref maxBufferDuration);

        private long GetEosTicks() =>
            Volatile.Read(ref eosTicks);

        public StreamType StreamType { get; }
        private MetaDataStreamConfig streamMetaData;

        public DataBuffer(StreamType streamType)
        {
            StreamType = streamType;

            UpdateBufferDuration(TimeBufferDepthDefault);

            Interlocked.Exchange(ref bufferAvailableTicks, maxBufferDuration);
        }

        public DataRequest GetDataRequest()
        {
            var ticks = GetAvailableBufferTicks();
            var ticksIn = GetLastTicksIn();
            var ticksOut = GetLastTicksOut();
            var eos = GetEosTicks();

            var duration = ticks > 0 ? TimeSpan.FromTicks(ticks) : TimeSpan.Zero;
            
            return new DataRequest
            {
                Duration = duration,
                StreamType = StreamType,
                IsBufferEmpty = (ticksIn == ticksOut && eos != ticksOut)
            };
        }

        public TimeSpan Duration()
        {
            var maxBuffer = GetMaxBufferDuration();
            var availTicks = GetAvailableBufferTicks();
            var durationTicks = maxBuffer - availTicks;
            return TimeSpan.FromTicks(durationTicks);
        }

        public double FillLevel()
        {
            var maxBuffer = GetMaxBufferDuration();
            var availTicks = GetAvailableBufferTicks();
            var fillValue = (((maxBuffer - availTicks)
                              / (double)maxBuffer)) * 100;

            return Math.Round(fillValue, 2);
        }

        public bool UpdateBufferConfiguration(MetaDataStreamConfig newStreamConfig)
        {
            if (newStreamConfig == streamMetaData)
                return false;

            streamMetaData = newStreamConfig;

            var previousMax = GetMaxBufferDuration();

            UpdateBufferDuration(newStreamConfig.BufferDuration);

            var durationDiff = maxBufferDuration - previousMax;

            Interlocked.Add(ref bufferAvailableTicks, durationDiff);

            return true;
        }

        private void UpdateBufferDuration(TimeSpan duration)
        {
            Interlocked.Exchange(ref maxBufferDuration, duration.Ticks);

            logger.Info($"{StreamType}: Buffer Size {duration}");
        }

        public void Reset()
        {
            logger.Info($"{StreamType}: {TimeSpan.FromTicks(maxBufferDuration)}");

            Interlocked.Exchange(ref bufferAvailableTicks, maxBufferDuration);
            Interlocked.Exchange(ref lastTicksIn, long.MaxValue);
            Interlocked.Exchange(ref lastTicksOut, long.MaxValue);
            Interlocked.Exchange(ref eosTicks, -1);
        }

        public void ReportFullBuffer()
        {
            logger.Info($"{StreamType}:");

            reportFull = true;
        }

        public void ReportActualBuffer()
        {
            logger.Info($"{StreamType}:");

            reportFull = false;
        }

        private void MarkEosDts(long clockTicks)
        {
            Interlocked.Exchange(ref eosTicks, clockTicks);
            logger.Info($"{StreamType}: EOS Dts set to {TimeSpan.FromTicks(clockTicks)}");
        }

        public void DataIn(Packet packet)
        {
            var lastTicks = lastTicksIn;

            if (!packet.ContainsData())
            {
                if (packet is EOSPacket)
                    MarkEosDts(lastTicks);

                return;
            }

            var currentTicks = packet.Dts.Ticks;

            if (lastTicks > currentTicks)
            {
                if( lastTicks != long.MaxValue)
                    logger.Warn($"{StreamType}: Clock Mismatch! Current {packet.Dts} Last {TimeSpan.FromTicks(lastTicks)}. Stale data received?");

                lastTicksIn = currentTicks;
                return;
            }

            var duration = currentTicks - lastTicks;
            Interlocked.Add(ref bufferAvailableTicks, -duration);
            Interlocked.Exchange(ref lastTicksIn, currentTicks);

        }

        public void DataOut(Packet packet)
        {
            if (!packet.ContainsData())
                return;

            var currentTicks = packet.Dts.Ticks;
            var lastTicks = lastTicksOut;
            
            if (lastTicks > currentTicks)
            {
                if( lastTicks != long.MaxValue)
                    logger.Warn($"{StreamType}: Clock Mismatch! Current {packet.Dts} Last {TimeSpan.FromTicks(lastTicks)}. Stale data received?");

                lastTicksOut = currentTicks;
                return;
            }

            var duration = currentTicks - lastTicks;
            Interlocked.Add(ref bufferAvailableTicks, duration);
            Interlocked.Exchange(ref lastTicksOut, currentTicks);
            
        }
    }
}
