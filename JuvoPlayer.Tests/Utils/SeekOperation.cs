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

        private Task GetPositionReachedTask(TestContext context, TimeSpan targetClock)
        {
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

            var positionTask = GetPositionReachedTask(context, SeekPosition);
            var seekTask = service.SeekTo(SeekPosition);

            try
            {
                if (context.Timeout != TimeSpan.Zero)
                {
                    seekTask = seekTask.WithTimeout(context.Timeout);
                    positionTask = positionTask.WithTimeout(context.Timeout);
                }

                // Await seek & position individually. Seek failures may be expected
                // under certain circumstance.
                await seekTask.WithCancellation(context.Token);
                await positionTask.WithCancellation(context.Token);
            }
            catch (Exception)
            {
                _logger.Error($"Seek To {SeekPosition} SeekTask: {seekTask.Status} PositionTask: {positionTask.Status}");
                throw;
            }
        }

        private TimeSpan RandomSeekTime(IPlayerService service)
        {
            var rand = new Random();
            return TimeSpan.FromSeconds(rand.Next((int)service.Duration.TotalSeconds - 10));
        }
    }
}