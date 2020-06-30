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
using System.Threading;

namespace JuvoLogger.Udp
{
    internal class SocketSession : IDisposable
    {
        public bool StopOutput { get; private set; } = true;
        public int MaxPayloadSize { get; } = UdpLoggerToolBox.GetLowestCommonMtu() - MaxUdpHeader;

        private enum Message { Connect, Stop, Start, Terminate, Hijack };
        // Any way to get processed (encoded) byte arrays directly from resources?
        private readonly byte[][] _messageData = UdpLoggerToolBox.ConvertAll(Encoding.UTF8.GetBytes,
                Resources.Messages.ConnectMessage,
                Resources.Messages.StopMessage,
                Resources.Messages.StartMessage,
                Resources.Messages.TerminationMessage,
                Resources.Messages.HijackMessage);

        private const int MaxUdpHeader = 48; // IP6 UDP header.
        private readonly Socket _socket;
        private readonly EndPoint _clientEndPoint = new IPEndPoint(IPAddress.None, 0);
        private readonly UdpPacket _clientListenerPacket;
        private UdpPacket _clientDataPacket;
        private readonly CancellationToken _sessionToken;

        public SocketSession(CancellationToken token)
        {
            _sessionToken = token;
            _socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            _clientListenerPacket = new UdpPacket(1, OnConnected, _sessionToken);
            ((SocketAsyncEventArgs)_clientListenerPacket).RemoteEndPoint = new IPEndPoint(IPAddress.None, 0);
        }

        public void Start(int port)
        {
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));
            ReceiveFromEndPoint(_clientListenerPacket);
        }

        public void Stop()
        {
            try
            {
                StopOutput = true;
                if (_clientEndPoint.IsAddressValid())
                    _socket.SendTo(_messageData[(int)Message.Terminate], _clientEndPoint);
            }
            catch (Exception)
            { /* Ignore on stop */}
            finally
            {
                _clientEndPoint.InvalidateAddress();
            }
        }

        public void SendMessage(in string message)
        {
            if (!_clientEndPoint.IsAddressValid())
                return;

            // Resulting messgeBytes can be up to x4 message.Length
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var msgLength = messageBytes.Length;
            var mtu = MaxPayloadSize;
            int bytesSent = 0;

            do
            {
                var packet = GetPacket();
                var payloadSize = Math.Min(msgLength - bytesSent, mtu);
                packet.SetBuffer(messageBytes, bytesSent, payloadSize);
                SendTo(_clientEndPoint, packet);
                bytesSent += payloadSize;
            } while (msgLength > bytesSent);
        }

        private UdpPacket GetPacket()
        {
            return Interlocked.Exchange(ref _clientDataPacket, null) ?? new UdpPacket(OnCompleted, _sessionToken, ReturnPacket);
        }
        private void ReturnPacket(UdpPacket packet)
        {
            if (Interlocked.CompareExchange(ref _clientDataPacket, packet, null) != null)
                packet.Dispose();
        }

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
            if (((UdpPacket)asyncState).IsTerminated)
                return;

            if (asyncState.SocketError == SocketError.Success)
                ProcessEndPoint(asyncState.RemoteEndPoint);

            ReceiveFromEndPoint(asyncState);
        }

        private void ReceiveFromEndPoint(SocketAsyncEventArgs asyncState)
        {
            if (((UdpPacket)asyncState).IsTerminated)
                return;

            try
            {
                asyncState.SetBuffer(asyncState.Offset, asyncState.Buffer.Length);
                if (!_socket.ReceiveFromAsync(asyncState))
                    UdpPacket.CompleteAsync(asyncState);
            }
            catch (Exception e)
            when (!_sessionToken.IsCancellationRequested)
            {
                SendMessage(e.ToString());
                throw;
            }
        }

        private void SendTo(in EndPoint target, in UdpPacket buffer)
        {
            if (buffer.IsTerminated)
                return;

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
                _clientEndPoint.InvalidateAddress();
            }
        }

        // Run fixed messages buffers outside of packet pool. Not expecting that much traffic here.
        // Less fiddling then buffer restore / message copying.
        private UdpPacket PacketFromMessage(in Message msg) =>
            new UdpPacket(_messageData[(int)msg], OnCompleted, _sessionToken, UdpLoggerToolBox.Dispose);

        private void ProcessEndPoint(in EndPoint newEp)
        {
            if (!newEp.SameAs(_clientEndPoint))
            {
                if (_clientEndPoint.IsAddressValid())
                    SendTo(_clientEndPoint, PacketFromMessage(Message.Hijack));

                SendTo(newEp, PacketFromMessage(Message.Connect));
                newEp.CopyTo(_clientEndPoint);
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
