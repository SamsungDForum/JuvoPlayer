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
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Linq;
using Nito.AsyncEx;
using System.Diagnostics;

namespace JuvoLogger.Udp
{
    internal class SocketSession : IDisposable
    {
        public bool PauseOutput { get; private set; } = true;
        public int MaxPayloadSize { get; private set; }

        private static readonly byte[] ConnectMessage = Encoding.UTF8.GetBytes(Resources.Messages.ConnectMessage);
        private static readonly byte[] PauseMessage = Encoding.UTF8.GetBytes(Resources.Messages.PauseMessage);
        private static readonly byte[] ResumeMessage = Encoding.UTF8.GetBytes(Resources.Messages.ResumeMessage);
        private static readonly byte[] TerminateMessage = Encoding.UTF8.GetBytes(Resources.Messages.TerminationMessage);
        private static readonly byte[] HijackMessage = Encoding.UTF8.GetBytes(Resources.Messages.HijackMessage);

        private readonly CancellationToken _token;
        private EndPoint _currentEndPoint;
        private Socket _socket;
        private readonly Channel<EndPoint> _endPointChannel;
        private readonly Channel<string> _messageChannel;
        private const int MaxUdpHeader = 48; // IP6 UDP header.
        private const int MaxPacketBuffers = 64;
        private static readonly TimeSpan BufferFlushInterval = TimeSpan.FromMilliseconds(300);

        public SocketSession(CancellationToken token)
        {
            _token = token;
            MaxPayloadSize = UdpLoggerToolBox.GetLowestCommonMtu() - MaxUdpHeader;
            _endPointChannel = Channel.CreateUnbounded<EndPoint>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = true
            });
            _messageChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = true
            });
        }

        public async Task Stop()
        {
            if (_currentEndPoint != null)
                await WriteToSocket(_currentEndPoint, TerminateMessage, TerminateMessage.Length, new AsyncAutoResetEvent());

            _endPointChannel.Writer.Complete();
            _messageChannel.Writer.Complete();

            _socket.Close();
        }

        public void Start(int port)
        {
            _socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));

            Task.Run(async () => await EndPointListener());
            Task.Run(async () => await SocketWriter());
        }

        public ValueTask SendMessage(in string message) =>
            _messageChannel.Writer.WriteAsync(message, _token);

        public void TryLogException(Exception e, in string message = null)
        {
            if (_currentEndPoint == null)
                return;

            var exceptionData = message == null ? e.ToString() + e.StackTrace : message + e.ToString() + e.StackTrace;
            var pb = new PacketBuffer(this);
            pb.Append(exceptionData);
            pb.Flush();
        }

        public Task WriteToSocket(byte[] data, int length, AsyncAutoResetEvent readyToWrite) =>
            WriteToSocket(_currentEndPoint, data, length, readyToWrite);

        private async Task WriteToSocket(EndPoint destinationEp, byte[] data, int length, AsyncAutoResetEvent readyToWrite)
        {
            var asyncResult = _socket.BeginSendTo(data, 0, length, SocketFlags.None, destinationEp, OnSocketReady, readyToWrite);
            await readyToWrite.WaitAsync(_token);
            _socket.EndSendTo(asyncResult);
        }

        private static void OnSocketReady(IAsyncResult asyncResult) =>
            ((AsyncAutoResetEvent)asyncResult.AsyncState).Set();

        private async Task EndPointListener()
        {
            AsyncAutoResetEvent readToRead = new AsyncAutoResetEvent();
            var buffer = new byte[1];
            try
            {
                while (true)
                {
                    var clientEp = await ReadEndPointFromSocket(buffer, readToRead);
                    await ProcessEndPoint(clientEp);
                }
            }
            catch (Exception e)
            when (!(e is OperationCanceledException))
            {
                TryLogException(e);
                throw;
            }
        }

        private Task ProcessEndPoint(EndPoint newEp)
        {
            if (!newEp.SameAs(_currentEndPoint))
            {
                var hijackSendTask = _currentEndPoint != null
                    ? WriteToSocket(_currentEndPoint, HijackMessage, HijackMessage.Length, new AsyncAutoResetEvent())
                    : Task.CompletedTask;

                var connectSendTask = WriteToSocket(newEp, ConnectMessage, ConnectMessage.Length, new AsyncAutoResetEvent());

                _currentEndPoint = newEp;
                PauseOutput = false;
                return Task.WhenAll(hijackSendTask, connectSendTask);
            }

            PauseOutput = !PauseOutput;
            var pauseResumeMsg = PauseOutput ? PauseMessage : ResumeMessage;
            return WriteToSocket(_currentEndPoint, pauseResumeMsg, pauseResumeMsg.Length, new AsyncAutoResetEvent());
        }

        private async Task<EndPoint> ReadEndPointFromSocket(byte[] buffer, AsyncAutoResetEvent readyToRead)
        {
            EndPoint clientEp = new IPEndPoint(IPAddress.Any, 0);
            var asyncResult = _socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref clientEp, OnSocketReady, readyToRead);
            await readyToRead.WaitAsync(_token);
            _socket.EndReceiveFrom(asyncResult, ref clientEp);
            return clientEp;
        }

        private async Task SocketWriter()
        {
            var packetBuffers = new PacketBuffer[MaxPacketBuffers];
            for (var i = 0; i < MaxPacketBuffers; i++)
                packetBuffers[i] = new PacketBuffer(this);

            var readyBuffers = packetBuffers.Select(pb => pb.Flushing);
            PacketBuffer currentBuffer = packetBuffers[0];
            var reader = _messageChannel.Reader;
            var stopWatch = Stopwatch.StartNew();

            try
            {
                while (true)
                {
                    if (!reader.TryRead(out var message))
                    {
                        currentBuffer = currentBuffer?.Flush();
                        stopWatch.Stop();
                        stopWatch.Reset();
                        while (!await reader.WaitToReadAsync(_token)) ;
                        stopWatch.Start();
                        continue;
                    }

                    if (currentBuffer == null)
                        currentBuffer = await await Task.WhenAny(readyBuffers);

                    if (!currentBuffer.Append(message))
                    {
                        currentBuffer.Flush();
                        stopWatch.Restart();
                        currentBuffer = await await Task.WhenAny(readyBuffers);
                        currentBuffer.Append(message);
                        continue;
                    }

                    if (stopWatch.Elapsed > BufferFlushInterval)
                    {
                        currentBuffer = currentBuffer.Flush();
                        stopWatch.Restart();
                    }
                }
            }
            catch (Exception e)
            when (!(e is OperationCanceledException))
            {
                TryLogException(e);
                throw;
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
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
