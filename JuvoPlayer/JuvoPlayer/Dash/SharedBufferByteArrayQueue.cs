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
using Tizen;

namespace JuvoPlayer.RTSP {

    public class SharedBufferByteArrayQueue : ISharedBuffer {

        public class ByteArrayQueue {
            private byte[] _buffer;
            private int _head;
            private int _tail;
            private readonly int _initialBufferSize;
            private int Size => _head <= _tail ? _tail - _head : (_buffer.Length - _head) + _tail;
            private static readonly object QueLocker = new object();

            public ByteArrayQueue(int initialBufferSize = 2048) {
                _initialBufferSize = initialBufferSize;
                _buffer = new byte[_initialBufferSize];
                _head = 0;
                _tail = 0;
            }

            public void Clear() {
                lock (QueLocker) {
                    _head = 0;
                    _tail = 0;
                    Resize(_initialBufferSize);
                }
            }

            public int Length => Size;

            private void Resize(int newSize) {
                if (_buffer.Length == newSize) {
                    return;
                }
                byte[] newBuffer = new byte[newSize];
                int oldSize = Size;
                if (oldSize > 0) {
                    if (_head < _tail) {
                        Buffer.BlockCopy(_buffer, _head, newBuffer, 0, oldSize);
                    }
                    else {
                        Buffer.BlockCopy(_buffer, _head, newBuffer, 0, _buffer.Length - _head);
                        Buffer.BlockCopy(_buffer, 0, newBuffer, _buffer.Length - _head, _tail);
                    }
                }
                _head = 0;
                _tail = oldSize;
                _buffer = newBuffer;
            }

            public void Push(byte[] buffer, int offset, int size) {
                lock (QueLocker) {
                    if ((Size + size) > _buffer.Length) {
                        Resize(1 << ((int)Math.Ceiling(Math.Log(Size + size, 2)) + 1)); // resize to double the lowest power of 2 that is higher than required size
                    }
                    if (_head < _tail) {
                        int length = _buffer.Length - _tail;
                        if (length >= size) {
                            Buffer.BlockCopy(buffer, offset, _buffer, _tail, size);
                        }
                        else {
                            Buffer.BlockCopy(buffer, offset, _buffer, _tail, length);
                            Buffer.BlockCopy(buffer, offset + length, _buffer, 0, size - length);
                        }
                    }
                    else {
                        Buffer.BlockCopy(buffer, offset, _buffer, _tail, size);
                    }
                    _tail = (_tail + size) % _buffer.Length;
                }
            }

            public int Pop(byte[] buffer, int offset, int size) {
                lock (QueLocker) {
                    size = Math.Min(size, Size);
                    if (_head < _tail) {
                        Buffer.BlockCopy(_buffer, _head, buffer, offset, size);
                    }
                    else {
                        int length = _buffer.Length - _head;
                        if (length >= size) {
                            Buffer.BlockCopy(_buffer, _head, buffer, offset, size);
                        }
                        else {
                            Buffer.BlockCopy(_buffer, _head, buffer, offset, length);
                            Buffer.BlockCopy(_buffer, 0, buffer, offset + length, size - length);
                        }
                    }
                    _head = (_head + size) % _buffer.Length;
                    if (Size == 0) {
                        _head = 0;
                        _tail = 0;
                    }
                    if (_buffer.Length > Size * 2 && Size * 2 > _initialBufferSize) {
                        Resize(_buffer.Length / 2);
                    }
                    return size;
                }
            }
        }

        static readonly object Locker = new object();
        private readonly ByteArrayQueue _buffer = new ByteArrayQueue();

        public bool EndOfData { get; set; }

        public void ClearData() {
            lock (Locker) {
                _buffer.Clear();
                EndOfData = false;
            }
        }

        public void WriteData(byte[] data, bool endOfData = false) { // endOfData=true should be atomic with writing last bit of data
            //Log.Info("JuvoPlayer", "SharedBuffer::WriteData(" + data.Length + ") IN");
            lock (Locker) {
                _buffer.Push(data, 0, data.Length);
                EndOfData = endOfData;
                Monitor.PulseAll(Locker);
            }
            //Log.Info("JuvoPlayer", "SharedBuffer::WriteData(" + data.Length + ") OUT");
        }

        // SharedBuffer::ReadData(int size) is blocking - it will block until it has enough data or return less data if EOF is reached.
        // Returns byte array of leading [size] bytes of data from the buffer; it should remove the leading [size] bytes of data from the buffer.
        public byte[] ReadData(int size) {
            Log.Info("JuvoPlayer", "SharedBuffer::ReadData(" + size + ") IN");
            lock (Locker) {
                while (true) {
                    if (_buffer.Length >= size || EndOfData) {
                        long dsize = Math.Min(_buffer.Length, size);
                        byte[] temp = new byte[dsize]; // should be optimized later by removing excessive copying
                        _buffer.Pop(temp, 0, (int)dsize);
                        Log.Info("JuvoPlayer", "SharedBuffer::ReadData(" + size + ") OUT");
                        return temp;
                    }
                    Monitor.Wait(Locker); // lock is released while waiting
                }
            }
        }
    }
}