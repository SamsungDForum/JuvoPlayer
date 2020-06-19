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
            private readonly CancellationToken _token;
            private readonly ChannelReader<object[]> _reader;

            public LineReader(in ChannelReader<object[]> reader, in CancellationToken token)
            {
                _token = token;
                _reader = reader;
            }

            public async ValueTask<(bool flush, object[] lineData)> Read(bool flushWhenNoData)
            {
                if (!_reader.TryRead(out var lineData))
                {
                    if (flushWhenNoData)
                        return (true, null);

                    lineData = await _reader.ReadAsync(_token);
                }

                return (false, lineData);
            }
        }

        private const float SendThreshold = 0.87f;
        private readonly Channel<object[]> _logChannel;
        private readonly CancellationTokenSource _cts;
        private SocketSession _socketSession;
        public bool StopOutput { get => _socketSession?.StopOutput ?? true; }

        public UdpLoggerService()
        {
            _cts = new CancellationTokenSource();
            _logChannel = Channel.CreateUnbounded<object[]>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public void Log(params object[] args)
        {
            _logChannel.Writer.TryWrite(args);
        }

        public async Task StartLogger(int udpPort, string logFormat)
        {
            try
            {
                _socketSession = new SocketSession(_cts.Token);
                _socketSession.Start(udpPort);
                await Task.Run(async () => await Run(logFormat));
            }
            catch (Exception e)
            when (!(e is OperationCanceledException || e is ChannelClosedException))
            {
                _socketSession.SendMessage(e.ToString());
                throw;
            }
            finally
            {
                _socketSession?.Stop();
                _socketSession?.Dispose();
                _socketSession = null;
                _logChannel.Writer.Complete();
            }
        }

        private async Task Run(string logFormat)
        {
            var maxMtu = _socketSession.MaxPayloadSize;
            int sendThresholdValue = (int)(maxMtu * SendThreshold);
            var message = new StringBuilder(maxMtu);
            var lineReader = new LineReader(_logChannel.Reader, _cts.Token);
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                var (flush, lineData) = await lineReader.Read(message.Length > 0);

                if (flush)
                {
                    _socketSession.SendMessage(message.ToString());
                    message.Clear();
                    continue;
                }

                message.AppendFormat(logFormat, lineData);
                if (message.Length < sendThresholdValue)
                    continue;

                _socketSession.SendMessage(message.ToString());
                message.Clear();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
