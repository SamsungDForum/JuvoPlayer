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
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace JuvoLogger.Udp
{
    internal class SocketSession : IDisposable
    {
        public bool StopOutput { get; private set; } = true;
        public int MaxPayloadSize { get; } = UdpLoggerToolBox.GetLowestCommonMtu() - MaxUdpHeader;

        private enum Message { Connect, Stop, Start, Terminate, Hijack };
        // Any way to get processed (encoded) byte arrays directly from resources?
        private readonly byte[][] _messageData = {
            Encoding.UTF8.GetBytes(Resources.Messages.ConnectMessage),
            Encoding.UTF8.GetBytes(Resources.Messages.StopMessage),
            Encoding.UTF8.GetBytes(Resources.Messages.StartMessage),
            Encoding.UTF8.GetBytes(Resources.Messages.TerminationMessage),
            Encoding.UTF8.GetBytes(Resources.Messages.HijackMessage)};

        private const int MaxUdpHeader = 48; // IP6 UDP header.
        private const int MaxPacketBuffers = 16;

        private Socket _socket;
        private EndPoint _clientEndPoint;
        private readonly MtuBuffer _clientListenerBuffer;
        private readonly Func<EndPoint> GetListeningEndPoint;
        private readonly ObjectPool<MtuBuffer> _bufferPool;

        public SocketSession(int port)
        {
            GetListeningEndPoint = () => new IPEndPoint(IPAddress.Any, port);

            _socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(GetListeningEndPoint());

            _bufferPool = new ObjectPool<MtuBuffer>(MaxPacketBuffers, CreateMtuBuffer, false, false);

            // connection receiver
            _clientListenerBuffer = new MtuBuffer(1, OnConnected);
            ReceiveFromEndPoint(_clientListenerBuffer);
        }
        public void Stop()
        {
            _socket.Close();
        }
        public void TryLogException(Exception e, in string message = null)
        {
            if (_clientEndPoint == null)
                return;

            var buffer = new MtuBuffer(MaxPayloadSize, OnCompleted, UdpLoggerToolBox.Dispose);
            if (message != null)
                buffer.Append(message);
            buffer.Append(e.ToString());

            SendTo(_clientEndPoint, buffer);
        }
        public void SendMessage(in string message)
        {
            // Migrate to chunk/slice append taken directly from 
            // string builder sourcing this method - if migrated to core 2.x
            var buffer = _bufferPool.Take().Append(message);
            SendTo(_clientEndPoint, buffer);
        }
        private MtuBuffer CreateMtuBuffer() => new MtuBuffer(MaxPayloadSize, OnCompleted, ReturnMtuBufferToPool);
        private void ReturnMtuBufferToPool(MtuBuffer buffer) => _bufferPool.Return(buffer);
        private void OnCompleted(object o, SocketAsyncEventArgs asyncState)
        {
            // asyncState.SocketError: Which errors are recoverable, if any?
            // Send*Async()s: Possible scenario?
            // - Expected transmission = buffer[0..Size-1]
            // - args.BytesTransferred < Expected transmission
            asyncState.SetBuffer(asyncState.Offset, 0);
            MtuBuffer.Complete(asyncState);
        }
        private void OnConnected(object o, SocketAsyncEventArgs asyncState)
        {
            if (asyncState.SocketError == SocketError.Success)
                ProcessEndPoint(asyncState.RemoteEndPoint);

            ReceiveFromEndPoint(asyncState);
        }
        private void ReceiveFromEndPoint(SocketAsyncEventArgs asyncState)
        {
            try
            {
                asyncState.RemoteEndPoint = GetListeningEndPoint();
                asyncState.SetBuffer(asyncState.Offset, asyncState.Buffer.Length);
                if (!_socket.ReceiveFromAsync(asyncState))
                    MtuBuffer.CompleteAsync(asyncState);
            }
            catch (Exception e)
            {
                TryLogException(e);
                throw;
            }
        }
        private void SendTo(in EndPoint target, in MtuBuffer buffer)
        {
            try
            {
                var asyncState = (SocketAsyncEventArgs)buffer;
                asyncState.RemoteEndPoint = target;
                if (!_socket.SendToAsync(asyncState))
                    MtuBuffer.CompleteAsync(buffer);
            }
            catch (Exception e)
            {
                TryLogException(e);
                // Be an Englishmen. Silently ignore. In UDP world things get lost
                // "Disconnect" client perhaps?
            }
        }

        // Run fixed messages buffers outside of mtuBufferPool. Not expecting that many of them.
        // Less fiddling with mtu buffer restore / message copying.
        private MtuBuffer BufferFromMessage(in Message msg) =>
            new MtuBuffer(_messageData[(int)msg], OnCompleted, UdpLoggerToolBox.Dispose);

        private void ProcessEndPoint(in EndPoint newEp)
        {
            if (!newEp.SameAs(_clientEndPoint))
            {
                if (_clientEndPoint != null)
                    SendTo(_clientEndPoint, BufferFromMessage(Message.Hijack));

                SendTo(newEp, BufferFromMessage(Message.Connect));
                _clientEndPoint = newEp;
                StopOutput = false;
                return;
            }

            StopOutput = !StopOutput;
            SendTo(_clientEndPoint, BufferFromMessage(StopOutput ? Message.Stop : Message.Start));
        }

        #region IDisposable Support
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _socket.Close();
                    _clientListenerBuffer.Dispose();
                    _bufferPool.Dispose();
                    _socket.Dispose();
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
