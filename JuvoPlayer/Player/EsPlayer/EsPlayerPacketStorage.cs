/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using Nito.AsyncEx;

namespace JuvoPlayer.Player.EsPlayer
{
    /// <summary>
    /// Provides packet storage services for EsPlayer
    /// </summary>
    internal sealed class EsPlayerPacketStorage : IDisposable
    {
        private struct DataStorage
        {
            private readonly ConcurrentQueue<Packet> _queueDataStorage;
            public readonly AsyncCollection<Packet> Queue;
            public volatile bool IsDisabled;
            public int Count => _queueDataStorage.Count;

            public DataStorage(bool isDisabled)
            {
                _queueDataStorage = new ConcurrentQueue<Packet>();
                Queue = new AsyncCollection<Packet>(_queueDataStorage);
                IsDisabled = isDisabled;
            }
        }

        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        /// <summary>
        /// Data storage collection
        /// </summary>
        private readonly DataStorage[] packetQueues = new DataStorage[(int)StreamType.Count];

        #region Public API

        /// <summary>
        /// Initializes storage for specified stream. Has to be called before
        /// using stream for data transfer
        /// </summary>
        /// <param name="stream">Common.StreamType</param>
        public void Initialize(StreamType stream)
        {
            logger.Info(stream.ToString());

            // Create new queue in its place
            packetQueues[(int)stream] = new DataStorage(false);
        }


        /// <summary>
        /// Adds packet to internal packet storage.
        /// </summary>
        /// <param name="packet">Packet to be added</param>
        public Task AddPacket(Packet packet)
        {
            if (packetQueues[(int)packet.StreamType].IsDisabled)
                throw new InvalidOperationException();

            return packetQueues[(int)packet.StreamType].Queue.AddAsync(packet);
        }

        /// <summary>
        ///
        /// Retrieves a packet from internal storage for a given stream type
        ///
        /// </summary>
        /// <param name="stream">stream for which packet is to be retrieved</param>
        /// <param name="token">external cancellation token for stopping retrieval</param>
        /// <returns>Packet. May be null!</returns>
        /// <exception cref="InvalidOperationException">
        /// Internal System.Collections.Concurrent.BlockingCollection Indexed by Packet.StreamType
        /// has been marked as complete with regards to additions.
        /// -or- The underlying collection didn't accept the item.
        /// <exception cref="OperationCanceledException">
        /// extStopToken has been cancelled.
        /// </exception>
        /// <remarks>
        /// All Other exceptions, return EOS packet as data source is unusable
        /// </remarks>
        public Task<Packet> GetPacket(StreamType stream, CancellationToken token)
        {
            return packetQueues[(int)stream].Queue.TakeAsync(token);
        }

        /// <summary>
        /// Disables storage. No further addition of data will be possible.
        /// Extraction of already contained data is still possible.
        /// </summary>
        /// <param name="stream">stream for which packet is to be retrieved</param>
        public void Disable(StreamType stream)
        {
            packetQueues[(int)stream].Queue.CompleteAdding();
            packetQueues[(int)stream].IsDisabled = true;
        }

        public int Count(StreamType stream)
        {
            return packetQueues[(int)stream].Count;
        }

        public void Empty(StreamType stream)
        {
            EmptyQueue(stream);
        }

        public void Enable(StreamType stream)
        {
            if (!packetQueues[(int)stream].IsDisabled)
            {
                logger.Warn($"{stream}: Not disabled");
                return;
            }

            packetQueues[(int)stream].IsDisabled = false;

            logger.Info($"{stream}");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Empties individual data queue
        /// </summary>
        /// <param name="queue">BlockingCollection(packet)></param>
        /// <param name="stream">Stream to be emptied</param>
        ///
        private void EmptyQueue(StreamType stream)
        {
            var dataStorage = packetQueues[(int)stream];
            packetQueues[(int)stream] = new DataStorage(dataStorage.IsDisabled);

            Task.Run(() => DisposeQueue(stream, dataStorage));
        }

        private void DisposeQueue(StreamType stream, DataStorage ds)
        {
            var packetCount = 0;
            var configsDropped = 0;
            var packetEnumerable = ds.Queue.GetConsumingEnumerable();
            foreach (var packet in packetEnumerable)
            {
                packetCount++;
                if (packet is BufferConfigurationPacket)
                    configsDropped++;

                packet.Dispose();
            }

            // Reclaim memory after queue purge
            GC.Collect();
            logger.Info($"{stream}: Disposed {packetCount}/{configsDropped} data/config packets");

        }

        #endregion

        #region Dispose support
        private bool isDisposed;

        public void Dispose()
        {
            if (isDisposed)
                return;

            logger.Info("");
            for (var i = (StreamType)0; i < StreamType.Count; i++)
                EmptyQueue(i);

            isDisposed = true;
        }
        #endregion
    }
}
