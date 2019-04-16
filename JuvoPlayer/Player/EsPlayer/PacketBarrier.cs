/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
 * Copyright 2019, Samsung Electronics Co., Ltd
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
 *
 */

using System;
using JuvoPlayer.Common;

namespace JuvoPlayer.Player.EsPlayer
{
    public class PacketBarrier
    {
        private TimeSpan _firstPts;
        private TimeSpan _currentTransfer;
        private readonly TimeSpan _timeFrame;
        private DateTime _startTime;

        public PacketBarrier(TimeSpan timeFrame)
        {
            _timeFrame = timeFrame;
        }

        public void PacketPushed(Packet packet)
        {
            if (!packet.ContainsData())
                return;

            if (_startTime == default(DateTime))
                _startTime = DateTime.Now;

            if (_firstPts == default(TimeSpan))
                _firstPts = packet.Pts;
            else
                _currentTransfer = packet.Pts - _firstPts;
        }

        public bool Reached()
        {
            if (_startTime == default(DateTime))
                return false;
            return _currentTransfer >= _timeFrame;
        }

        public TimeSpan TimeToNextFrame()
        {
            if (!Reached())
                throw new InvalidOperationException("Cannot calculate time to the next time frame before reaching the barrier");

            return _currentTransfer - (DateTime.Now - _startTime);
        }

        public void Reset()
        {
            _firstPts = default(TimeSpan);
            _startTime = default(DateTime);
            _currentTransfer = default(TimeSpan);
        }
    }
}