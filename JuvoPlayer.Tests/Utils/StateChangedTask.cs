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
using JuvoPlayer.Common;

namespace JuvoPlayer.Tests.Utils
{
    public class StateChangedTask
    {
        private readonly IPlayerService _service;
        private readonly PlayerState _expectedState;
        private readonly CancellationToken _cancellationToken;
        private TimeSpan _timeout;

        public StateChangedTask(IPlayerService service, PlayerState expectedState, CancellationToken token,
            TimeSpan timeout)
        {
            _service = service;
            _expectedState = expectedState;
            _cancellationToken = token;
            _timeout = timeout;
        }

        public Task Observe()
        {
            return Task.Run(async () =>
            {
                using (var timeoutCts = new CancellationTokenSource())
                using (var linkedCts =
                    CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, timeoutCts.Token))
                {
                    timeoutCts.CancelAfter(_timeout);
                    while (_service.State != _expectedState && !linkedCts.IsCancellationRequested)
                    {
                        await Task.Delay(100, linkedCts.Token);
                    }
                }
            }, _cancellationToken);
        }

        public static Task Observe(IPlayerService service, PlayerState expectedState, CancellationToken token,
            TimeSpan timeout)
        {
            return new StateChangedTask(service, expectedState, token, timeout).Observe();
        }
    }
}