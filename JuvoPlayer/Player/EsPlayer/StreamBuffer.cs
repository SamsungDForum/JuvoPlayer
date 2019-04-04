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
using System.Diagnostics;
using System.Reactive;
using static Configuration.StreamBuffer;

namespace JuvoPlayer.Player.EsPlayer
{
    class StreamBuffer:IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private TimeSpan maxBufferDuration;
        private TimeSpan dataOnLevel;
        private TimeSpan dataOffLevel;
        private TimeSpan bufferOnLevel;
        private TimeSpan bufferOffLevel;

        public TimeSpan CurrentBufferSize => TimeSpan.FromTicks(GetCurrentBufferDurationTicks());
        public int BufferFill => (int) (((double) Interlocked.Read(ref currentBufferDurationTicks) / maxBufferDuration.Ticks) * 100);
        
        private readonly StreamType streamType;

        private readonly Subject<long> dataInSubject = new Subject<long>();
        private readonly Subject<long> dataOutSubject = new Subject<long>();
        private readonly BehaviorSubject<DataArgs> bufferUpdateSubject;

        private long currentBufferDurationTicks;
        
        private TimeSpan? lastDtsIn;
        private TimeSpan? lastDtsOut;
        private TimeSpan? eosDts;

        private long GetCurrentBufferDurationTicks() =>
            Interlocked.Read(ref currentBufferDurationTicks);

        private bool IsBufferEmpty(TimeSpan currentBuffer) =>
            currentBuffer >= maxBufferDuration;

        public IObservable<DataArgs> DataState() =>
            bufferUpdateSubject
                .Merge(filteredDataInOut())
                .AsObservable();

        private IObservable<DataArgs> filteredDataInOut() =>
            filteredDataIn()
                .Merge(filteredDataOut())
                .Sample(TimeSpan.FromSeconds(1))
                .Select(currentBufferTicks =>
                {
                    var bufferSize = maxBufferDuration - TimeSpan.FromTicks(currentBufferTicks);
                    return new DataArgs
                    {
                        DurationRequired = bufferSize,
                        StreamType = streamType,
                        BufferEmpty = IsBufferEmpty(bufferSize)
                    };
                });

        private IObservable<long> filteredDataIn() =>
            dataInSubject
                .Where(currentBuffer => currentBuffer >= dataOffLevel.Ticks)
                .DistinctUntilChanged();

        private IObservable<long> filteredDataOut() =>
            dataOutSubject
                .Where(currentBuffer => currentBuffer <= dataOnLevel.Ticks)
                .DistinctUntilChanged();
        
        public StreamBuffer(StreamType streamType)
        {
            this.streamType = streamType;

            UpdateBufferDuration(TimeBufferDepthDefault);

            bufferUpdateSubject = new BehaviorSubject<DataArgs>(new DataArgs
            {
                DurationRequired = maxBufferDuration,
                BufferEmpty = false,
                StreamType = streamType
            });
        }

        public void UpdateBufferConfiguration(MetaDataStreamConfig newStreamConfig)
        {
            UpdateBufferDuration(newStreamConfig.BufferDuration);

            var bufferSpace = maxBufferDuration - CurrentBufferSize;

            bufferUpdateSubject.OnNext(new DataArgs
            {
                DurationRequired = bufferSpace,
                BufferEmpty = IsBufferEmpty(bufferSpace),
                StreamType = streamType
            });   
        }
        
        private void UpdateBufferDuration(TimeSpan duration)
        {
            maxBufferDuration = duration;
            dataOnLevel = TimeSpan.FromSeconds(duration.TotalSeconds * DataOnLevel);
            dataOffLevel = TimeSpan.FromSeconds(duration.TotalSeconds * DataOffLevel);
            bufferOnLevel = TimeSpan.FromSeconds(duration.TotalSeconds * BufferOnLevel);
            bufferOffLevel = TimeSpan.FromSeconds(duration.TotalSeconds * BufferOffLevel);

            logger.Info($"Size={maxBufferDuration} DataOn={dataOnLevel} DataOff={dataOffLevel} BufferOn={bufferOnLevel} BufferOff={bufferOffLevel}");
        }

        public void Reset()
        {
            logger.Info($"{streamType}");

            currentBufferDurationTicks = 0;

            lastDtsIn = null;
            lastDtsOut = null;

            bufferUpdateSubject.OnNext(new DataArgs
            {
                DurationRequired = maxBufferDuration,
                BufferEmpty = false,
                StreamType = streamType
            });
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
            
            var duration = packet.Dts - lastDtsIn;
            if (duration < TimeSpan.Zero)
            {
                logger.Warn($"DTS Mismatch! lastDTS {lastDtsIn} PacketDTS {packet.Dts}. Presentation changed?");
                return;
            }

            if (duration == TimeSpan.Zero)
                return;

            lastDtsIn = packet.Dts;

            var bufferTicks = Interlocked.Add(ref currentBufferDurationTicks, duration.Value.Ticks);

            dataInSubject.OnNext(bufferTicks);            
        }

        public void DataOut(Packet packet)
        {
            if (!packet.ContainsData())
                return;

            if (!lastDtsOut.HasValue)
            {
                lastDtsOut = packet.Dts;
                return;
            }

            var duration = packet.Dts - lastDtsOut;
            if (duration < TimeSpan.Zero)
            {
                logger.Warn($"DTS Mismatch! lastDTS {lastDtsIn} PacketDTS {packet.Dts}. Presentation changed?");
                return;
            }

            if (duration == TimeSpan.Zero)
                return;

            lastDtsOut = packet.Dts;

            var bufferTicks = Interlocked.Add(ref currentBufferDurationTicks, duration.Value.Negate().Ticks);
            
            // If "out" packet DTS matches EOS DTS, don't process On levels
            // to prevent buffer event generation on end of stream.
            if (lastDtsOut == eosDts)
                return;

            dataOutSubject.OnNext(bufferTicks);
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
