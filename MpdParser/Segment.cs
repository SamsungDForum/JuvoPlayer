/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
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

namespace MpdParser.Node.Dynamic
{
    public class TimeRange
    {
        public readonly TimeSpan Start;
        public readonly TimeSpan Duration;

        public TimeRange(TimeSpan start, TimeSpan duration)
        {
            Start = start;
            Duration = duration;
        }

        /// <summary>
        /// Clones TimeRange Object with Start/Duration TimeSpan preservation.
        /// </summary>
        /// <returns>TimeRange. A Cloned TimeRange object</returns>
        public TimeRange Copy()
        {
            return (TimeRange)this.MemberwiseClone();
        }

        public override string ToString()
        {
            return $"({Start})-({Duration})";
        }

    }
    public enum TimeRelation
    {
        UNKNOWN = -3,
        OVERLAP,
        EARLIER,
        SPOTON,
        LATER
    }
   
    public class Segment
    {
        public readonly Uri Url;
        public readonly string ByteRange;
        public readonly TimeRange Period;

        public Segment(Uri url, string range, TimeRange period = null)
        {
            Url = url;
            ByteRange = range;
            Period = period;
        }

        internal TimeRelation Contains(TimeSpan timePoint)
        {
            if (Period == null)
                return TimeRelation.UNKNOWN;
            if (timePoint < Period.Start)
                return TimeRelation.LATER;
            timePoint -= Period.Start;
            return timePoint <= Period.Duration ? TimeRelation.SPOTON : TimeRelation.EARLIER;
        }
    }
}
