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
using System.Linq;
using JuvoPlayer.Common;
using JuvoPlayer.Dash.MPD;
using Period = JuvoPlayer.Dash.MPD.Period;

namespace JuvoPlayer.Dash
{
    public class DashPeriod
    {
        public DashPeriod(int id)
        {
            Id = id;
            AvailableStreams = new StreamGroup[0];
        }

        public int Id { get; }
        public Period Period { get; private set; }
        public StreamGroup[] AvailableStreams { get; private set; }

        public void Update(Period period)
        {
            Period = period;
            BuildAvailableStreams();
        }

        private void BuildAvailableStreams()
        {
            var adaptationSets = Period.AdaptationSets;
            var availableStreams = new StreamGroup[adaptationSets.Count];
            for (var index = 0; index < adaptationSets.Count; index++)
            {
                var adaptationSet = adaptationSets[index];
                var contentType = adaptationSet.ContentType;
                var representations =
                    adaptationSet.Representations;
                var streams = representations
                    .Select(representation => representation.Format)
                    .Select(format => new StreamInfo(format))
                    .ToList();
                availableStreams[index] = new StreamGroup(contentType, streams);
            }

            AvailableStreams = availableStreams;
        }

        public AdaptationSet[] SelectStreams(StreamGroup[] selectedStreamGroups)
        {
            var adaptationSets =
                new AdaptationSet[selectedStreamGroups.Length];
            for (var index = 0; index < selectedStreamGroups.Length; ++index)
            {
                var selectedStreamGroup = selectedStreamGroups[index];
                var adaptationSetIndex =
                    Array.IndexOf(AvailableStreams, selectedStreamGroup);
                if (adaptationSetIndex == -1)
                    throw new ArgumentException("Invalid stream group");
                var adaptationSet =
                    Period.AdaptationSets[adaptationSetIndex];
                adaptationSets[index] = adaptationSet;
            }

            return adaptationSets;
        }

        public AdaptationSet GetAdaptationSet(StreamGroup streamGroup)
        {
            var adaptationSetIndex =
                Array.IndexOf(AvailableStreams, streamGroup);
            if (adaptationSetIndex == -1)
                throw new ArgumentException("Invalid stream group");
            return Period.AdaptationSets[adaptationSetIndex];
        }
    }
}
