using System;
using System.Collections.Generic;
using System.Linq;

namespace JuvoPlayer.DataProviders
{
    public class ThroughputHistory : IThroughputHistory
    {
        private const int MaxMeasurementsToKeep = 20;
        private const int AverageThroughputSampleAmount = 4;
        private const int MinimumThroughputSampleAmount = 2;

        private const double ThroughputDecreaseScale = 1.3;
        private const double ThroughputIncreaseScale = 1.3;

        private readonly LinkedList<double> throughputs = new LinkedList<double>();
        public double GetAverageThroughput()
        {
            lock (throughputs)
            {
                var samplesCount = GetSamplesCount();
                return samplesCount > 0.0 ? throughputs.Take(samplesCount).Average() : 0.0;
            }
        }

        private int GetSamplesCount()
        {
            if (throughputs.Count < MinimumThroughputSampleAmount)
                return 0;

            var sampleSize = AverageThroughputSampleAmount;
            if (sampleSize >= throughputs.Count)
                return throughputs.Count;

            // if throughput samples vary a lot, average over a wider sample
            var first = throughputs.First;
            var second = first.Next;
            for (var i = 0; i < sampleSize - 1; ++i, first = second, second = second.Next)
            {
                var ratio = first.Value / second.Value;

                if (ratio >= ThroughputIncreaseScale || ratio <= 1 / ThroughputDecreaseScale)
                {
                    if (++sampleSize == throughputs.Count)
                        break;
                }
            }

            return sampleSize;
        }

        public void Push(int sizeInBytes, TimeSpan duration)
        {
            lock (throughputs)
            {
                // bits/ms = kbits/s
                var throughput = 8 * sizeInBytes / duration.TotalMilliseconds;
                throughputs.AddFirst(throughput * 1000); // we want throughputs in bps

                if (throughputs.Count > MaxMeasurementsToKeep)
                    throughputs.RemoveLast();
            }
        }

        public void Reset()
        {
            lock (throughputs)
            {
                throughputs.Clear();
            }
        }
    }
}
