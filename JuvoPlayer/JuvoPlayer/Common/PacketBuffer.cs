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
using System.Collections.Generic;
using System.Threading;
using Tizen;

namespace JuvoPlayer.Common {
    public class PacketBuffer {

        private List<StreamPacket> data;

        public enum Ordering {
            PtsAsc,
            DtsAsc,
            PtsDesc,
            DtsDesc,
            Fifo
        }

        public int MinimumSizeThreshold { get; set; }
        public int MaximumSizeThreshold { get; set; }
        private Ordering QueueOrdering { get; set; }

        AutoResetEvent bufferFull, bufferEmpty, bufferPeek;

        public PacketBuffer(Ordering queueOrdering = Ordering.Fifo, int minimumSizeThreshold = 0, int maximumSizeThreshold = int.MaxValue) {
            data = new List<StreamPacket>();
            MinimumSizeThreshold = minimumSizeThreshold;
            MaximumSizeThreshold = maximumSizeThreshold;
            bufferFull = new AutoResetEvent(true);
            bufferEmpty = new AutoResetEvent(false);
            bufferPeek = new AutoResetEvent(false);
            QueueOrdering = queueOrdering;
        }

        public void Enqueue(StreamPacket packet) {
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

        public StreamPacket Dequeue() {
            for (; true; bufferEmpty.WaitOne()) {
                lock (data) {
                    if (data.Count - MinimumSizeThreshold > 0) {
                        StreamPacket headPacket = data[0];
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

        public StreamPacket Peek() {
            lock (data) {
                return data[0]; // throws exception if data is empty!
            }
        }

        public int Count() {
            lock (data) {
                return data.Count;
            }
        }

        public void Flush() {
            lock (data) {
                data = new List<StreamPacket>();
            }
        }

        public int AvailablePacketsCount() { // It locks on data! Don't call it from another method that locks on data like Enqueue() or Dequeue()!
            lock (data) {
                int available = data.Count - MinimumSizeThreshold;
                return available > 0 ? available : 0;
            }
        }

        private void SwapIndices(int a, int b) {
            StreamPacket tmp = data[a];
            data[a] = data[b];
            data[b] = tmp;
        }

        private long Compare(StreamPacket a, StreamPacket b) {
            switch (QueueOrdering) {
                case Ordering.DtsAsc:
                    return (long)(a.Dts - b.Dts); // in order of increasing dts
                case Ordering.DtsDesc:
                    return (long)(b.Dts - a.Dts); // in order of decreasing dts
                case Ordering.PtsAsc:
                    return (long)(a.Pts - b.Pts); // in order of increasing pts
                case Ordering.PtsDesc:
                    return (long)(b.Pts - a.Pts); // in order of decreasing pts
                case Ordering.Fifo:
                default:
                    return 0; // fifo
            }
        }


        public ulong PeekSortingValue() {
            for (; true; bufferPeek.WaitOne()) {
                lock (data) {
                    if (data.Count - MinimumSizeThreshold > 0) {
                        switch (QueueOrdering) {
                            case Ordering.DtsAsc:
                            case Ordering.DtsDesc:
                                return data[0].Dts;
                            case Ordering.PtsAsc:
                            case Ordering.PtsDesc:
                                return data[0].Pts;
                            case Ordering.Fifo:
                            default:
                                return data[0].Pts; // fifo, comparison outside by pts
                        }
                    }
                }
            }
        }

        /*
        public bool IsConsistent() {
            lock (data) {
                if (QueueOrdering == Ordering.Fifo)
                    return true;
                if (data.Count == 0)
                    return true;
                int lastIndex = data.Count - 1;
                for (int parentIndex = 0; parentIndex < data.Count; ++parentIndex) {
                    int leftChildIndex = 2 * parentIndex + 1;
                    int rightChildIndex = 2 * parentIndex + 2;

                    if (leftChildIndex <= lastIndex && Compare(data[parentIndex], data[leftChildIndex]) > 0)
                        return false;
                    if (rightChildIndex <= lastIndex && Compare(data[parentIndex], data[rightChildIndex]) > 0)
                        return false;
                }
                return true;
            }
        }
        */
    }
}
