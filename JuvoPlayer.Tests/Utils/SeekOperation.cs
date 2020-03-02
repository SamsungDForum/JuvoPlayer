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
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Common;
using JuvoPlayer.Player.EsPlayer;

namespace JuvoPlayer.Tests.Utils
{
    [Serializable]
    public class SeekOperation : TestOperation
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");

        public TimeSpan SeekPosition { get; set; }

        private bool Equals(SeekOperation other)
        {
            return SeekPosition.Equals(other.SeekPosition);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((SeekOperation)obj);
        }

        public override int GetHashCode()
        {
            return SeekPosition.GetHashCode();
        }

        public void Prepare(TestContext context)
        {
            var service = context.Service;
            var newSeekPos = context.SeekTime ?? RandomSeekTime(service);
            SeekPosition = newSeekPos - TimeSpan.FromMilliseconds(newSeekPos.Milliseconds);
        }

        private static Task GetPositionReachedTask(TestContext context, TimeSpan targetClock)
        {
            return context.Service
                .PlayerClock()
                .FirstAsync(pClock =>
                {
                    var clk = pClock - TimeSpan.FromMilliseconds(pClock.Milliseconds);
                    var diffMs = Math.Abs((clk - targetClock).TotalMilliseconds);
                    return diffMs <= 500;
                })
                .Timeout(context.Timeout)
                .ToTask(context.Token);
        }

        public Task Execute(TestContext context)
        {
            var service = context.Service;
            var seekStartState = service.State;

            _logger.Info($"Seeking to {SeekPosition} in state {seekStartState}");

            var positionReachedTask = GetPositionReachedTask(context, SeekPosition);
            var seekTask = service.SeekTo(SeekPosition);

            // When seeking in paused state, seek Task and thus position task
            // will not complete until playback is resumed. Return a completed task.
            return seekStartState == PlayerState.Paused
                ? Task.CompletedTask
                : Task.WhenAll(seekTask, positionReachedTask).WithTimeout(context.Timeout);
        }

        private static TimeSpan RandomSeekTime(IPlayerService service)
        {
            var rand = new Random();
            return TimeSpan.FromSeconds(rand.Next((int)service.Duration.TotalSeconds - 10));
        }
    }
}