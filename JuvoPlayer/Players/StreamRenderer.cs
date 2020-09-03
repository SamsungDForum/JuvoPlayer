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
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;

namespace JuvoPlayer.Players
{
    public class StreamRenderer : IStreamRenderer
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");
        private readonly PacketSynchronizer _packetSynchronizer;
        private CancellationTokenSource _cancellationTokenSource;

        public StreamRenderer(
            PacketSynchronizer packetSynchronizer)
        {
            _packetSynchronizer = packetSynchronizer;
        }

        public bool IsPushingPackets { get; private set; }

        public void OnPacketReady(Packet packet)
        {
            _logger.Info($"{packet.StreamType} {packet.Pts}");
            _packetSynchronizer.Add(packet);
        }

        public void OnDrmDataReady(DrmInitData drmInitData)
        {
            throw new NotImplementedException();
        }

        public async Task StartPushingPackets(
            Segment segment,
            IPlatformPlayer platformPlayer,
            CancellationToken externalCancellationToken)
        {
            try
            {
                if (IsPushingPackets)
                    return;
                IsPushingPackets = true;

                _packetSynchronizer.Segment = segment;

                _cancellationTokenSource = new CancellationTokenSource();
                using (var linkedCancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        _cancellationTokenSource.Token,
                        externalCancellationToken))
                {
                    var cancellationToken = linkedCancellationTokenSource.Token;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        using (var packet = await _packetSynchronizer.TakeAsync(cancellationToken))
                        {
                            var result = platformPlayer.SubmitPacket(packet);
                            _logger.Info($"{packet.StreamType} {packet.Pts} {result}");
                            if (result != SubmitResult.Success)
                            {
                                throw new NotImplementedException(
                                    "TODO: Handle other results");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        public void StopPushingPackets()
        {
            _logger.Info();
            _cancellationTokenSource?.Cancel();
            IsPushingPackets = false;
        }

        public void Flush()
        {
            _logger.Info();
            _packetSynchronizer.Flush();
        }
    }
}
