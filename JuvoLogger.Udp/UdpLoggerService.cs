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
        private Channel<object[]> _logChannel;
        private CancellationTokenSource _cts;

        public UdpLoggerService(int udpPort, string logFormat)
        {
            _cts = new CancellationTokenSource();

            _logChannel = Channel.CreateUnbounded<object[]>(new UnboundedChannelOptions()
            {
                SingleReader = true,
                SingleWriter = false
            });

            Task.Run(async () => await Run(udpPort, logFormat));
        }

        public void Log(params object[] args)
        {
            _logChannel.Writer.TryWrite(args);
        }

        private async Task Run(int port, string logFormat)
        {
            using (var socketSession = new SocketSession(_cts.Token))
            {
                try
                {
                    socketSession.Start(port);
                    var messageBuilder = new StringBuilder(socketSession.MaxPayloadSize);
                    var reader = _logChannel.Reader;

                    while (true)
                    {
                        var msg = await reader.ReadAsync(_cts.Token);
                        if (socketSession.PauseOutput)
                            continue;

                        messageBuilder.AppendFormat(logFormat, msg);
                        await socketSession.SendMessage(messageBuilder.ToString());
                        messageBuilder.Clear();
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
                    _cts?.Cancel();
                    _logChannel.Writer.Complete();
                    await socketSession.Stop();
                }
            }
        }

        public void Dispose()
        {
            var currentCts = _cts;
            _cts = null;
            currentCts.Cancel();
            currentCts.Dispose();
        }
    }
}
