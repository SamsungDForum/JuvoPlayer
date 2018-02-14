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
    }

    enum TimeRelation
    {
        UNKNOWN = -2,
        EARLIER,
        SPOTON,
        LATER
    }
    public class Segment
    {
        public readonly Uri Url;
        public readonly string ByteRange;
        public readonly TimeRange Period;
        public readonly bool EndSegment;

        public Segment(Uri url, string range, TimeRange period = null, bool eof = false)
        {
            Url = url;
            ByteRange = range;
            Period = period;
            EndSegment = eof;
        }

        internal TimeRelation Contains(TimeSpan time_point)
        {
            if (Period == null)
                return TimeRelation.UNKNOWN;
            if (time_point < Period.Start)
                return TimeRelation.LATER;
            time_point -= Period.Start;
            return time_point <= Period.Duration ? TimeRelation.SPOTON : TimeRelation.EARLIER;
        }
    }
}