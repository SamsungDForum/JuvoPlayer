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
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using JuvoPlayer.Player.EsPlayer;

namespace JuvoPlayer.TizenTests.Utils
{
    public class SeekOperation : TestOperation
    {
        private readonly ILogger _logger = LoggerManager.GetInstance().GetLogger("UT");

        public async Task Execute(TestContext context)
        {
            var service = context.Service;
            var position = context.SeekTime ?? RandomSeekTime(service);

            _logger.Info($"Seeking to {position}");

            using (var timeoutCts = new CancellationTokenSource())
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, context.Token))
            {
                timeoutCts.CancelAfter(context.Timeout);

                await service.SeekTo(position).WithCancellation(linkedCts.Token);

                for (var i = 0; i < 50; i++)
                {
                    var seekPos = position;
                    var curPos = service.CurrentPosition;
                    var diffMs = Math.Abs((curPos - seekPos).TotalMilliseconds);
                    if (diffMs < 500)
                        return;
                    await Task.Delay(200, linkedCts.Token);
                }

                throw new Exception("Seek failed");
            }
        }

        private TimeSpan RandomSeekTime(PlayerService service)
        {
            var rand = new Random();
            return TimeSpan.FromSeconds(rand.Next((int) service.Duration.TotalSeconds - 10));
        }
    }
}