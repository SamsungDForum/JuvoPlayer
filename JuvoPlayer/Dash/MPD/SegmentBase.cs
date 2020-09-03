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
    public class SegmentBase
    {
        public SegmentBase(RangedUri initialization, long timeScale, long presentationTimeOffset)
        {
            Initialization = initialization;
            TimeScale = timeScale;
            PresentationTimeOffset = presentationTimeOffset;
        }

        public RangedUri Initialization { get; }
        public long TimeScale { get; }
        public long PresentationTimeOffset { get; }

        public virtual RangedUri GetInitialization(Representation representation)
        {
            return Initialization;
        }

        public TimeSpan GetPresentationTimeOffset()
        {
            return TimeSpan.FromSeconds(PresentationTimeOffset / (double)TimeScale);
        }
    }

    public class SingleSegmentBase : SegmentBase
    {
        public SingleSegmentBase(RangedUri initialization, long timeScale, long presentationTimeOffset, long indexStart,
            long indexLength) : base(initialization, timeScale, presentationTimeOffset)
        {
            IndexStart = indexStart;
            IndexLength = indexLength;
        }

        public SingleSegmentBase()
            : this(null, 1, 0, 0, 0)
        {
        }

        public long IndexStart { get; }
        public long IndexLength { get; }

        public RangedUri GetIndex()
        {
            return IndexLength <= 0 ? null : new RangedUri(null, IndexStart, IndexLength);
        }
    }

    public abstract class MultiSegmentBase : SegmentBase
    {
        public MultiSegmentBase(RangedUri initialization, long timeScale, long presentationTimeOffset, long startNumber,
            long? duration, SegmentTimeline segmentTimeline) : base(
            initialization, timeScale, presentationTimeOffset)
        {
            StartNumber = startNumber;
            Duration = duration;
            SegmentTimeline = segmentTimeline;
        }

        public long StartNumber { get; }
        public long? Duration { get; }
        public SegmentTimeline SegmentTimeline { get; }

        public long GetSegmentNum(TimeSpan time, TimeSpan? periodDuration)
        {
            long? segmentCount = GetSegmentCount(periodDuration);
            if (segmentCount.HasValue && segmentCount == 0)
                return StartNumber;
            if (SegmentTimeline == null)
            {
                var duration = TimeSpan.FromSeconds(Duration.Value / (double)TimeScale);
                var segmentNum = StartNumber + (long)(time.TotalMilliseconds / duration.TotalMilliseconds);
                return segmentNum < StartNumber ? StartNumber :
                    !segmentCount.HasValue ? segmentNum : Math.Min(segmentNum, StartNumber + segmentCount.Value - 1);
            }

            var index = StartNumber;
            var maxIndex = StartNumber + segmentCount.Value;
            for (; index < maxIndex; index++)
            {
                var startTime = GetSegmentStartTime(index);
                if (time < startTime)
                    return index;
                var duration = GetSegmentDuration(index, periodDuration);
                if (time < startTime + duration)
                    return index;
            }

            return maxIndex - 1;
        }

        public TimeSpan GetSegmentStartTime(long sequenceNumber)
        {
            long segmentTime;
            if (SegmentTimeline != null)
            {
                segmentTime = SegmentTimeline.Get((int)(sequenceNumber - StartNumber)).StartTime -
                              PresentationTimeOffset;
            }
            else
            {
                segmentTime = (sequenceNumber - StartNumber) * Duration.Value;
            }

            return TimeSpan.FromSeconds(segmentTime / (double)TimeScale);
        }

        public TimeSpan GetSegmentDuration(long sequenceNumber, TimeSpan? periodDuration)
        {
            if (SegmentTimeline != null)
            {
                var duration = SegmentTimeline.Get((int)(sequenceNumber - StartNumber)).Duration;
                return TimeSpan.FromSeconds(duration / (double)TimeScale);
            }

            var segmentCount = GetSegmentCount(periodDuration);
            return segmentCount.HasValue && sequenceNumber == StartNumber + segmentCount - 1
                ? periodDuration.Value - GetSegmentStartTime(sequenceNumber)
                : TimeSpan.FromSeconds(Duration.Value / (double)TimeScale);
        }

        public abstract RangedUri GetSegmentUrl(Representation representation, long index);
        public abstract int? GetSegmentCount(TimeSpan? periodDuration);
    }

    public class SegmentList : MultiSegmentBase
    {
        public SegmentList(RangedUri initialization, long timeScale, long presentationTimeOffset, long startNumber,
            long? duration, SegmentTimeline segmentTimeline, IList<RangedUri> mediaSegments) : base(initialization,
            timeScale, presentationTimeOffset, startNumber, duration, segmentTimeline)
        {
            MediaSegments = mediaSegments;
        }

        public IList<RangedUri> MediaSegments { get; }

        public override RangedUri GetSegmentUrl(Representation representation, long index)
        {
            return MediaSegments[(int)(index - StartNumber)];
        }

        public override int? GetSegmentCount(TimeSpan? periodDuration)
        {
            return MediaSegments.Count;
        }
    }

    public class SegmentTemplate : MultiSegmentBase
    {
        public SegmentTemplate(RangedUri initialization, long timeScale, long presentationTimeOffset, long startNumber,
            long? duration, SegmentTimeline segmentTimeline, UrlTemplate initializationTemplate,
            UrlTemplate mediaTemplate, long? endNumber) : base(initialization, timeScale, presentationTimeOffset,
            startNumber, duration, segmentTimeline)
        {
            InitializationTemplate = initializationTemplate;
            MediaTemplate = mediaTemplate;
            EndNumber = endNumber;
        }

        public UrlTemplate InitializationTemplate { get; }
        public UrlTemplate MediaTemplate { get; }
        public long? EndNumber { get; }

        public override RangedUri GetInitialization(Representation representation)
        {
            if (InitializationTemplate != null)
            {
                var urlString = InitializationTemplate.Get(representation.Format.Bitrate,
                    representation.Format.Id, 0, 0);
                return new RangedUri(urlString, 0, null);
            }

            return base.GetInitialization(representation);
        }

        public override RangedUri GetSegmentUrl(Representation representation, long index)
        {
            long time;
            if (SegmentTimeline != null)
                time = SegmentTimeline.Get((int)(index - StartNumber)).StartTime;
            else
                time = (index - StartNumber) * Duration.Value;
            var urlString = MediaTemplate.Get(representation.Format.Bitrate, representation.Format.Id, index, time);
            return new RangedUri(urlString, 0, null);
        }

        public override int? GetSegmentCount(TimeSpan? periodDuration)
        {
            if (SegmentTimeline != null) return SegmentTimeline.Count;

            if (EndNumber.HasValue) return (int)(EndNumber.Value - StartNumber + 1);

            if (periodDuration.HasValue)
            {
                var durationTicks = TimeSpan.FromSeconds(Duration.Value / (double)TimeScale).Ticks;
                var periodDurationTicks = periodDuration.Value.Ticks;
                return (int)((periodDurationTicks + durationTicks - 1) / durationTicks);
            }

            return null;
        }
    }
}
