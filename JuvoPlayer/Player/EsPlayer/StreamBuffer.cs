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


namespace JuvoPlayer.Player.EsPlayer
{
    internal class StreamBuffer:IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private TimeSpan maxBufferDuration;
        private long bufferAvailableTicks;

        private readonly StreamType streamType;

        private readonly Subject<long> dataInSubject = new Subject<long>();
        private readonly Subject<long> dataOutSubject = new Subject<long>();
        private readonly Subject<long> bufferUpdateSubject = new Subject<long>();
        
        private TimeSpan? lastDtsIn;
        private TimeSpan? lastDtsOut;
        private TimeSpan? eosDts;

        private volatile uint sequenceId;
        private volatile bool reportFull;

        public string BufferTimeRange => lastDtsOut  + "-" + lastDtsIn;
        private long GetAvailableBufferTicks() =>
            Interlocked.Read(ref bufferAvailableTicks);

        public IObservable<DataArgs> DataState() =>
            FilteredDataIn()
                .Merge(FilteredDataOut())
                .TakeLast(1)
                .Merge(bufferUpdateSubject)
                .Select(currentBufferTicks => new DataArgs
                {
                    SequenceId = sequenceId,
                    DurationRequired = reportFull?
                        TimeSpan.Zero:
                        TimeSpan.FromTicks(currentBufferTicks),
                    StreamType = streamType,
                });

        private IObservable<long> FilteredDataIn() =>
            dataInSubject
                .Throttle(TimeSpan.FromMilliseconds(250))
                .Timeout(TimeSpan.FromSeconds(1), OnDataTimeout());


        private IObservable<long> FilteredDataOut() =>
            dataOutSubject
                .Throttle(TimeSpan.FromMilliseconds(250))
                .Timeout(TimeSpan.FromSeconds(1), OnDataTimeout());


        public StreamBuffer(StreamType streamType, uint id)
        {
            this.streamType = streamType;
            sequenceId = id;

            UpdateBufferDuration(TimeSpan.FromSeconds(10));

            Interlocked.Exchange(ref bufferAvailableTicks, maxBufferDuration.Ticks);
        }

        private IObservable<long> OnDataTimeout()
        {
            var ticks = GetAvailableBufferTicks();
            
            return Observable.Return(ticks);
        }

        public TimeSpan CurrentBufferedDuration()
        {
            var durationTicks = maxBufferDuration.Ticks - GetAvailableBufferTicks();
            return TimeSpan.FromTicks(durationTicks);
        }

        public double BufferFill()
        {
            var fillValue = (((maxBufferDuration.Ticks - GetAvailableBufferTicks())
                              / (double) maxBufferDuration.Ticks)) * 100;

            return Math.Round(fillValue,2);
        }

        public void UpdateBufferConfiguration(MetaDataStreamConfig newStreamConfig)
        {
            var previousMax = maxBufferDuration;

            UpdateBufferDuration(newStreamConfig.BufferDuration);

            var durationDiff =  maxBufferDuration - previousMax;

            var diff = Interlocked.Add(ref bufferAvailableTicks, durationDiff.Ticks);
            
            bufferUpdateSubject.OnNext(diff);   
            
        }
        private void UpdateBufferDuration(TimeSpan duration)
        {
            maxBufferDuration = duration;

            logger.Info($"{streamType}: Buffer Size {maxBufferDuration}");
        }

        public void Reset(uint id)
        {
            logger.Info($"{streamType}: {id}");

            Interlocked.Exchange(ref bufferAvailableTicks, maxBufferDuration.Ticks);

            // EOS is not reset. Intentional.
            lastDtsIn = null;
            lastDtsOut = null;

            sequenceId = id;

            bufferUpdateSubject.OnNext(maxBufferDuration.Ticks);
        }

        public void Update()
        {
            logger.Info($"{streamType}:");

            bufferUpdateSubject.OnNext(GetAvailableBufferTicks());
        }

        public void ReportFullBuffer()
        {
            logger.Info($"{streamType}:");

            reportFull = true;
        }

        public void ReportActualBuffer()
        {
            logger.Info($"{streamType}:");

            reportFull = false;
        }

        public void DataIn(Packet packet)
        {
            if (!packet.ContainsData())
            {
                if (packet is EOSPacket)
                    eosDts = lastDtsIn;
                
                return;
            }

            if (!lastDtsIn.HasValue)
            {
                lastDtsIn = packet.Dts;
                return;
            }
            
            var duration = packet.Dts - lastDtsIn.Value;
            if (duration < TimeSpan.Zero)
            {
                logger.Warn($"{streamType}: DTS Mismatch! last DTS In {lastDtsIn} PacketDTS {packet.Dts}. Presentation changed?");
                return;
            }
            
            lastDtsIn = packet.Dts;

            var bufferTicks = Interlocked.Add(ref bufferAvailableTicks, duration.Negate().Ticks);

            dataInSubject.OnNext(bufferTicks);            
        }

        public bool DataOut(Packet packet)
        {
            if (!packet.ContainsData())
                return false;

            if (!lastDtsOut.HasValue)
            {
                lastDtsOut = packet.Dts;
                return false;
            }

            var duration = packet.Dts - lastDtsOut.Value;
            if (duration < TimeSpan.Zero)
            {
                logger.Warn($"{streamType}: DTS Mismatch! last DTS Out {lastDtsOut} PacketDTS {packet.Dts}. Presentation changed?");
                return false;
            }

            lastDtsOut = packet.Dts;

            var bufferTicks = Interlocked.Add(ref bufferAvailableTicks, duration.Ticks);

            dataOutSubject.OnNext(bufferTicks);

            var bufferEmpty = bufferTicks >= maxBufferDuration.Ticks;

            // If "out" packet DTS matches EOS DTS, don't process On levels
            // to prevent buffer event generation on end of stream.
            return lastDtsOut < eosDts && bufferEmpty;
        }

        public void Dispose()
        {
            logger.Info($"{streamType}");

            dataInSubject.Dispose();
            dataOutSubject.Dispose();
            bufferUpdateSubject.Dispose();   
        }
    }
}
