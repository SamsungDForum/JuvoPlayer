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
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common;
using static Configuration.StreamBuffer;

namespace JuvoPlayer.Player.EsPlayer.Stream.Buffering
{
    internal class StreamBuffer : IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private TimeSpan maxBufferDuration;
        private long bufferAvailableTicks;

        private readonly Subject<long> bufferUpdateSubject = new Subject<long>();

        private TimeSpan? eosDts;

        private volatile uint sequenceId;
        private volatile bool reportFull;

        private long GetAvailableBufferTicks() =>
            Interlocked.Read(ref bufferAvailableTicks);

        private readonly IObservable<long> dataStateSource;

        public IObservable<DataArgs> DataState() =>
            dataStateSource
                .Merge(bufferUpdateSubject)
                .Select(currentBufferTicks => new DataArgs
                {
                    SequenceId = sequenceId,
                    DurationRequired = reportFull ?
                        TimeSpan.Zero :
                        TimeSpan.FromTicks(currentBufferTicks),
                    StreamType = StreamType,
                });

        public TimeSpan StreamClockIn { get; private set; }
        public TimeSpan StreamClockOut { get; private set; }
        public static TimeSpan GetStreamClock(Packet packet) => packet.Dts;
        public string BufferTimeRange => StreamClockOut + "-" + StreamClockIn;
        public StreamType StreamType { get; }

        public StreamBuffer(StreamType streamType, uint id)
        {
            StreamType = streamType;
            sequenceId = id;

            UpdateBufferDuration(TimeBufferDepthDefault);

            Interlocked.Exchange(ref bufferAvailableTicks, maxBufferDuration.Ticks);

            dataStateSource = Observable
                .Interval(DataStateEventGenerationInterval)
                .Select(l => GetAvailableBufferTicks());
        }

        public TimeSpan CurrentBufferedDuration()
        {
            var durationTicks = maxBufferDuration.Ticks - GetAvailableBufferTicks();
            return TimeSpan.FromTicks(durationTicks);
        }

        public double BufferFill()
        {
            var fillValue = (((maxBufferDuration.Ticks - GetAvailableBufferTicks())
                              / (double)maxBufferDuration.Ticks)) * 100;

            return Math.Round(fillValue, 2);
        }

        public void UpdateBufferConfiguration(MetaDataStreamConfig newStreamConfig)
        {
            var previousMax = maxBufferDuration;

            UpdateBufferDuration(newStreamConfig.BufferDuration);

            var durationDiff = maxBufferDuration - previousMax;

            var diff = Interlocked.Add(ref bufferAvailableTicks, durationDiff.Ticks);

            bufferUpdateSubject.OnNext(diff);

        }
        private void UpdateBufferDuration(TimeSpan duration)
        {
            maxBufferDuration = duration;

            logger.Info($"{StreamType}: Buffer Size {maxBufferDuration}");
        }

        public void Reset(uint id)
        {
            logger.Info($"{StreamType}: {id}");

            Interlocked.Exchange(ref bufferAvailableTicks, maxBufferDuration.Ticks);

            // EOS is not reset. Intentional.
            StreamClockIn = TimeSpan.Zero;
            StreamClockOut = TimeSpan.Zero;

            sequenceId = id;

            bufferUpdateSubject.OnNext(maxBufferDuration.Ticks);
        }

        public void PublishBufferState()
        {
            logger.Info($"{StreamType}:");

            bufferUpdateSubject.OnNext(GetAvailableBufferTicks());
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

        public void DataIn(TimeSpan currentClock)
        {
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

        public bool DataOut(TimeSpan currentClock)
        {
            if (StreamClockOut == TimeSpan.Zero)
            {
                StreamClockOut = currentClock;
                return false;
            }

            if (StreamClockOut > currentClock)
            {
                logger.Warn($"{StreamType}: DTS Mismatch! Last clock {StreamClockOut} Stream clock {currentClock}. Presentation changed?");
                StreamClockOut = currentClock;
                return false;
            }

            var duration = currentClock - StreamClockOut;

            StreamClockOut = currentClock;

            var bufferTicks = Interlocked.Add(ref bufferAvailableTicks, duration.Ticks);

            var bufferEmpty = bufferTicks >= maxBufferDuration.Ticks;

            // If "out" packet DTS matches EOS DTS, don't process On levels
            // to prevent buffer event generation on end of stream.
            return StreamClockOut < eosDts && bufferEmpty;
        }

        public void Dispose()
        {
            logger.Info($"{StreamType}");

            bufferUpdateSubject.Dispose();
        }
    }
}
