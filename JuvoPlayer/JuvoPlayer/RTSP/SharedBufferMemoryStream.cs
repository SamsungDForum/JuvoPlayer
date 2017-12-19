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
using System;
using System.Threading;
using Tizen;

namespace JuvoPlayer.RTSP {

    // NOTE(g.skowinski): DEBUG HELPER CLASS - REMOVE IT LATER
    public class SharedBufferMemoryStream : ISharedBuffer {

        static readonly object _locker = new object();
        private System.IO.MemoryStream buffer = new System.IO.MemoryStream();

        public bool EndOfData { get; set; } = false;
        private long readOffset = 0;
        private long writeOffset = 0;

        public SharedBufferMemoryStream() {
        }

        public void ClearData() {
            lock (_locker) {
                buffer = new System.IO.MemoryStream();
                readOffset = 0;
                writeOffset = 0;
                EndOfData = false;
            }
        }

        public void WriteData(byte[] data, bool endOfData = false) // endOfData=true should be atomic with writing last bit of data
        {
            //Log.Info("JuvoPlayer", "SharedBuffer::WriteData(" + data.Length + ") IN");
            lock (_locker) {
                buffer.Position = writeOffset;
                buffer.Write(data, 0, data.Length);
                writeOffset += data.Length;
                EndOfData = endOfData;
                Monitor.PulseAll(_locker);
            }
            //Log.Info("JuvoPlayer", "SharedBuffer::WriteData(" + data.Length + ") OUT");
        }

        // SharedBuffer::ReadData(int size) is blocking - it will block until it has enough data or return less data if EOF is reached.
        // Returns byte array of leading [size] bytes of data from the buffer; it should remove the leading [size] bytes of data from the buffer.
        public byte[] ReadData(int size) {
            Log.Info("JuvoPlayer", "SharedBuffer::ReadData(" + size + ") IN");
            lock (_locker) {
                while (true) {
                    if (buffer.Length - readOffset >= size || EndOfData == true) {
                        long dsize = Math.Min(buffer.Length, size);
                        byte[] temp = new byte[dsize]; // should be optimized later by removing excessive copying
                        buffer.Position = readOffset;
                        readOffset += buffer.Read(temp, 0, (int)dsize);
                        Log.Info("JuvoPlayer", "SharedBuffer::ReadData(" + size + ") OUT");
                        return temp;
                    }
                    Monitor.Wait(_locker); // lock is released while waiting
                }
            }
        }
    }

}