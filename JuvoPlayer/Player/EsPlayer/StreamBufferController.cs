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
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.Player.EsPlayer
{

    // TODO: Extend class functionality to provide:
    // TODO:    - Stream Synchronization so no streams lags behind other stream
    // TODO:    - Prebuffering on stream start (initial) to accomodate
    // TODO:      MPD's minBufferTime if present. minBufferTime = minimum buffer period
    // TODO:      before playback start assuring smooth content delivery as defined in
    // TODO:      ISO/IEC 23009-1 5.3.1.2 Table 3

    class StreamBufferController : IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly StreamBuffer[] streamBuffers = new StreamBuffer[(int)StreamType.Count];
        private readonly MetaDataStreamConfig[] metaDataConfigs = new MetaDataStreamConfig[(int)StreamType.Count];
        private readonly bool[] streamBufferBuffering = new bool[(int)StreamType.Count];
        private readonly BehaviorSubject<bool> aggregatedBufferSubject = new BehaviorSubject<bool>(false);
        private readonly IList<IDisposable> bufferSubscriptions = new List<IDisposable>();

        public void DataIn(Packet packet) =>
            streamBuffers[(int) packet.StreamType].DataIn(packet);
        
        public void DataOut(Packet packet) =>
            streamBuffers[(int) packet.StreamType].DataOut(packet);

        public IObservable<bool> BufferingStateChanged() =>
            aggregatedBufferSubject
                .DistinctUntilChanged()
                .Do(bufferingState => logger.Info($"Buffering: {bufferingState}"))
                .AsObservable();
        
        public IObservable<DataArgs> DataNeededStateChanged() =>
            streamBuffers
                .Where(buffer => buffer != null)
                .Select(buffer => buffer.DataState())
                .Aggregate( (curr, next) => curr.Merge(next) )
                .Do(a => logger.Info(a.ToString()))
                .AsObservable();

        public StreamBuffer GetStreamBuffer(StreamType stream) 
            => streamBuffers[(int)stream];

        public Func<bool> BufferEventsEnabled { get; set; } = () => false;

        private void ResetBufferingEventState()
        {
            for (var i = 0; i < streamBufferBuffering.Length; i++)
                streamBufferBuffering[i] = false;
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

        public StreamBuffer Initialize(StreamType stream)
        {
            logger.Info(stream.ToString());

            if (streamBuffers[(int) stream] != null)
            {
                throw new ArgumentException($"Stream buffer {stream} already initialized");
            }

            var buffer = new StreamBuffer(stream);
            streamBuffers[(int) stream] = buffer;

            var dataSub = buffer.DataState().Subscribe(args =>
            {
                if (!BufferEventsEnabled())
                {
                    logger.Info("Buffering events not allowed");
                    ResetBufferingEventState();
                    return;
                }

                streamBufferBuffering[(int)args.StreamType] = args.BufferEmpty;
                var anyBuffering = streamBufferBuffering.Any(isBuffering => isBuffering == true);
                aggregatedBufferSubject.OnNext(anyBuffering);
            }, SynchronizationContext.Current);

            bufferSubscriptions.Add(dataSub);

            return buffer;
        }
        
        public void ResetBuffers()
        {
            logger.Info("");

            foreach (var buffer in streamBuffers)
                buffer?.Reset();

            ResetBufferingEventState();
        }

        public void Dispose()
        {
            logger.Info("");
            
            foreach (var subscription in bufferSubscriptions)
                subscription.Dispose();
            
            foreach (var buffer in streamBuffers)
                buffer?.Dispose();

            aggregatedBufferSubject.Dispose();
        }
    }
}
