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
        string BufferTimeRange { get; }

        TimeSpan CurrentBufferedDuration();
        double BufferFill();
    }

    internal class DataBuffer : IDataBuffer
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private long maxBufferDuration;
        private long bufferAvailableTicks;
        private TimeSpan? eosDts;

        private volatile bool reportFull;

        private long GetAvailableBufferTicks() =>
            reportFull ? 0 : Interlocked.Read(ref bufferAvailableTicks);

        public TimeSpan StreamClockIn { get; private set; }
        public TimeSpan StreamClockOut { get; private set; }

        public string BufferTimeRange => StreamClockOut + "-" + StreamClockIn;
        public StreamType StreamType { get; }

        public DataBuffer(StreamType streamType)
        {
            StreamType = streamType;

            UpdateBufferDuration(TimeBufferDepthDefault);

            Interlocked.Exchange(ref bufferAvailableTicks, maxBufferDuration);
        }

        public DataRequest GetDataRequest()
        {
            var ticks = GetAvailableBufferTicks();
            var duration = ticks > 0 ? TimeSpan.FromTicks(ticks) : TimeSpan.Zero;
            return new DataRequest
            {
                Duration = duration,
                StreamType = StreamType,
                IsBufferEmpty = (StreamClockIn == StreamClockOut && eosDts != StreamClockOut)
            };
        }

        public TimeSpan CurrentBufferedDuration()
        {
            var durationTicks = maxBufferDuration - GetAvailableBufferTicks();
            return TimeSpan.FromTicks(durationTicks);
        }

        public double BufferFill()
        {
            var maxBuffer = Interlocked.Read(ref maxBufferDuration);
            var fillValue = (((maxBuffer - GetAvailableBufferTicks())
                              / (double)maxBuffer)) * 100;

            return Math.Round(fillValue, 2);
        }

        public void UpdateBufferConfiguration(MetaDataStreamConfig newStreamConfig)
        {
            var previousMax = Interlocked.Read(ref maxBufferDuration);

            UpdateBufferDuration(newStreamConfig.BufferDuration);

            var durationDiff = maxBufferDuration - previousMax;

            Interlocked.Add(ref bufferAvailableTicks, durationDiff);
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

            // EOS is not reset. Intentional.
            StreamClockIn = TimeSpan.Zero;
            StreamClockOut = TimeSpan.Zero;
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

        public void MarkEosDts()
        {
            eosDts = StreamClockIn;
            logger.Info($"{StreamType}: EOS Dts set to {eosDts}");
        }

        public void DataIn(Packet packet)
        {
            var currentClock = packet.Dts;
            if (StreamClockIn == TimeSpan.Zero)
            {
                StreamClockIn = currentClock;
                return;
            }

            if (StreamClockIn > currentClock)
            {
                logger.Warn($"{StreamType}: Clock Mismatch! Last clock {StreamClockIn} Stream clock {currentClock}. Presentation changed?");
                StreamClockIn = currentClock;
                return;
            }

            var duration = currentClock - StreamClockIn;

            StreamClockIn = currentClock;

            Interlocked.Add(ref bufferAvailableTicks, duration.Negate().Ticks);
        }

        public void DataOut(Packet packet)
        {
            var currentClock = packet.Dts;
            if (StreamClockOut == TimeSpan.Zero)
            {
                StreamClockOut = currentClock;
                return;
            }

            if (StreamClockOut > currentClock)
            {
                logger.Warn($"{StreamType}: DTS Mismatch! Last clock {StreamClockOut} Stream clock {currentClock}. Presentation changed?");
                StreamClockOut = currentClock;
                return;
            }

            var duration = currentClock - StreamClockOut;

            StreamClockOut = currentClock;

            Interlocked.Add(ref bufferAvailableTicks, duration.Ticks);
        }
    }
}
