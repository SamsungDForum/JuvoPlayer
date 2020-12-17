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

using JuvoPlayer.Common;
using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

namespace JuvoPlayer.Tests.Utils
{
    public class RunningPlayerTask
    {
        private readonly TestContext _context;
        private readonly IPlayerService _service;
        private Task _playbackErrorTask;
        private Task _clipCompletedTask;

        public RunningPlayerTask(TestContext context)
        {
            _context = context;
            _service = _context.Service;
        }

        public RunningPlayerTask Observe()
        {
            _playbackErrorTask = _service.PlaybackError()
                .FirstAsync()
                .ToTask();

            _clipCompletedTask = _service.StateChanged()
                .AsCompletion()
                .ToTask();

            return this;
        }

        public async Task VerifyRunning(TimeSpan duration)
        {
            // Playing state first.
            await WaitForState.Observe(_service, PlayerState.Playing, _context.Token, _context.Timeout);

            // Start clock & termination listners.
            var runningClockTask = RunningClockTask.Observe(_service, _context.Token, _context.Timeout);
            await Task.WhenAny(_playbackErrorTask, _clipCompletedTask, Task.Delay(duration)).WithCancellation(_context.Token);

            if (_playbackErrorTask.IsCompleted || _clipCompletedTask.IsCompleted)
            {
                throw new Exception($"Playback terminated or not running. " +
                    $"Error: {_playbackErrorTask.IsCompleted} " +
                    $"Completed: {_clipCompletedTask.IsCompleted}");
            }

            // verify running clock
            await runningClockTask;
        }

        public static RunningPlayerTask Observe(TestContext context)
        {
            return new RunningPlayerTask(context).Observe();
        }
    }
}
