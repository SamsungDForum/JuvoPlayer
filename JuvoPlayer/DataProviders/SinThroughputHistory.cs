using System;

namespace JuvoPlayer.DataProviders
{
    /// <summary>
    /// Fake ThroughputHistory based on sin function. Useful for testing.
    /// </summary>
    public class SinThroughputHistory : IThroughputHistory
    {
        private double x;
        private readonly double step;
        private readonly double min;
        private readonly double max;

        public SinThroughputHistory(double step, double min, double max)
        {
            this.step = step;
            this.min = min;
            this.max = max;
        }

        public double GetAverageThroughput()
        {
            var y = Math.Sin(x);
            var yScaled = (max - min) * (y + 1) / 2 + min;
            x += step;
            return yScaled;
        }

        public void Push(int sizeInBytes, TimeSpan duration)
        {
        }

        public void Reset()
        {
            x = 0;
        }
    }
}