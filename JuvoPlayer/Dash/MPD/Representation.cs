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
using System.Collections.Immutable;
using JuvoPlayer.Common;

namespace JuvoPlayer.Dash.MPD
{
    public abstract class Representation
    {
        private readonly RangedUri _initializationUri;

        public Representation(long? revisionId, Format format, string baseUrl, SegmentBase segmentBase,
            IList<Descriptor> inbandEventStreams)
        {
            RevisionId = revisionId;
            Format = format;
            BaseUrl = baseUrl;
            InbandEventStreams = inbandEventStreams == null
                ? new List<Descriptor>()
                : (IList<Descriptor>)inbandEventStreams.ToImmutableList();
            _initializationUri = segmentBase.GetInitialization(this);
            PresentationTimeOffset = segmentBase.GetPresentationTimeOffset();
        }

        public long? RevisionId { get; }
        public Format Format { get; }
        public string BaseUrl { get; }
        public TimeSpan PresentationTimeOffset { get; }
        public IList<Descriptor> InbandEventStreams { get; }

        public static Representation NewInstance(long? revisionId, Format format, string baseUrl,
            SegmentBase segmentBase)
        {
            return NewInstance(revisionId, format, baseUrl, segmentBase, null);
        }

        public static Representation NewInstance(long? revisionId, Format format, string baseUrl,
            SegmentBase segmentBase, IList<Descriptor> inbandEventStreams)
        {
            if (segmentBase is SingleSegmentBase singleSegmentBase)
            {
                return new SingleSegmentRepresentation(revisionId, format, baseUrl, singleSegmentBase,
                    inbandEventStreams, null);
            }

            if (segmentBase is MultiSegmentBase multiSegmentBase)
            {
                return new MultiSegmentRepresentation(revisionId, format, baseUrl, multiSegmentBase,
                    inbandEventStreams);
            }

            throw new ArgumentException("Unsupported segmentBase type");
        }

        public RangedUri GetInitializationUri()
        {
            return _initializationUri;
        }

        public abstract RangedUri GetIndexUri();
        public abstract ISegmentIndex GetIndex();

        public class SingleSegmentRepresentation : Representation
        {
            private readonly RangedUri _indexUri;
            private readonly SingleSegmentIndex _segmentIndex;

            public SingleSegmentRepresentation(long? revisionId, Format format, string baseUrl,
                SingleSegmentBase segmentBase,
                IList<Descriptor> inbandEventStreams, long? contentLength) : base(revisionId, format, baseUrl,
                segmentBase, inbandEventStreams)
            {
                Uri = new Uri(baseUrl);
                _indexUri = segmentBase.GetIndex();
                ContentLength = contentLength;
                _segmentIndex = _indexUri != null
                    ? null
                    : new SingleSegmentIndex(new RangedUri(null, 0, contentLength));
            }

            public Uri Uri { get; }
            public long? ContentLength { get; }

            public override RangedUri GetIndexUri()
            {
                return _indexUri;
            }

            public override ISegmentIndex GetIndex()
            {
                return _segmentIndex;
            }
        }

        public class MultiSegmentRepresentation : Representation, ISegmentIndex
        {
            private readonly MultiSegmentBase _segmentBase;

            public MultiSegmentRepresentation(long? revisionId, Format format, string baseUrl,
                MultiSegmentBase segmentBase, IList<Descriptor> inbandEventStreams) : base(revisionId, format, baseUrl,
                segmentBase, inbandEventStreams)
            {
                _segmentBase = segmentBase;
            }

            public long? GetSegmentCount(TimeSpan? periodDuration)
            {
                return _segmentBase.GetSegmentCount(periodDuration);
            }

            public long GetSegmentNum(TimeSpan time, TimeSpan? periodDuration)
            {
                return _segmentBase.GetSegmentNum(time, periodDuration);
            }

            public TimeSpan GetStartTime(long segmentNum)
            {
                return _segmentBase.GetSegmentStartTime(segmentNum);
            }

            public TimeSpan? GetDuration(long segmentNum, TimeSpan? periodDuration)
            {
                return _segmentBase.GetSegmentDuration(segmentNum, periodDuration);
            }

            public RangedUri GetSegmentUrl(long segmentNum)
            {
                return _segmentBase.GetSegmentUrl(this, segmentNum);
            }

            public long GetFirstSegmentNum()
            {
                return _segmentBase.StartNumber;
            }

            public override RangedUri GetIndexUri()
            {
                return null;
            }

            public override ISegmentIndex GetIndex()
            {
                return this;
            }
        }
    }
}
