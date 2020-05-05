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
        private readonly UdpPacket _clientListenerPacket;
        private readonly Func<EndPoint> GetListeningEndPoint;
        private readonly ObjectPool<UdpPacket> _packetPool;

        public SocketSession(int port)
        {
            GetListeningEndPoint = () => new IPEndPoint(IPAddress.Any, port);

            _socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(GetListeningEndPoint());

            _packetPool = new ObjectPool<UdpPacket>(MaxPacketBuffers, CreatePoolPacket, false, false);

            // connection receiver
            _clientListenerPacket = new UdpPacket(1, OnConnected);
            ReceiveFromEndPoint(_clientListenerPacket);
        }
        public void Stop()
        {
            _socket.Close();
        }
       
        public void SendMessage(in string message)
        {
            if (_clientEndPoint == null)
                return;

            // Migrate to chunk/slice append taken directly from 
            // string builder sourcing this method - if migrated to core 2.x
            var bytesSent = 0;
            var msgLength = message.Length;
            do
            {
                var buffer = _packetPool.Take();
                bytesSent += buffer.Append(message,bytesSent, msgLength - bytesSent);
                SendTo(_clientEndPoint, buffer);
            } while (msgLength > bytesSent);            
        }

        private UdpPacket CreatePoolPacket() => new UdpPacket(MaxPayloadSize, OnCompleted, ReturnPacketToPool);
        private void ReturnPacketToPool(UdpPacket packet) => _packetPool.Return(packet);
        private void OnCompleted(object o, SocketAsyncEventArgs asyncState)
        {
            // asyncState.SocketError: Which errors are recoverable, if any?
            // Send*Async()s: Possible scenario?
            // - Expected transmission = buffer[0..Size-1]
            // - args.BytesTransferred < Expected transmission
            asyncState.SetBuffer(asyncState.Offset, 0);
            UdpPacket.Complete(asyncState);
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
                    UdpPacket.CompleteAsync(asyncState);
            }
            catch (Exception e)
            {
                SendMessage(e.ToString());
                throw;
            }
        }
        private void SendTo(in EndPoint target, in UdpPacket buffer)
        {
            try
            {
                var asyncState = (SocketAsyncEventArgs)buffer;
                asyncState.RemoteEndPoint = target;
                if (!_socket.SendToAsync(asyncState))
                    UdpPacket.CompleteAsync(buffer);
            }
            catch (Exception)
            {
                // If we've failed to send message to end point... 
                // no point in trying to send exception which caused this failure.
                // Disconnect client
                StopOutput = true;
                _clientEndPoint = null;
            }
        }

        // Run fixed messages buffers outside of packet pool. Not expecting that much traffic here.
        // Less fiddling then buffer restore / message copying.
        private UdpPacket PacketFromMessage(in Message msg) =>
            new UdpPacket(_messageData[(int)msg], OnCompleted, UdpLoggerToolBox.Dispose);

        private void ProcessEndPoint(in EndPoint newEp)
        {
            if (!newEp.SameAs(_clientEndPoint))
            {
                if (_clientEndPoint != null)
                    SendTo(_clientEndPoint, PacketFromMessage(Message.Hijack));

                SendTo(newEp, PacketFromMessage(Message.Connect));
                _clientEndPoint = newEp;
                StopOutput = false;
                return;
            }

            StopOutput = !StopOutput;
            SendTo(_clientEndPoint, PacketFromMessage(StopOutput ? Message.Stop : Message.Start));
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
                    _clientListenerPacket.Dispose();
                    _packetPool.Dispose();
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
