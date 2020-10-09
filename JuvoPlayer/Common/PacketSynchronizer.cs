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
using Nito.AsyncEx;

namespace JuvoPlayer.Common
{
    public class PacketSynchronizer
    {
        private readonly Func<TimeSpan, CancellationToken, Task> _delayer;
        private Packet _currentPacket;
        private AsyncCollection<Packet> _packets;

        public PacketSynchronizer(Func<TimeSpan, CancellationToken, Task> delayer = default)
        {
            _packets = new AsyncCollection<Packet>();
            _delayer = delayer ?? Task.Delay;
        }

        public IClock Clock { get; set; }
        public Segment Segment { get; set; }
        public TimeSpan Offset { get; set; }

        public void Add(Packet packet)
        {
            _packets.Add(packet);
        }

        public async Task<Packet> TakeAsync(CancellationToken cancellationToken = default)
        {
            if (_currentPacket == null)
            {
                _currentPacket = await _packets.TakeAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!(_currentPacket is EosPacket))
            {
                var packetClockTime = Segment.ToClockTime(_currentPacket.Pts);
                var timeToPushPacket = packetClockTime - Clock.Elapsed - Offset;
                if (timeToPushPacket > TimeSpan.Zero)
                {
                    await _delayer.Invoke(timeToPushPacket, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            var currentPacket = _currentPacket;
            _currentPacket = null;
            return currentPacket;
        }

        public void Flush()
        {
            var events = _packets;
            events.CompleteAdding();
            foreach (var @event in events.GetConsumingEnumerable())
                @event.Dispose();

            _currentPacket?.Dispose();
            _currentPacket = null;
            _packets = new AsyncCollection<Packet>();
        }
    }
}
