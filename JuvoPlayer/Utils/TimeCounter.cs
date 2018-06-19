using System;
using System.Diagnostics;

namespace JuvoPlayer.Utils
{
    internal class TimeCounter : IDisposable
    {
        private readonly Action<TimeSpan> disposeAction;
        private readonly Stopwatch watch = Stopwatch.StartNew();

        public TimeCounter(Action<TimeSpan> disposeAction)
        {
            this.disposeAction = disposeAction;
        }

        public void Dispose()
        {
            watch.Stop();

            var elapsed = watch.Elapsed;
            disposeAction?.Invoke(elapsed);
        }
    }
}
