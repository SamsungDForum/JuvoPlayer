using System;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;

namespace JuvoPlayer.Common
{
    /// <summary>
    /// Class allowing asynchronous GC logs collection.
    /// </summary>
    public class GCLogger : IDisposable
    {
        private ILogger Logger = LoggerManager.GetInstance().GetLogger("GC");
        private Task task;
        private CancellationTokenSource tokenSource;

        /// <summary>
        /// Starts collecting logs.
        /// </summary>
        /// <param name="interval">time interval between reading from GC</param>
        public void Start(TimeSpan interval)
        {
            tokenSource = new CancellationTokenSource();
            task = Task.Run(async () => await CollectLogs(interval, tokenSource.Token));
        }

        /// <summary>
        /// Ends collecting logs.
        /// </summary>
        public void Dispose()
        {
            tokenSource?.Cancel();
            task?.Wait();
        }

        /// <summary>
        /// Collects logs.
        /// <para>Informations from GC are after 5th occurence of ':'.</para>
        /// </summary>
        /// <param name="interval"></param>
        /// <param name="tokenSourceToken"></param>
        /// <returns></returns>
        private async Task CollectLogs(TimeSpan interval, CancellationToken tokenSourceToken)
        {
            int miliseconds = (int)interval.TotalMilliseconds;
            int iteration = 0;
            while (!tokenSourceToken.IsCancellationRequested)
            {
                Logger.Info($":{iteration}: GetTotalMemory(false): {GC.GetTotalMemory(false)}");
                Logger.Info($":{iteration}: CollectionCount(0): {GC.CollectionCount(0)}");
                Logger.Info($":{iteration}: CollectionCount(1): {GC.CollectionCount(1)}");
                Logger.Info($":{iteration}: CollectionCount(2): {GC.CollectionCount(2)}");
                iteration++;
                try
                {
                    await Task.Delay(miliseconds, tokenSourceToken);
                }
                catch (TaskCanceledException ex)
                {
                    
                }
            }
        }
    }
}