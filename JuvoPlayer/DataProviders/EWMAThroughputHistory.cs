using System;

namespace JuvoPlayer.DataProviders
{
    public class EWMAThroughputHistory : IThroughputHistory
    {
        private const double SlowEWMACoeff = 0.99;
        private const double FastEWMACoeff = 0.98;
        private const double SlowBandwidth = 500000;
        private const double FastBandwidth = 500000;

        private double slowBandwidth = SlowBandwidth;
        private double fastBandwidth = FastBandwidth;

        private readonly object throughputLock = new object();

        public double GetAverageThroughput()
        {
            lock (throughputLock)
            {
                return Math.Min(slowBandwidth, fastBandwidth);
            }
        }

        public void Push(int sizeInBytes, TimeSpan duration)
        {
            lock (throughputLock)
            {
                var bw = 8.0 * sizeInBytes / duration.TotalSeconds;
                slowBandwidth = SlowEWMACoeff * slowBandwidth + (1 - SlowEWMACoeff) * bw;
                fastBandwidth = FastEWMACoeff * fastBandwidth + (1 - FastEWMACoeff) * bw;
            }
        }

        public void Reset()
        {
            lock (throughputLock)
            {
                slowBandwidth = SlowBandwidth;
                fastBandwidth = FastBandwidth;
            }
        }
    }
}