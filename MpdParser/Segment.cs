// Copyright (c) 2018 Samsung Electronics Co., Ltd All Rights Reserved
// PROPRIETARY/CONFIDENTIAL 
// This software is the confidential and proprietary
// information of SAMSUNG ELECTRONICS ("Confidential Information"). You shall
// not disclose such Confidential Information and shall use it only in
// accordance with the terms of the license agreement you entered into with
// SAMSUNG ELECTRONICS. SAMSUNG make no representations or warranties about the
// suitability of the software, either express or implied, including but not
// limited to the implied warranties of merchantability, fitness for a
// particular purpose, or non-infringement. SAMSUNG shall not be liable for any
// damages suffered by licensee as a result of using, modifying or distributing
// this software or its derivatives.

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

        internal TimeRelation Contains(TimeSpan time_point)
        {
            if (Period == null || time_point == null)
                return TimeRelation.UNKNOWN;
            if (time_point < Period.Start)
                return TimeRelation.LATER;
            time_point -= Period.Start;
            return time_point <= Period.Duration ? TimeRelation.SPOTON : TimeRelation.EARLIER;
        }
    }
}
