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
using System.Threading.Tasks;
using Nito.AsyncEx;
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

        public TimeSpan CurrentBufferSize => TimeSpan.FromTicks(Interlocked.Read(ref currentBufferDuration));
        public int BufferFill => (int) (((double) Interlocked.Read(ref currentBufferDuration) / maxBufferDuration.Ticks) * 100);
        
        private readonly StreamType streamType;

        private readonly Subject<bool> dataOnSubject = new Subject<bool>();
        private readonly Subject<bool> dataOffSubject = new Subject<bool>();
        private readonly BehaviorSubject<DataArgs> dataSourceSubject;
        private readonly Subject<bool> bufferOnSubject = new Subject<bool>();
        private readonly Subject<bool> bufferOffSubject = new Subject<bool>();

        public IObservable<bool> BufferState =>
            bufferOnSubject
                .Merge(bufferOffSubject)
                .DistinctUntilChanged()
                .AsObservable();

        public IObservable<DataArgs> DataState =>
            dataSourceSubject
                .Merge(ConvertedDataSource)
                .AsObservable();

        private IObservable<DataArgs> ConvertedDataSource =>
            dataOffSubject
                .Merge(dataOnSubject)
                .DistinctUntilChanged()
                .Select<bool, DataArgs>(flag => new DataArgs
                {
                    DurationRequired = flag ? maxBufferDuration - CurrentBufferSize : TimeSpan.Zero,
                    StreamType = streamType
                });
        
        private long currentBufferDuration;

        private TimeSpan? lastDtsIn;
        private TimeSpan? lastDtsOut;
        private TimeSpan? eosDts;

        public StreamBuffer(StreamType streamType)
        {
            this.streamType = streamType;

            dataSourceSubject = new BehaviorSubject<DataArgs>(new DataArgs
            {
                DurationRequired = TimeSpan.Zero,
                StreamType = streamType
            });

            UpdateBufferDuration(TimeBufferDepthDefault);
        }

        public void UpdateBufferConfiguration(MetaDataStreamConfig newStreamConfig)
        {
            UpdateBufferDuration(newStreamConfig.BufferDuration);
            dataSourceSubject.OnNext(new DataArgs
            {
                DurationRequired = maxBufferDuration - CurrentBufferSize,
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

            currentBufferDuration = 0;

            lastDtsIn = null;
            lastDtsOut = null;
            eosDts = null;

            dataSourceSubject.OnNext(new DataArgs
            {
                DurationRequired = maxBufferDuration,
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
            Debug.Assert(duration >= TimeSpan.Zero);

            lastDtsIn = packet.Dts;

            var bufferTicks = Interlocked.Add(ref currentBufferDuration, duration.Value.Ticks);

            ProcessOffLevels(bufferTicks);
            
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
            Debug.Assert(duration >= TimeSpan.Zero);

            lastDtsOut = packet.Dts;

            var bufferTicks = Interlocked.Add(ref currentBufferDuration, duration.Value.Negate().Ticks);
            
            // If "out" packet DTS matches EOS DTS, don't process On levels
            // to prevent buffer event generation on end of stream.
            if(lastDtsOut != eosDts)
                ProcessOnLevels(bufferTicks);
        }

        private void ProcessOffLevels(long currentBuffer)
        { 
            if (currentBuffer >= bufferOffLevel.Ticks)
                bufferOffSubject.OnNext(false);
            
            if (currentBuffer >= dataOffLevel.Ticks)
                dataOffSubject.OnNext(false);
        }

        private void ProcessOnLevels(long currentBuffer)
        { 
            if (currentBuffer <= dataOnLevel.Ticks)
                dataOnSubject.OnNext(true);
            
            if (currentBuffer <= bufferOnLevel.Ticks)
                bufferOnSubject.OnNext(true);
        }

        public void Dispose()
        {
            logger.Info($"{streamType}");

            dataSourceSubject.Dispose();
            dataOffSubject.Dispose();
            dataOnSubject.Dispose();
            bufferOffSubject.Dispose();
            bufferOnSubject.Dispose();
            
        }
    }
}
