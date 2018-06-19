using System;
using System.Collections.Generic;
using System.Text;

namespace JuvoPlayer.DataProviders
{
    public interface IThroughputHistory
    {
        double GetAverageThroughput();
        void Push(int sizeInBytes, TimeSpan duration);
        void Reset();
    }
}
