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
using System.IO;
using System.Threading;
using Tizen;

namespace JuvoPlayer.RTSP
{
    public class SharedBufferFile : ISharedBuffer {

        static readonly object _locker = new object();
        private static Stream fs;
        private int offset;
        private byte[] buffer;

        public bool EndOfData { get; set; } = false;

        public SharedBufferFile(string filename) {
            fs = File.OpenRead(filename);
            offset = 0;
            buffer = new byte[fs.Length];
            fs.Read(buffer, 0, (int)fs.Length);
        }

        public void ClearData() {
            lock(_locker)
            {
                offset = 0;
                EndOfData = false;
            }
        }

        public void WriteData(byte[] data, bool endOfData = false) // endOfData=true should be atomic with writing last bit of data
        {
        }

        // SharedBuffer::ReadData(int size) is blocking - it will block until it has enough data or return less data if EOF is reached.
        // Returns byte array of leading [size] bytes of data from the buffer; it should remove the leading [size] bytes of data from the buffer.
        public byte[] ReadData(int size)
        {
            Log.Info("JuvoPlayer", "SharedBuffer::ReadData(" + size + ") IN");
            lock(_locker)
            {
                while(true)
                {
                    if(buffer.Length - offset >= size || EndOfData == true)
                    {
                        long dsize = Math.Min(buffer.Length, size);
                        byte[] temp = new byte[dsize]; // should be optimized later by removing excessive copying
                        Buffer.BlockCopy(buffer, offset, temp, 0, (int)dsize);
                        offset += (int)dsize;
                        Log.Info("JuvoPlayer", "SharedBuffer::ReadData(" + size + ") OUT");
                        return temp;
                    }
                    Monitor.Wait(_locker); // lock is released while waiting
                }
            }
        }
    }

}