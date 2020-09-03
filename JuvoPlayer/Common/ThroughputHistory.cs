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
using System.Collections.Generic;
using System.Linq;

namespace JuvoPlayer.Common
{
    public class ThroughputHistory : IThroughputHistory
    {
        private readonly LinkedList<double> _throughputs = new LinkedList<double>();
        public static int MaxMeasurementsToKeep { get; } = 20;
        public static int AverageThroughputSampleAmount { get; } = 4;
        public static int MinimumThroughputSampleAmount { get; } = 2;
        public static double ThroughputDecreaseScale { get; } = 1.3;
        public static double ThroughputIncreaseScale { get; } = 1.3;

        public double GetAverageThroughput()
        {
            lock (_throughputs)
            {
                var samplesCount = GetSamplesCount();
                return samplesCount > 0.0 ? _throughputs.Take(samplesCount).Average() : 0.0;
            }
        }

        public void Push(int sizeInBytes, TimeSpan duration)
        {
            lock (_throughputs)
            {
                // bits/ms = kbits/s
                var throughput = 8 * sizeInBytes / duration.TotalMilliseconds;
                _throughputs.AddFirst(throughput * 1000); // we want throughputs in bps

                if (_throughputs.Count > MaxMeasurementsToKeep)
                    _throughputs.RemoveLast();
            }
        }

        public void Reset()
        {
            lock (_throughputs)
            {
                _throughputs.Clear();
            }
        }

        private int GetSamplesCount()
        {
            if (_throughputs.Count < MinimumThroughputSampleAmount)
                return 0;

            var sampleSize = AverageThroughputSampleAmount;
            if (sampleSize >= _throughputs.Count)
                return _throughputs.Count;

            // if throughput samples vary a lot, average over a wider sample
            var first = _throughputs.First;
            var second = first.Next;
            for (var i = 0; i < sampleSize - 1; ++i, first = second, second = second.Next)
            {
                var ratio = first.Value / second.Value;

                if (ratio >= ThroughputIncreaseScale || ratio <= 1 / ThroughputDecreaseScale)
                {
                    if (++sampleSize == _throughputs.Count)
                        break;
                }
            }

            return sampleSize;
        }
    }
}
