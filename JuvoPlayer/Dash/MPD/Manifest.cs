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
using System.Collections.Generic;

namespace JuvoPlayer.Dash.MPD
{
    public class Manifest
    {
        public Manifest(TimeSpan? mediaPresentationDuration, DateTime? availabilityStartTime, DateTime? publishTime,
            TimeSpan? minBufferTime, TimeSpan? timeShiftBufferDepth, TimeSpan? suggestedPresentationDelay,
            TimeSpan? minimumUpdatePeriod,
            ProgramInformation programInformation, UtcTiming utcTiming, IList<Period> periods, bool dynamic,
            Uri location)
        {
            MediaPresentationDuration = mediaPresentationDuration;
            AvailabilityStartTime = availabilityStartTime;
            PublishTime = publishTime;
            MinBufferTime = minBufferTime;
            TimeShiftBufferDepth = timeShiftBufferDepth;
            SuggestedPresentationDelay = suggestedPresentationDelay;
            MinimumUpdatePeriod = minimumUpdatePeriod;
            ProgramInformation = programInformation;
            UtcTiming = utcTiming;
            Periods = periods;
            Dynamic = dynamic;
            Location = location;
        }

        public TimeSpan? MediaPresentationDuration { get; }
        public DateTime? AvailabilityStartTime { get; }
        public DateTime? PublishTime { get; }
        public TimeSpan? MinBufferTime { get; }
        public TimeSpan? TimeShiftBufferDepth { get; }
        public TimeSpan? SuggestedPresentationDelay { get; }
        public TimeSpan? MinimumUpdatePeriod { get; }
        public ProgramInformation ProgramInformation { get; }
        public UtcTiming UtcTiming { get; }
        public IList<Period> Periods { get; }
        public bool Dynamic { get; }
        public Uri Location { get; }

        public TimeSpan? GetPeriodDuration(int index)
        {
            return index == Periods.Count - 1
                ? MediaPresentationDuration - Periods[index].Start
                : Periods[index + 1].Start - Periods[index].Start;
        }

        public TimeSpan? GetPeriodDuration(Period period)
        {
            var periodIndex = Periods.IndexOf(period);
            return GetPeriodDuration(periodIndex);
        }
    }
}
