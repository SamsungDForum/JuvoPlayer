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
using System.Linq;
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.Player.EsPlayer
{
    /// <summary>
    /// Provides packet storage services for EsPlayer
    /// </summary>
    internal sealed class EsPlayerPacketStorage : IDisposable
    {
        private class DataStorage
        {
            public BlockingCollection<Packet> packetQueue;
            public StreamType StreamType;
        }

        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        /// <summary>
        /// Data storage collection
        /// </summary>
        private readonly DataStorage[] packetQueues = new DataStorage[(int)Common.StreamType.Count];

        #region Public API

        /// <summary>
        /// Initializes storage for specified stream. Has to be called before
        /// using stream for data transfer
        /// </summary>
        /// <param name="stream">Common.StreamType</param>
        public void Initialize(StreamType stream)
        {
            logger.Info(stream.ToString());

            // Grab "old" queue
            var storage = packetQueues[(int)stream];

            // Create new queue in its place
            packetQueues[(int)stream] = new DataStorage
            {
                packetQueue = new BlockingCollection<Packet>(),
                StreamType = stream
            };

            if (storage == null)
                return;
            // Remove previous data if existed in first place...
            EmptyQueue(stream, ref storage.packetQueue);
            storage.packetQueue.Dispose();
        }

        /// <summary>
        /// Adds packet to internal packet storage.
        /// </summary>
        /// <param name="packet">Packet to be added</param>
        public void AddPacket(Packet packet)
        {
            try
            {
                var storage = packetQueues[(int)packet.StreamType];
                storage.packetQueue.Add(packet);
            }
            catch (InvalidOperationException)
            {
                logger.Warn($"Packet storage for {packet.StreamType} is stopped");
                packet.Dispose();
            }
        }

        /// <summary>
        ///
        /// Retrieves a packet from internal storage for a given stream type
        ///
        /// </summary>
        /// <param name="stream">stream for which packet is to be retrieved</param>
        /// <param name="extStopToken">external cancellation token for stopping retrieval</param>
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
        public Packet GetPacket(StreamType stream, CancellationToken extStopToken)
        {
            var storage = packetQueues[(int)stream];
            return storage.packetQueue.Take(extStopToken);
        }

        /// <summary>
        /// Disables storage. No further addition of data will be possible.
        /// Extraction of already contained data is still possible.
        /// </summary>
        /// <param name="stream">stream for which packet is to be retrieved</param>
        public void Disable(StreamType stream)
        {
            var storage = packetQueues[(int)stream];
            storage.packetQueue.CompleteAdding();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Empties individual data queue
        /// </summary>
        /// <param name="queue">BlockingCollection(packet)></param>
        /// <param name="stream">Stream to be emptied</param>
        ///
        private void EmptyQueue(StreamType stream, ref BlockingCollection<Packet> queue)
        {
            if (queue == null)
                return;

            var queueData = queue.ToArray();

            // We do not care about order of execution nor have to wait for its
            // completion.
            logger.Info($"{stream}: Disposing of {queueData.Length} packets");

            queueData.AsParallel().ForAll(aPacket => aPacket.Dispose());
        }

        #endregion

        #region Dispose support
        private bool isDisposed;

        public void Dispose()
        {
            if (isDisposed)
                return;

            // We have an array of blocking collection now, we can
            // dispose of them by calling EmptyQueue on each.
            packetQueues.ToArray().AsParallel().ForAll(storage =>
            {
                if (storage == null)
                    return;

                EmptyQueue(storage.StreamType, ref storage.packetQueue);
                storage.packetQueue?.Dispose();
                storage.packetQueue = null;

                logger.Info($"{storage.StreamType}: Disposed.");
            });

            isDisposed = true;
        }
        #endregion
    }
}
