/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
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
 *
 */

using System;

namespace JuvoPlayer.Dash.MPD
{
    public class SingleSegmentIndex : ISegmentIndex
    {
        private readonly RangedUri _uri;

        public SingleSegmentIndex(RangedUri uri)
        {
            _uri = uri;
        }

        public long? GetSegmentCount(TimeSpan? periodDuration)
        {
            return 1;
        }

        public long GetSegmentNum(TimeSpan time, TimeSpan? periodDuration)
        {
            return 0;
        }

        public TimeSpan GetStartTime(long segmentNum)
        {
            return TimeSpan.Zero;
        }

        public TimeSpan? GetDuration(long segmentNum, TimeSpan? periodDuration)
        {
            return periodDuration;
        }

        public RangedUri GetSegmentUrl(long segmentNum)
        {
            return _uri;
        }

        public long GetFirstSegmentNum()
        {
            return 0;
        }
    }
}
