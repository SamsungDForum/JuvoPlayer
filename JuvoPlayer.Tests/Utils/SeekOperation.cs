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
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
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

        private Task _seekPositionReachedTask = Task.CompletedTask;

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
            // don't set timeout on positionReached task. Timeout will be applied when awaiting result.
            return context.Service
                .PlayerClock()
                .FirstAsync(pClock =>
                {
                    var clk = pClock - TimeSpan.FromMilliseconds(pClock.Milliseconds);
                    var diffMs = Math.Abs((clk - targetClock).TotalMilliseconds);
                    return diffMs <= 500;
                })
                .ToTask(context.Token);
        }

        public async Task Execute(TestContext context)
        {
            var service = context.Service;

            var seekDuringPause = service.State == PlayerState.Paused;

            _logger.Info($"Seeking to {SeekPosition}");

            try
            {
                // Seek in paused state requires resume.
                if (seekDuringPause)
                {
                    _logger.Info($"Seeking in Paused state. Resuming playback");
                    var startOperation = new StartOperation();
                    await startOperation.Execute(context);
                    await startOperation.Result(context);
                }

                var seek = service.SeekTo(SeekPosition);
                _seekPositionReachedTask = GetPositionReachedTask(context, SeekPosition);

                await seek.WithTimeout(context.Timeout);
            }
            catch (SeekException)
            {
                // Ignore
            }

        }

        public async Task Result(TestContext context)
        {
            _logger.Info($"Waiting for seek position: {SeekPosition}");
            try
            {
                await _seekPositionReachedTask.WithTimeout(context.Timeout);
            }
            catch (SeekException)
            {
                // Ignore
            }
        }

        private static TimeSpan RandomSeekTime(IPlayerService service)
        {
            var rand = new Random();
            return TimeSpan.FromSeconds(rand.Next((int)service.Duration.TotalSeconds - 10));
        }
    }
}