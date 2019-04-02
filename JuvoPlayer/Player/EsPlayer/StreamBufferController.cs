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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Joins;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.Player.EsPlayer
{


    class StreamBufferController : IDisposable
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly StreamBuffer[] streamBuffers = new StreamBuffer[(int)StreamType.Count];
        private readonly MetaDataStreamConfig[] metaDataConfigs = new MetaDataStreamConfig[(int)StreamType.Count];
        private bool[] streamBufferBuffering = new bool[(int)StreamType.Count];
        private readonly SynchronizationContext bufferingContext = new SynchronizationContext();
        private readonly BehaviorSubject<bool> aggregatedBufferSubject = new BehaviorSubject<bool>(false);
        private readonly IList<IDisposable> bufferSubscriptions = new List<IDisposable>();

        public StreamBuffer GetStreamBuffer(StreamType stream) 
            => streamBuffers[(int)stream];

        public void SetMetaDataConfiguration(MetaDataStreamConfig config)
        {
            logger.Info("");

            var index = (int)config.Stream;
            
            if (config == metaDataConfigs[index])
                return;

            metaDataConfigs[index] = config;
            streamBuffers[index].UpdateBufferConfiguration(config);
        }

        public void DataIn(Packet packet) =>
            streamBuffers[(int) packet.StreamType].DataIn(packet);
        
        public void DataOut(Packet packet) =>
            streamBuffers[(int) packet.StreamType].DataOut(packet);
        
        public IObservable<bool> BufferingStateChanged() =>
            aggregatedBufferSubject
                .DistinctUntilChanged();
        
        public IObservable<DataArgs> DataNeededStateChanged() =>
            streamBuffers
                .Where(buffer => buffer != null)
                .Select(buffer => buffer.DataState)
                .Aggregate( (curr, next) => curr.Merge(next) )
                .AsObservable();

        public StreamBuffer Initialize(StreamType stream)
        {
            logger.Info(stream.ToString());

            if (streamBuffers[(int) stream] != null)
            {
                throw new ArgumentException($"Stream buffer {stream} already initialized");
            }

            var buffer = new StreamBuffer(stream);

            bufferSubscriptions.Add(
                buffer.BufferState.Subscribe(state =>
                {
                    streamBufferBuffering[(int) stream] = state;
                    var anyBuffering = streamBufferBuffering.Any(isBuffering => isBuffering);
                    aggregatedBufferSubject.OnNext(anyBuffering);
                }, bufferingContext)
            );


            streamBuffers[(int) stream] = buffer;
            metaDataConfigs[(int)stream] = new MetaDataStreamConfig();

            return buffer;
        }

        
        public void ResetBuffers()
        {
            logger.Info("");

            foreach (var buffer in streamBuffers)
                buffer?.Reset();

            streamBufferBuffering = new bool[(int)StreamType.Count];
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
