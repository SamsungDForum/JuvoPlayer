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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JuvoPlayer.OpenGL
{
    class SeekLogic
    {
        public bool IsSeekInProgress { get; set; }
        public bool IsSeekAccumulationInProgress { get; set; }

        private readonly Program _ui;

        private readonly TimeSpan _defaultSeekInterval = TimeSpan.FromMilliseconds(5000);
        private readonly TimeSpan _defaultSeekAccumulateInterval = TimeSpan.FromMilliseconds(2000);
        private readonly double _defaultMaximumSeekIntervalPercentOfContentTotalTime = 1.0;
        private readonly TimeSpan _defaultSeekIntervalValueThreshold = TimeSpan.FromMilliseconds(200); // time between key events when key is being hold is ~100ms
        private readonly Stopwatch _seekStopwatch = new Stopwatch();
        private TimeSpan _accumulatedSeekInterval;
        private Task _seekDelay;
        private CancellationTokenSource _seekCancellationTokenSource;

        public SeekLogic(Program ui)
        {
            _ui = ui;
        }

        public void Reset()
        {
            IsSeekInProgress = false;
            IsSeekAccumulationInProgress = false;
            _seekStopwatch.Reset();
            _accumulatedSeekInterval = TimeSpan.Zero;
        }

        public void SeekForward()
        {
            Seek(SeekInterval());
        }

        public void SeekBackward()
        {
            Seek(-SeekInterval());
        }

        private TimeSpan SeekInterval()
        {
            TimeSpan seekInterval = _defaultSeekInterval;
            TimeSpan contentLength = _ui.PlayerHandle?.Duration ?? TimeSpan.Zero;
            TimeSpan intervalSinceLastSeek = IntervalSinceLastSeek();

            if (intervalSinceLastSeek < _defaultSeekIntervalValueThreshold) // key is being hold
                seekInterval = TimeSpan.FromMilliseconds(Math.Max(
                    0.01 * _defaultMaximumSeekIntervalPercentOfContentTotalTime * contentLength.TotalMilliseconds,
                    _defaultSeekInterval.TotalMilliseconds));

            return seekInterval;
        }

        private TimeSpan IntervalSinceLastSeek()
        {
            TimeSpan interval = TimeSpan.MaxValue;
            if (_seekStopwatch.IsRunning)
            {
                _seekStopwatch.Stop();
                interval = _seekStopwatch.Elapsed;
            }
            _seekStopwatch.Restart();
            return interval;
        }

        private async void Seek(TimeSpan seekInterval)
        {
            if (_ui.PlayerHandle.IsSeekingSupported == false)
                return;

            _seekCancellationTokenSource?.Cancel();

            if (IsSeekAccumulationInProgress)
            {
                _accumulatedSeekInterval += seekInterval;
            }
            else
            {
                _accumulatedSeekInterval = seekInterval;
                IsSeekAccumulationInProgress = true;
            }
            _ui.PlayerTimeCurrentPositionUpdate(seekInterval);

            _seekCancellationTokenSource = new CancellationTokenSource();
            _seekDelay = Task.Delay(_defaultSeekAccumulateInterval, _seekCancellationTokenSource.Token);
            try
            {
                await _seekDelay;
            }
            catch (TaskCanceledException)
            {
                return;
            }

            _seekStopwatch.Reset();

            if (_ui.PlayerHandle != null && _ui.PlayerHandle.IsSeekingSupported && IsStateSeekable(_ui.PlayerHandle.State))
            {
                IsSeekInProgress = true;
                TimeSpan seekTargetTime = Clamp(_ui.PlayerHandle.CurrentPosition + _accumulatedSeekInterval, TimeSpan.Zero, _ui.PlayerHandle.Duration);
                _ = _ui.PlayerHandle.SeekTo(seekTargetTime); // discarding result to suppress 'await' warning
                IsSeekAccumulationInProgress = false;
            }

            _seekDelay = null;
            _seekCancellationTokenSource = null;
        }

        private static TimeSpan Clamp(TimeSpan x, TimeSpan minVal, TimeSpan maxVal)
        {
            if (x < minVal)
                return minVal;
            if (x > maxVal)
                return maxVal;
            return x;
        }

        private static bool IsStateSeekable(PlayerState state)
        {
            var seekableStates = new[] { PlayerState.Playing, PlayerState.Paused };
            return seekableStates.Contains(state);
        }
    }
}
