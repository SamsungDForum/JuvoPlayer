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

using JuvoPlayer.Common;
using System.Collections.Generic;
using System.Threading;

namespace JuvoPlayer.Player.SMPlayer {
    public class PacketBuffer {

        private List<Packet> data;

        public enum Ordering {
            PtsAsc,
            DtsAsc,
            PtsDesc,
            DtsDesc,
            Fifo
        }

        public int MinimumSizeThreshold { get; set; }
        public int MaximumSizeThreshold { get; set; }
        public Ordering QueueOrdering { get; } // reconstructing heap isn't implemented, so ordering type cannot be changed on the fly => that's why it's get-only auto-property

        private readonly AutoResetEvent bufferFull, bufferEmpty, bufferPeek;

        public PacketBuffer(Ordering queueOrdering = Ordering.Fifo, int minimumSizeThreshold = 0, int maximumSizeThreshold = int.MaxValue) {
            data = new List<Packet>();
            MinimumSizeThreshold = minimumSizeThreshold;
            MaximumSizeThreshold = maximumSizeThreshold;
            bufferFull = new AutoResetEvent(true);
            bufferEmpty = new AutoResetEvent(false);
            bufferPeek = new AutoResetEvent(false);
            QueueOrdering = queueOrdering;
        }

        public void Enqueue(Packet packet) {
            for (bool done = false; !done; bufferFull.WaitOne()) {
                lock (data) {
                    if (data.Count < MaximumSizeThreshold) {
                        done = true;
                        data.Add(packet);
                        if (QueueOrdering != Ordering.Fifo) {
                            int childIndex = data.Count - 1;
                            while (childIndex > 0) {
                                int parentIndex = (childIndex - 1) / 2;
                                if (Compare(data[childIndex], data[parentIndex]) >= 0)
                                    break;
                                SwapIndices(parentIndex, childIndex);
                                childIndex = parentIndex;
                            }
                        }
                        bufferPeek.Set();
                        bufferEmpty.Set();
                        return;
                    }
                }
            }
        }

        public Packet Dequeue() {
            for (; true; bufferEmpty.WaitOne()) {
                lock (data) {
                    if (data.Count - MinimumSizeThreshold > 0) {
                        Packet headPacket = data[0];
                        if (QueueOrdering == Ordering.Fifo) {
                            data.RemoveAt(0);
                        }
                        else {
                            int lastIndex = data.Count - 1;
                            data[0] = data[lastIndex];
                            data.RemoveAt(lastIndex);
                            --lastIndex;
                            int parentIndex = 0;
                            while (true) {
                                int childIndex = parentIndex * 2 + 1;
                                if (childIndex > lastIndex)
                                    break;
                                int rightChild = childIndex + 1;
                                if (rightChild <= lastIndex && Compare(data[rightChild], data[childIndex]) < 0)
                                    childIndex = rightChild;
                                if (Compare(data[parentIndex], data[childIndex]) <= 0)
                                    break;
                                SwapIndices(parentIndex, childIndex);
                                parentIndex = childIndex;
                            }
                        }
                        bufferFull.Set();
                        return headPacket;
                    }
                }
            }
        }

        public Packet Peek() {
            lock (data) {
                return data[0]; // throws exception if data is empty!
            }
        }

        public int Count() {
            lock (data) {
                return data.Count;
            }
        }

        public void Clear() {
            lock (data) {
                data = new List<Packet>();
            }
        }

        public int AvailablePacketsCount() { // It locks on data! Don't call it from another method that locks on data like Enqueue() or Dequeue()!
            lock (data) {
                int available = data.Count - MinimumSizeThreshold;
                return available > 0 ? available : 0;
            }
        }

        private void SwapIndices(int a, int b) {
            Packet tmp = data[a];
            data[a] = data[b];
            data[b] = tmp;
        }

        private long Compare(Packet a, Packet b) {
            switch (QueueOrdering) {
                case Ordering.DtsAsc:
                    return a.Dts.CompareTo(b.Dts); // in order of increasing dts
                case Ordering.DtsDesc:
                    return b.Dts.CompareTo(a.Dts); // in order of decreasing dts
                case Ordering.PtsAsc:
                    return a.Pts.CompareTo(b.Pts); // in order of increasing pts
                case Ordering.PtsDesc:
                    return b.Pts.CompareTo(a.Pts); // in order of decreasing pts
                case Ordering.Fifo:
                default:
                    return 0; // fifo
            }
        }

        public ulong PeekSortingValue() { // blocks if buffer is empty!
            for (; true; bufferPeek.WaitOne()) {
                lock (data) {
                    if (data.Count - MinimumSizeThreshold > 0) {
                        switch (QueueOrdering) {
                            case Ordering.DtsAsc:
                            case Ordering.DtsDesc:
                                return (ulong)data[0].Dts.TotalMilliseconds;
                            case Ordering.PtsAsc:
                            case Ordering.PtsDesc:
                                return (ulong)data[0].Pts.TotalMilliseconds;
                            case Ordering.Fifo:
                            default:
                                return (ulong)data[0].Pts.TotalMilliseconds; // fifo, comparison outside by pts
                        }
                    }
                }
            }
        }
    }
}
