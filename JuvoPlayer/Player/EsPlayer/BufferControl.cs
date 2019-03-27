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

namespace JuvoPlayer.Player.EsPlayer
{
    class BufferControl:IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        
        private const double DataOn = 0.2;
        private const double DataOff = 0.8;
        private const double BufferOn = 0;
        private const double BufferOff = 0.1;

        private TimeSpan maxBufferDuration = TimeSpan.FromSeconds(10);
        private TimeSpan dataOnLevel = TimeSpan.FromSeconds(2);
        private TimeSpan dataOffLevel = TimeSpan.FromSeconds(8);
        private TimeSpan bufferOnLevel = TimeSpan.Zero;
        private TimeSpan bufferOffLevel = TimeSpan.FromSeconds(1);

        public TimeSpan CurrentBufferSize => TimeSpan.FromTicks(Interlocked.Read(ref currentBufferDuration));
        public int BufferFill => (int) (((double) Interlocked.Read(ref currentBufferDuration) / maxBufferDuration.Ticks) * 100);
        
        private readonly StreamType streamType;

        /*
        private readonly Subject<DataArgs> dataOnSubject = new Subject<DataArgs>();
        private readonly Subject<DataArgs> dataOffSubject = new Subject<DataArgs>();
        private readonly Subject<DataArgs> bufferOnSubject = new Subject<DataArgs>();
        private readonly Subject<DataArgs> bufferOffSubject = new Subject<DataArgs>();
        */

        private readonly Subject<DataArgs> dataSubject = new Subject<DataArgs>();
        private readonly Subject<DataArgs> bufferSubject = new Subject<DataArgs>();
        

        public IObservable<DataArgs> BufferState => bufferSubject.AsObservable();
        public IObservable<DataArgs> DataState => dataSubject.AsObservable();

        private bool isBufferingNeeded = false;
        
        private bool isDataNeeded = true;

        private long currentBufferDuration;

        private TimeSpan? lastDtsIn;
        private TimeSpan? lastDtsOut;
        private TimeSpan? eosDts;

        private Task subjectNotifier = Task.CompletedTask;

        public BufferControl(StreamType streamType)
        {
            this.streamType = streamType;

            UpdateBufferDuration(maxBufferDuration);

        }

        public void UpdateBufferConfiguration(MetaDataStreamConfig newStreamConfig)
        {
            UpdateBufferDuration(newStreamConfig.BufferDuration);
        }
        
        private void UpdateBufferDuration(TimeSpan duration)
        {
            maxBufferDuration = duration;
            dataOnLevel = TimeSpan.FromSeconds(duration.TotalSeconds * DataOn);
            dataOffLevel = TimeSpan.FromSeconds(duration.TotalSeconds * DataOff);
            bufferOnLevel = TimeSpan.FromSeconds(duration.TotalSeconds * BufferOn);
            bufferOffLevel = TimeSpan.FromSeconds(duration.TotalSeconds * BufferOff);

            logger.Info($"Size={maxBufferDuration} DataOn={dataOnLevel} DataOff={dataOffLevel} BufferOn={bufferOnLevel} BufferOff={bufferOffLevel}");
        }

        public void Reset()
        {
            isBufferingNeeded = false;
            isDataNeeded = true;

            currentBufferDuration = 0;

            lastDtsIn = null;
            lastDtsOut = null;
            eosDts = null;
        }

        public void DataIn(Packet packet)
        {
            if (!packet.ContainsData())
            {
                if (packet is EOSPacket)
                {
                    eosDts = lastDtsIn;
                }

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
            if(lastDtsOut != eosDts)
                ProcessOnLevels(bufferTicks);
        }

        private void ProcessOffLevels(long currentBuffer)
        { 
            if (currentBuffer >= bufferOffLevel.Ticks)
            {
                SetBufferState(false,currentBuffer);
            }

            if (currentBuffer >= dataOffLevel.Ticks)
            {    
                SetDataState(false,currentBuffer);
            }
        }

        private void ProcessOnLevels(long currentBuffer)
        { 
            if (currentBuffer <= dataOnLevel.Ticks)
            {
                SetDataState(true,currentBuffer);
            }
        
            if (currentBuffer <= bufferOnLevel.Ticks)
            {   
                SetBufferState(true,currentBuffer);
            }   
        }

        private void SetBufferState(bool flag, long currentBuffer)
        {
            if (flag == isBufferingNeeded)
                return;

            isBufferingNeeded = flag;

            subjectNotifier.ContinueWith(_ =>
            {
                bufferSubject.OnNext(new DataArgs {StreamType = streamType, DataFlag = isBufferingNeeded});
                logger.Info($"{streamType}: Buffer {isBufferingNeeded} {TimeSpan.FromTicks(currentBuffer)}");
            });
        }

        private void SetDataState(bool flag, long currentBuffer)
        {
            if (flag == isDataNeeded)
            {
                return;
            }

            isDataNeeded = flag;

            subjectNotifier.ContinueWith(_ =>
            {
                dataSubject.OnNext(new DataArgs {StreamType = streamType, DataFlag = isDataNeeded});
                logger.Info($"{streamType}: Data {isDataNeeded} {TimeSpan.FromTicks(currentBuffer)}");
            });
        }

        public void Dispose()
        {
            dataSubject.Dispose();
            dataSubject.Dispose();
        }
    }
}
