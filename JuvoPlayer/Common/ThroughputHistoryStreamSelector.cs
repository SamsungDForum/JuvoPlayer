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

using System.Linq;

namespace JuvoPlayer.Common
{
    public class ThroughputHistoryStreamSelector : IStreamSelector
    {
        private readonly IThroughputHistory _throughputHistory;

        public ThroughputHistoryStreamSelector(IThroughputHistory throughputHistory)
        {
            _throughputHistory = throughputHistory;
        }

        public int Select(StreamGroup streamGroup)
        {
            var averageThroughput = _throughputHistory.GetAverageThroughput();
            var streams = streamGroup.Streams;
            var stream = streams
                             .OrderByDescending(streamInfo => streamInfo.Format.Bitrate)
                             .FirstOrDefault(streamInfo => streamInfo.Format.Bitrate < averageThroughput)
                         ?? streams.Last();
            return streams.IndexOf(stream);
        }

        protected bool Equals(ThroughputHistoryStreamSelector other)
        {
            return Equals(_throughputHistory, other._throughputHistory);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((ThroughputHistoryStreamSelector)obj);
        }

        public override int GetHashCode()
        {
            return _throughputHistory != null ? _throughputHistory.GetHashCode() : 0;
        }
    }
}
