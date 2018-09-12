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
using System.Text;
using System.Threading;
using JuvoLogger;
using JuvoPlayer.Common;
using System.Threading.Tasks;


namespace JuvoPlayer.Player.EsPlayer
{
    internal class EsPlayerPacketStorage
    {
        private static ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        internal static BlockingCollection<Packet>[] packetQueues;
        internal static EsPlayerPacketStorage packetStorage;

        private static readonly Object createLock = new Object();

        public static EsPlayerPacketStorage GetInstance()
        {
            lock (createLock)
            {
                if (packetStorage != null)
                    return packetStorage;

                logger.Info("Creating Instance");
                packetStorage = new EsPlayerPacketStorage();
                packetQueues = new BlockingCollection<Packet>[(int)Common.StreamType.Count];

                return packetStorage;
            }
        }

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
                return packetQueues[(int)stream].Take();
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
        /// Disables storage. No further addition/extraction of data will be possible.
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

        private static void EmptyQueue(ref BlockingCollection<Packet> queue)
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

        private static void DisposeStorage(ref BlockingCollection<Packet>[] packetQueues)
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


        public static void FreeInstance()
        {
            lock (createLock)
            {
                if (packetStorage == null)
                    return;

                DisposeStorage(ref packetQueues);
                packetStorage = null;
            }
        }
    }
}
