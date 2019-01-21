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
    public enum SegmentLocation
    {
        None,
        Period,
        Set,
        Repr
    }

    public class SegmentBaseTmplt<S> where S : Node.SegmentBase
    {
        protected S repr_;
        protected S set_;
        protected S period_;

        public uint? Timescale
        {
            get
            {
                return
                    repr_?.Timescale ??
                    set_?.Timescale ??
                    period_?.Timescale;
            }
        }
        public string IndexRange
        {
            get
            {
                return
                    repr_?.IndexRange ??
                    set_?.IndexRange ??
                    period_?.IndexRange;
            }
        }
        public bool IndexRangeExact
        {
            get
            {
                return
                    Flag(repr_?.IndexRangeExact) ||
                    Flag(set_?.IndexRangeExact) ||
                    Flag(period_?.IndexRangeExact);
            }
        }
        public ulong? PresentationTimeOffset
        {
            get
            {
                return
                    repr_?.PresentationTimeOffset ??
                    set_?.PresentationTimeOffset ??
                    period_?.PresentationTimeOffset;
            }
        }
        public double? AvailabilityTimeOffset
        {
            get
            {
                return
                    repr_?.AvailabilityTimeOffset ??
                    set_?.AvailabilityTimeOffset ??
                    period_?.AvailabilityTimeOffset;
            }
        }

        public bool AvailabilityTimeComplete
        {
            get
            {
                return
                    Flag(repr_?.AvailabilityTimeComplete) ||
                    Flag(set_?.AvailabilityTimeComplete) ||
                    Flag(period_?.AvailabilityTimeComplete);
            }
        }

        public TimeSpan? TimeShiftBufferDepth
        {
            get
            {
                return
                    repr_?.TimeShiftBufferDepth ??
                    set_?.TimeShiftBufferDepth ??
                    period_?.TimeShiftBufferDepth;
            }
        }

        public URL[] Initializations
        {
            get
            {
                return
                    Nonempty(repr_?.Initializations) ??
                    Nonempty(set_?.Initializations) ??
                    Nonempty(period_?.Initializations) ??
                    new URL[] { };
            }
        }

        public URL[] RepresentationIndexes
        {
            get
            {
                return
                    Nonempty(repr_?.RepresentationIndexes) ??
                    Nonempty(set_?.RepresentationIndexes) ??
                    Nonempty(period_?.RepresentationIndexes) ??
                    new URL[] { };
            }
        }

        internal SegmentBaseTmplt(S repr, S set, S period)
        {
            repr_ = repr;
            set_ = set;
            period_ = period;
        }

        protected bool Flag(bool? f) { return f ?? false; }
        protected T[] Nonempty<T>(T[] array)
        {
            return (array?.Length ?? 0) > 0 ? array : null;
        }

        public SegmentLocation HighestAvailable()
        {
            if (repr_ != null)
                return SegmentLocation.Repr;
            if (set_ != null)
                return SegmentLocation.Set;
            if (period_ != null)
                return SegmentLocation.Period;
            return SegmentLocation.None;
        }

        public SegmentLocation NextAvailable(SegmentLocation below)
        {
            if (below == SegmentLocation.None)
                throw new Exception("Can't get lower, than SegmentLocation.None");

            if (below == SegmentLocation.Repr && set_ != null)
                return SegmentLocation.Set;
            if (below != SegmentLocation.Period && period_ != null)
                return SegmentLocation.Period;
            return SegmentLocation.None;
        }

        public S GetDirect(SegmentLocation which)
        {
            switch (which)
            {
                case SegmentLocation.Repr: return repr_;
                case SegmentLocation.Set: return set_;
                case SegmentLocation.Period: return period_;
            }
            return null;
        }
    }

    public class MultipleSegmentBaseTmplt<S> : SegmentBaseTmplt<S> where S : Node.MultipleSegmentBase
    {
        public uint? Duration
        {
            get
            {
                return
                    repr_?.Duration ??
                    set_?.Duration ??
                    period_?.Duration;
            }
        }

        public uint? StartNumber
        {
            get
            {
                return
                    repr_?.StartNumber ??
                    set_?.StartNumber ??
                    period_?.StartNumber;
            }
        }

        public SegmentTimeline[] SegmentTimelines
        {
            get
            {
                return
                    Nonempty(repr_?.SegmentTimelines) ??
                    Nonempty(set_?.SegmentTimelines) ??
                    Nonempty(period_?.SegmentTimelines) ??
                    new SegmentTimeline[] { };
            }
        }

        public URL[] BitstreamSwitchings
        {
            get
            {
                return
                    Nonempty(repr_?.BitstreamSwitchings) ??
                    Nonempty(set_?.BitstreamSwitchings) ??
                    Nonempty(period_?.BitstreamSwitchings) ??
                    new URL[] { };
            }
        }

        internal MultipleSegmentBaseTmplt(S repr, S set, S period) : base(repr, set, period)
        {
        }
    }

    public class SegmentBase : SegmentBaseTmplt<Node.SegmentBase>
    {
        public SegmentBase(Node.SegmentBase repr, Node.SegmentBase set, Node.SegmentBase period)
            : base(repr, set, period)
        {
        }
    }

    public class SegmentTemplate : MultipleSegmentBaseTmplt<Node.SegmentTemplate>
    {
        public Template Media
        {
            get
            {
                return
                    repr_?.Media ??
                    set_?.Media ??
                    period_?.Media;
            }
        }

        public Template Index
        {
            get
            {
                return
                    repr_?.Index ??
                    set_?.Index ??
                    period_?.Index;
            }
        }

        public Template Initialization
        {
            get
            {
                return
                    repr_?.Initialization ??
                    set_?.Initialization ??
                    period_?.Initialization;
            }
        }

        public string BitstreamSwitching
        {
            get
            {
                return
                    repr_?.BitstreamSwitching ??
                    set_?.BitstreamSwitching ??
                    period_?.BitstreamSwitching;
            }
        }

        public SegmentTemplate(Node.SegmentTemplate repr, Node.SegmentTemplate set, Node.SegmentTemplate period)
            : base(repr, set, period)
        {
        }
    }

    public class SegmentList : MultipleSegmentBaseTmplt<Node.SegmentList>
    {
        public SegmentURL[] SegmentURLs
        {
            get
            {
                return
                    Nonempty(repr_?.SegmentURLs) ??
                    Nonempty(set_?.SegmentURLs) ??
                    Nonempty(period_?.SegmentURLs) ??
                    new SegmentURL[] { };
            }
        }

        public SegmentList(Node.SegmentList repr, Node.SegmentList set, Node.SegmentList period)
            : base(repr, set, period)
        {
        }
    }



   
 

    public struct TimelineItem
    {
        public ulong Number;
        public ulong Time;
        public ulong Duration;
        public ulong Repeats;

        internal TimeRelation RepeatFor(ulong point, out uint repeat)
        {
            repeat = 0;
            if (point < Time)
                return TimeRelation.LATER;
            if (point > Time + Duration * (Repeats + 1))
                return TimeRelation.EARLIER;
            point -= Time;
            repeat = (uint)(point / Duration);
            return TimeRelation.SPOTON;
        }
    }

    public class Timeline
    {
        public static TimelineItem[] FromDuration(uint startNumber, TimeSpan start, TimeSpan duration, ulong segDuration, ulong timescale)
        {
            ulong totalDuration = (ulong)Math.Ceiling(duration.TotalSeconds * timescale);
            ulong totalStart = (ulong)Math.Ceiling(start.TotalSeconds * timescale);
            TimeSpan scaledDuration = TimeSpan.FromSeconds((double)segDuration / timescale);

            ulong count = totalDuration / segDuration;
            ulong end = count * segDuration;

            TimelineItem[] result = new TimelineItem[totalDuration == end ? 1 : 2];
            result[0].Number = startNumber;
            result[0].Time = totalStart;
            result[0].Duration = segDuration;
            result[0].Repeats = (count - 1);

            if (totalDuration != end)
            {
                result[1].Number = startNumber + count;
                result[1].Time = totalStart + end;
                result[1].Duration = totalDuration - end;
                result[1].Repeats = 0;
            }

            return result;
        }

        public static TimelineItem[] FromXml(uint startNumber, TimeSpan periodStart, TimeSpan? periodEnd, uint timescale, S[] esses)
        {
            ulong offset = (ulong)Math.Ceiling(periodStart.TotalSeconds * timescale);
            ulong start = 0;
            TimelineItem[] result = new TimelineItem[esses.Length];
            for (int i = 0; i < esses.Length; ++i)
            {
                S s = esses[i];
                if (s.D == null)
                    return null;

                start = s.T ?? start;

                uint count = 1;
                int rep = s.R ?? -1; // non-existing and invalid @r should be treated the same:
                if (rep < 0)
                {
                    if (i < (esses.Length - 1))
                    {
                        // if t[s+1] is present, then r[s] is the ceil of (t[s+1] - t[s])/d[s]
                        ulong nextStart = esses[i + 1].T ?? (start + s.D.Value);
                        ulong chunks = (ulong)Math.Ceiling(((double)nextStart - start) / s.D.Value);
                        rep = (int)chunks - 1;
                    }
                    else
                    {
                        // else r[s] is the ceil of (PEwc[i] - PSwc[i] - t[s]/ts)*ts/d[s])
                        ulong totalEnd = periodEnd == null ? start + s.D.Value :
                            (ulong)Math.Ceiling(periodEnd.Value.TotalSeconds * timescale);
                        ulong chunks = (ulong)Math.Ceiling(((double)totalEnd - offset - start) / s.D.Value);
                        rep = (int)chunks - 1;
                    }
                }
                count += (uint)rep;

                result[i].Number = startNumber;
                result[i].Time = start + offset;
                result[i].Duration = s.D.Value;
                result[i].Repeats = (ulong)count - 1;

                start += s.D.Value * count;
                startNumber += count;
            }
            return result;
        }
    }
}
