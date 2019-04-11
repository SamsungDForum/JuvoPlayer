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
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.Player.EsPlayer
{

    // TODO: Extend class functionality to provide:
    // TODO:    - Stream Synchronization
    // TODO:    - Prebuffering on stream start (initial) to accomodate
    // TODO:      MPD's minBufferTime if present. minBufferTime = minimum buffer period
    // TODO:      before playback start assuring smooth content delivery as defined in
    // TODO:      ISO/IEC 23009-1 5.3.1.2 Table 3

    internal class StreamBufferController : IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly StreamBuffer[] streamBuffers = new StreamBuffer[(int)StreamType.Count];
        private readonly MetaDataStreamConfig[] metaDataConfigs = new MetaDataStreamConfig[(int)StreamType.Count];
        private volatile bool[] streamBufferingFilter = new bool[(int)StreamType.Count];

        private readonly Subject<Unit> bufferingOnSubject = new Subject<Unit>();
        private readonly Subject<Unit> bufferingOffSubject = new Subject<Unit>();
        private volatile bool eventsEnabled = true;
        
        private volatile bool isBuffering;

        private volatile uint sequenceId = 1;


        public IObservable<bool> BufferingStateChanged() =>
            bufferOnSource()
                .Merge(bufferOffSource())
                .Do(state =>
                {
                    isBuffering = state;
                    logger.Info($"Buffering Needed: {state}");
                })
                .AsObservable();

        private IObservable<bool> bufferOnSource() =>
            bufferingOnSubject
                .Where(_ => eventsEnabled)
                .Throttle(TimeSpan.FromSeconds(1))
                .Where(_=> isBuffering == false &&
                           streamBufferingFilter.Any(bufferingNeeded => bufferingNeeded))
                .Select(_ => true);

        private IObservable<bool> bufferOffSource() =>
            bufferingOffSubject
                .Where(_ => eventsEnabled && 
                            isBuffering && 
                            !streamBufferingFilter.Any(bufferingNeeded => bufferingNeeded))
                .Select(_ => false);

        public IObservable<DataArgs> DataNeededStateChanged() =>
            streamBuffers
                .Where(buffer => buffer != null)
                .Select(buffer => buffer.DataState())
                .Aggregate( (curr, next) => curr.Merge(next) )
                .Where(args => eventsEnabled && args.SequenceId == sequenceId)
                .Do(args =>
                    {
                        logger.Info(args.ToString());
                    })
                .AsObservable();
                

        public StreamBuffer GetStreamBuffer(StreamType stream) 
            => streamBuffers[(int)stream];

        public void Initialize(StreamType stream)
        {
            logger.Info(stream.ToString());

            var index = (int) stream;

            if (streamBuffers[index] != null)
            {
                throw new ArgumentException($"Stream buffer {stream} already initialized");
            }
            
            streamBuffers[index] = new StreamBuffer(stream,sequenceId);
        }

        public void DataIn(Packet packet)
        {
            var index = (int) packet.StreamType;

            if (!packet.ContainsData())
            {
                if (packet is EOSPacket)
                    streamBuffers[index].MarkEosDts();

                return;
            }
            
            streamBuffers[index].DataIn(packet);

            if (!packet.ContainsData())
                return;

            if(streamBufferingFilter[index])
                logger.Info($"{packet.StreamType}: Buffering Off");

            var wasEmpty = streamBufferingFilter[index];
            streamBufferingFilter[index] = false;
            
            if(wasEmpty)
                bufferingOffSubject.OnNext(Unit.Default);
        }

        public void DataOut(Packet packet)
        {
            if (!packet.ContainsData())
                return;

            var index = (int) packet.StreamType;
            var isEmpty = streamBuffers[index].DataOut(packet);

            if (!packet.ContainsData())
                return;

            if(isEmpty )
                logger.Info($"{packet.StreamType}: Buffering On");

            streamBufferingFilter[index] = isEmpty;

            if(isEmpty)
                bufferingOnSubject.OnNext(Unit.Default);
        }

        private void ResetStreamBufferingFilter()
        {
            streamBufferingFilter = Enumerable.Repeat(false, (int) StreamType.Count).ToArray();
            isBuffering = false;

        }
        public void EnableEvents()
        {
            logger.Info("");

            eventsEnabled = true;
        }

        public void DisableEvents()
        {
            logger.Info("");

            eventsEnabled = false;
        }

        public void ReportFullBuffer()
        {
            logger.Info("");

            foreach (var buffer in streamBuffers)
                buffer?.ReportFullBuffer();
        }

        public void ReportActualBuffer()
        {
            logger.Info("");

            foreach (var buffer in streamBuffers)
                buffer?.ReportActualBuffer();
        }

        public void UpdateBuffer()
        {
            logger.Info("");

            foreach (var buffer in streamBuffers)
                buffer?.Update();
        }

        public void SetMetaDataConfiguration(MetaDataStreamConfig config)
        {
            logger.Info("");

            var index = (int)config.Stream;
            
            if (config == metaDataConfigs[index])
                return;

            metaDataConfigs[index] = config;
            streamBuffers[index].UpdateBufferConfiguration(config);
        }

        public void ResetBuffers()
        {
            logger.Info("");

            sequenceId++;

            ResetStreamBufferingFilter();

            foreach (var buffer in streamBuffers)
                buffer?.Reset(sequenceId);

        }
        public void Dispose()
        {
            logger.Info("");
            
            foreach (var buffer in streamBuffers)
                buffer?.Dispose();

            bufferingOnSubject.Dispose();
            bufferingOffSubject.Dispose();
        }
    }
}
