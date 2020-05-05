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
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace JuvoLogger.Udp
{
    internal class UdpLoggerService : IDisposable
    {
        private readonly struct LineReader
        {
            private readonly StringBuilder _builder;
            private readonly CancellationToken _token;
            private readonly ChannelReader<object[]> _reader;
            private readonly string _format;

            public LineReader(in ChannelReader<object[]> reader, in int initialSize, in string format, in CancellationToken token)
            {
                _builder = new StringBuilder(initialSize);
                _token = token;
                _reader = reader;
                _format = format;
            }
           
            public async ValueTask<(bool flush,StringBuilder lineData)> Read(bool flushWhenNoData)
            {
                if (!_reader.TryRead(out var lineData))
                {
                    if (flushWhenNoData)
                        return (true, null);

                    lineData = await _reader.ReadAsync(_token);
                }
                _builder.Clear();
                return (false, _builder.AppendFormat(_format, lineData));
            }
        }

        private readonly Channel<object[]> _logChannel;
        private readonly CancellationTokenSource _cts;
        private readonly string _logFormat;
        public bool StopOutput { get => _socketSession?.StopOutput ?? true; }
        private SocketSession _socketSession;

        public UdpLoggerService(int udpPort, string logFormat)
        {

            _cts = new CancellationTokenSource();
            _logChannel = Channel.CreateUnbounded<object[]>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = false
            });
            _logFormat = logFormat;
            Task.Run(async () => await Run(udpPort));
        }

        public void Log(params object[] args)
        {
            _logChannel.Writer.TryWrite(args);
        }

        private async Task Run(int udpPort)
        {
            using (var socketSession = new SocketSession(udpPort))
            {
                var maxMtu = socketSession.MaxPayloadSize;
                var message = new StringBuilder(maxMtu);
                _socketSession = socketSession;

                var lineReader = new LineReader(_logChannel.Reader, maxMtu, _logFormat, _cts.Token);

                try
                {
                    while (true)
                    {
                        var (flush, lineData) = await lineReader.Read(message.Length > 0);

                        if (flush)
                        {
                            socketSession.SendMessage(message.ToString());
                            message.Clear();
                            continue;
                        }
                        
                        if (lineData.Length + message.Length < maxMtu)
                        {
                            message.Append(lineData);
                            continue;
                        }

                        socketSession.SendMessage(message.ToString());
                        message.Clear();
                        message.Append(lineData);
                    }
                }
                catch (Exception e)
                when (!(e is OperationCanceledException))
                {
                    socketSession?.SendMessage(e.ToString());
                    throw;
                }
                finally
                {
                    _socketSession = null;
                    socketSession.Stop();
                }
            }
        }

        public void Dispose()
        {
            _logChannel.Writer.Complete();
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
