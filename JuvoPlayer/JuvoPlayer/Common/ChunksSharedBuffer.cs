// Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved
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
using System.Linq;
using System.Threading;

namespace JuvoPlayer.Common
{
    public class ChunksSharedBuffer : ISharedBuffer
    {
        private readonly object locker = new object();
        private readonly LinkedList<byte[]> buffer = new LinkedList<byte[]>();
        private int readPos;

        public bool EndOfData { get; set; }

        public ChunksSharedBuffer()
        {
            EndOfData = false;
        }

        public ulong Length()
        {
            lock (locker)
            {
                return (ulong) buffer.Sum(o => o.Length);
            }
        }

        public void ClearData()
        {
            lock (locker)
            {
                buffer.Clear();
                EndOfData = false;
            }
        }

        public void WriteData(byte[] data, bool endOfData = false)
        { // endOfData=true should be atomic with writing last bit of data
            lock (locker)
            {
                if (data != null)
                {
                    buffer.AddLast(data);
                }

                EndOfData = endOfData;
                Monitor.PulseAll(locker);
            }
        }

        // ChunksSharedBuffer::ReadData(int size) is blocking - it will block until buffer is not empty or if EOF is reached.
        // size is the maximum amount of data that can be returned from buffer.
        public ArraySegment<byte>? ReadData(int size)
        {
            lock (locker)
            {
                while (true)
                {
                    if (buffer.Count > 0)
                    {
                        var bytesLeft = buffer.First.Value.Length - readPos;
                        var read = Math.Min(bytesLeft, size);
                        var segment = new ArraySegment<byte>(buffer.First.Value, readPos, read);
                        readPos += read;
                        if (readPos >= buffer.First.Value.Length)
                        {
                            readPos = 0;
                            buffer.RemoveFirst();
                        }
                        return segment;
                    }
                    if (EndOfData)
                    {
                        return null;
                    }

                    Monitor.Wait(locker); // lock is released while waiting
                }
            }
        }
    }
}
