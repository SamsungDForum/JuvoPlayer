/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2019, Samsung Electronics Co., Ltd
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
using JuvoPlayer.Common;
using Nito.AsyncEx;

namespace JuvoPlayer.Player.EsPlayer.Stream.Synchronization
{
    internal class StreamSynchronizationBarrier
    {
        private readonly ILogger logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        private readonly TimeSpan syncPoint;
        private readonly AsyncCountdownEvent trap;
        private readonly bool[] seekPointReached = new bool[(int)StreamType.Count];

        public StreamSynchronizationBarrier(TimeSpan syncPoint, int streamsToSync)
        {
            this.syncPoint = syncPoint;

            trap = new AsyncCountdownEvent(streamsToSync);
        }

        public void UpdateSynchronization(StreamType stream, TimeSpan checkTime)
        {
            if (checkTime < syncPoint || seekPointReached[(int)stream])
                return;

            seekPointReached[(int)stream] = true;
            logger.Info($"{stream}: {checkTime} Sync point reached {syncPoint}");

            trap.Signal();
        }

        public Task WaitForSynchronization(CancellationToken token) =>
            trap.WaitAsync(token);

    }
}
