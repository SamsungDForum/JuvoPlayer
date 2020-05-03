/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2020, Samsung Electronics Co., Ltd
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
using System.Net.Sockets;
using System.Text;

namespace JuvoLogger.Udp
{
    internal class MtuBuffer : IDisposable
    {
        public delegate void AsyncDone(object o, SocketAsyncEventArgs args);
        public delegate void BufferDone(MtuBuffer buffer);

        private readonly AsyncDone _completeAsyncHandler;
        private readonly BufferDone _completedHandler;
        private readonly SocketAsyncEventArgs _asyncState;

        public MtuBuffer(in int bufferCapacity, in AsyncDone asyncHandler, in BufferDone bufferHandler = null)
        {
            _completeAsyncHandler = asyncHandler;
            _completedHandler = bufferHandler;
            _asyncState = CreateSocketAsyncEventArgs();
            _asyncState.SetBuffer(new byte[bufferCapacity], 0, 0); // Mark buffer as "empty"
        }
        public MtuBuffer(in byte[] message, in AsyncDone asyncHandler, in BufferDone bufferHandler = null)
        {
            _completeAsyncHandler = asyncHandler;
            _completedHandler = bufferHandler;
            _asyncState = CreateSocketAsyncEventArgs();
            _asyncState.SetBuffer(message, 0, message.Length); // Mark buffer as "containing data"
        }
        private SocketAsyncEventArgs CreateSocketAsyncEventArgs()
        {
            SocketAsyncEventArgs asyncState = new SocketAsyncEventArgs
            {
                UserToken = this
            };
            asyncState.Completed += new EventHandler<SocketAsyncEventArgs>(_completeAsyncHandler);
            return asyncState;
        }
        public MtuBuffer Append(in string message)
        {
            var buffer = _asyncState.Buffer;
            var bufferedBytes = _asyncState.Count;

            // Truncate input message to available buffer space.
            var consumeLength = Math.Min(message.Length, buffer.Length - bufferedBytes);

            Encoding.UTF8.GetBytes(message, 0, consumeLength, buffer, bufferedBytes);
            _asyncState.SetBuffer(_asyncState.Offset, bufferedBytes + consumeLength);
            return this;
        }

        public static void Complete(in MtuBuffer buffer) => buffer._completedHandler?.Invoke(buffer);
        public static void CompleteAsync(in MtuBuffer buffer) => buffer._completeAsyncHandler(null, buffer._asyncState);

        public void Dispose()
        {
            _asyncState.Dispose();
        }

        public static implicit operator SocketAsyncEventArgs(in MtuBuffer buffer) => buffer._asyncState;
        public static implicit operator MtuBuffer(in SocketAsyncEventArgs asyncState) => (MtuBuffer)asyncState.UserToken;
    }
}
