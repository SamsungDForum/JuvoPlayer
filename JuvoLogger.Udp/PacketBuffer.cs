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

using Nito.AsyncEx;
using System;
using System.Text;
using System.Threading.Tasks;

namespace JuvoLogger.Udp
{
    internal class PacketBuffer
    {
        public Task<PacketBuffer> Flushing { get; private set; }

        private readonly byte[] _buffer;
        private int _bufferSize = 0;
        private readonly SocketSession _socketSession;
        private readonly AsyncAutoResetEvent _readToWrite = new AsyncAutoResetEvent();

        public PacketBuffer(SocketSession socketSession)
        {
            _buffer = new byte[socketSession.MaxPayloadSize];
            _socketSession = socketSession;
            Flushing = Task.FromResult(this);
        }

        public bool Append(in string data)
        {
            // Truncate input if needed
            var dataLength = Math.Min(data.Length, _buffer.Length);

            if (_bufferSize + data.Length > _buffer.Length)
                return false;

            Encoding.UTF8.GetBytes(data, 0, dataLength, _buffer, _bufferSize);
            _bufferSize += dataLength;
            return true;
        }

        public PacketBuffer Flush()
        {
            if (_bufferSize == 0)
                return this;

            Flushing = FlushInternal();
            return null;
        }

        private async Task<PacketBuffer> FlushInternal()
        {
            await _socketSession.WriteToSocket(_buffer, _bufferSize, _readToWrite);
            _bufferSize = 0;
            return this;
        }
    }
}
