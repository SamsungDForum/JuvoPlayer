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
using System.Threading;

namespace JuvoPlayer.RTSP
{
    public class SharedBuffer : ISharedBuffer {

        private AutoResetEvent waitHandle;
        private bool eof;
        private System.IO.MemoryStream buffer;

        public bool EndOfFile
        {
            get { return eof; }
            set { eof = value; }
        }

        public SharedBuffer() {
            buffer = new System.IO.MemoryStream();
            EndOfFile = true;
        }

        public void ClearData() {
            lock(buffer)
            {
                buffer = new System.IO.MemoryStream();
            }
        }

        public void WriteData(byte[] data)
        {
            lock(buffer)
            {
                buffer.Write(data, 0, data.Length);
            }
            waitHandle.Set();
        }

        // SharedBuffer::ReadData(int size) is blocking - it will block until it has enough data or return less data if EOF is reached.
        // Returns byte array of leading [size] bytes of data from the buffer; it should remove the leading [size] bytes of data from the buffer.
        byte[] ISharedBuffer.ReadData(int size)
        {
            while(true)
            {
                lock(buffer)
                {
                    if(buffer.Length >= size || EndOfFile == true)
                    {
                        long dsize = Math.Min(buffer.Length, size);
                        byte[] temp = new byte[dsize];
                        buffer.Read(temp, 0, (int)dsize);
                        return temp;
                    }
                }
                waitHandle.WaitOne();
            }
        }
    }

}