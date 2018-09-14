// Copyright (c) 2017 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

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
    internal class EsPlayerPacketStorage
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        /// <summary>
        /// Data storage collection
        /// </summary>
        private BlockingCollection<Packet>[] packetQueues;

        /// <summary>
        /// Reference to a singleton instance of EsPlayerPacketStorage
        /// </summary>
        private static EsPlayerPacketStorage packetStorage;

        private static readonly object CreateLock = new object();

        #region Instance Support

        /// <summary>
        /// Obtains an instance of packet storage
        /// </summary>
        /// <returns>EsPlayerPacketStorage</returns>
        public static EsPlayerPacketStorage GetInstance()
        {
            lock (CreateLock)
            {
                if (packetStorage != null)
                    return packetStorage;

                packetStorage = new EsPlayerPacketStorage();
                packetStorage.CreateStorage();

                return packetStorage;
            }
        }

        /// <summary>
        /// Creates underlying storage collection. 
        /// </summary>
        private void CreateStorage()
        {
            logger.Info("Creating Instance");
            packetQueues = new BlockingCollection<Packet>[(int)Common.StreamType.Count];
        }

        /// <summary>
        /// Initializes storage for specified stream. Has to be called before
        /// using stream for data transfer
        /// </summary>
        /// <param name="stream">Common.StreamType</param>
        public void Initialize(Common.StreamType stream)
        {
            logger.Info(stream.ToString());
            // Grab "old" queue 
            //
            var queue = packetQueues[(int)stream];

            // Create new queue in its place
            //
            packetQueues[(int)stream] = new BlockingCollection<Packet>();

            // Remove previous data if existed in first place...
            //
            EmptyQueue(ref queue);
            queue?.Dispose();
        }

        /// <summary>
        /// Releases created instance of data storage
        /// </summary>
        public static void FreeInstance()
        {
            lock (CreateLock)
            {
                if (packetStorage == null)
                    return;

                packetStorage.DisposeStorage();
                packetStorage = null;
            }
        }
        #endregion

        #region Public API

        /// <summary>
        /// Adds packet to internal packet storage.
        /// </summary>
        /// <param name="packet">Packet to be added</param>
        public void AddPacket(Packet packet)
        {
            try
            {
                var queue = packetQueues[(int)packet.StreamType];
                queue.Add(packet);
                return;
            }
            catch (NullReferenceException)
            {
                logger.Warn($"Uninitialized packet storage for {packet.StreamType}");
            }
            catch (ObjectDisposedException)
            {
                logger.Warn($"Packet storage for {packet.StreamType} is disposed");
            }
            catch (InvalidOperationException)
            {
                logger.Warn($"Packet storage for {packet.StreamType} is stopped");
            }

            packet.Dispose();
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
        public Packet GetPacket(Common.StreamType stream, CancellationToken extStopToken)
        {
            try
            {
                return packetQueues[(int)stream].Take(extStopToken);
            }
            catch (NullReferenceException)
            {
                logger.Warn($"Uninitialized packet storage for {stream}");
            }
            catch (ObjectDisposedException)
            {
                logger.Warn($"Packet storage for {stream} is disposed");
            }

            return new Packet { IsEOS = true };
        }

        /// <summary>
        /// Disables storage. No further addition of data will be possible.
        /// Extraction of already contained data is still possible.
        /// </summary>
        /// <param name="stream">stream for which packet is to be retrieved</param>
        public void Disable(Common.StreamType stream)
        {
            try
            {
                packetQueues[(int)stream].CompleteAdding();
            }
            catch (NullReferenceException)
            {
                logger.Warn($"Uninitialized packet storage for {stream}");
            }
            catch (ObjectDisposedException)
            {
                logger.Warn($"Packet storage for {stream} is disposed");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Empties individual data queue
        /// </summary>
        /// <param name="queue">BlockingCollection<Packet></param>
        private void EmptyQueue(ref BlockingCollection<Packet> queue)
        {
            if (queue == null)
                return;

            var queueData = queue.ToArray();

            // We do not care about order of execution nor have to wait for its 
            // completion
            //
            logger.Info($"Disposing of {queueData.Length} packets");

            queueData.AsParallel().ForAll(aPacket => aPacket.Dispose());
        }

        /// <summary>
        /// Clears all underlying data storage - all data queues.
        /// </summary>
        private void DisposeStorage()
        {
            if (packetQueues == null)
                return;

            var queues = packetQueues.ToArray();

            // We have an array of blocking collection now, we can
            // dispose of them by calling EmptyQueue on each.
            //
            queues.AsParallel().ForAll(packetQueue =>
            {
                EmptyQueue(ref packetQueue);
                packetQueue?.Dispose();
            });
        }

        #endregion
    }
}
