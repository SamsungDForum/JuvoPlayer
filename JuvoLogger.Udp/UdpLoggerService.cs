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
        private struct LineSelector
        {
            private readonly LineReader[] _readers;
            private readonly ValueTask<bool>[] _results;
            private int _readerIdx;

            public LineSelector(params LineReader[] readers)
            {
                _readers = readers;
                _results = new ValueTask<bool>[_readers.Length];
                _readerIdx = 0;
            }
            public int Read(in bool flushWhenNoData)
            {
                _readerIdx = ++_readerIdx % _readers.Length;
                _results[_readerIdx] = _readers[_readerIdx].Read(flushWhenNoData);
                return _readerIdx;
            }

            public async ValueTask<(bool flush, StringBuilder lineData)> GetData(int idx)
            {
                var flush = await _results[idx];
                var lineData = _readers[idx].Get();
                return (flush, lineData);
            }

        }
        private readonly struct LineReader
        {
            private readonly StringBuilder _builder;
            private readonly CancellationToken _token;
            private readonly ChannelReader<object[]> _reader;
            private readonly string _format;
            public StringBuilder Get() => _builder;

            public LineReader(in ChannelReader<object[]> reader, in int initialSize, in string format, in CancellationToken token)
            {
                _builder = new StringBuilder(initialSize);
                _token = token;
                _reader = reader;
                _format = format;
            }
            public async ValueTask<bool> Read(bool flushWhenNoData)
            {
                _builder.Clear();

                if (!_reader.TryRead(out var lineData))
                {
                    if (flushWhenNoData)
                        return true;

                    lineData = await _reader.ReadAsync(_token);
                }

                _builder.AppendFormat(_format, lineData);
                return false;
            }
        }

        private readonly Channel<object[]> _logChannel;
        private readonly CancellationTokenSource _cts;
        private readonly string _logFormat;

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

                // For issuing "early reads" before data processing.
                // Even with... observed saturation of object pool is 4-5 mtu buffers.. :-/
                var lineSelector = new LineSelector(
                    new LineReader(_logChannel.Reader, maxMtu, _logFormat, _cts.Token),
                    new LineReader(_logChannel.Reader, maxMtu, _logFormat, _cts.Token));
                var currentLine = lineSelector.Read(false);
                var message = new StringBuilder(maxMtu);

                try
                {
                    while (true)
                    {
                        var (flush, lineData) = await lineSelector.GetData(currentLine);

                        if (socketSession.StopOutput)
                        {
                            currentLine = lineSelector.Read(false);
                            // Flush current message if present
                            if (message.Length > 0)
                            {
                                socketSession.SendMessage(message.ToString());
                                message.Clear();
                            }

                            continue;
                        }

                        if (flush)
                        {
                            currentLine = lineSelector.Read(false);
                            socketSession.SendMessage(message.ToString());
                            message.Clear();

                            continue;
                        }

                        currentLine = lineSelector.Read(true);

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
                    socketSession?.TryLogException(e);
                    throw;
                }
                finally
                {
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
