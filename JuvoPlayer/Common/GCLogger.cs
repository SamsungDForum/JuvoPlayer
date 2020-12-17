/*!
 * https://github.com/SamsungDForum/JuvoPlayer
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
 */

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