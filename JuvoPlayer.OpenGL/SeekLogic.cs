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
        public bool IsBufferingInProgress { get; set; }

        private readonly Program _ui;

        private readonly TimeSpan _defaultSeekInterval = TimeSpan.FromMilliseconds(5000);
        private readonly TimeSpan _defaultSeekAccumulateInterval = TimeSpan.FromMilliseconds(1000);
        private readonly double _defaultMaximumSeekIntervalPercentOfContentTotalTime = 1.0;
        private readonly TimeSpan _defaultSeekIntervalValueThreshold = TimeSpan.FromMilliseconds(200); // time between key events when key is being hold is ~100ms
        private readonly Stopwatch _seekStopwatch = new Stopwatch();
        private TimeSpan _accumulatedSeekTime;
        private Task _seekDelay;
        private CancellationTokenSource _seekCancellationTokenSource;

        public SeekLogic(Program ui)
        {
            _ui = ui;
        }

        public void Reset()
        {
            IsSeekInProgress = false;
            IsBufferingInProgress = false;
            _seekStopwatch.Reset();
            _accumulatedSeekTime = TimeSpan.Zero;
        }

        public void SeekForward()
        {
            Seek(_defaultSeekInterval);
        }

        public void SeekBackward()
        {
            Seek(-_defaultSeekInterval);
        }

        private TimeSpan SeekInterval(TimeSpan intervalSinceLastSeek, TimeSpan contentLength, TimeSpan defaultSeekInterval)
        {
            TimeSpan seekInterval = defaultSeekInterval;
            if (intervalSinceLastSeek < _defaultSeekIntervalValueThreshold) // key is being hold
                seekInterval = TimeSpan.FromMilliseconds(Math.Max(
                    0.01 * _defaultMaximumSeekIntervalPercentOfContentTotalTime * contentLength.TotalMilliseconds,
                    defaultSeekInterval.TotalMilliseconds));
            return seekInterval;
        }

        private TimeSpan IntervalSinceLastSeek()
        {
            TimeSpan timeInterval = TimeSpan.MaxValue;
            if (_seekStopwatch.IsRunning)
            {
                _seekStopwatch.Stop();
                timeInterval = _seekStopwatch.Elapsed;
            }
            _seekStopwatch.Restart();
            return timeInterval;
        }

        private async void Seek(TimeSpan seekTime)
        {
            if (_ui.PlayerHandle.IsSeekingSupported == false)
                return;

            _seekCancellationTokenSource?.Cancel();

            seekTime = Math.Sign(seekTime.TotalMilliseconds) * SeekInterval(IntervalSinceLastSeek(), _ui.PlayerHandle?.Duration ?? TimeSpan.Zero, seekTime);

            if (IsBufferingInProgress == false)
            {
                _accumulatedSeekTime = seekTime;
                IsBufferingInProgress = true;
            }
            else
            {
                _accumulatedSeekTime += seekTime;
            }

            _ui.PlayerTimeCurrentPositionUpdate(seekTime);

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

            if (_accumulatedSeekTime > TimeSpan.Zero)
                Forward(_accumulatedSeekTime);
            else
                Rewind(-_accumulatedSeekTime);
            IsBufferingInProgress = false;

            _seekDelay = null;
            _seekCancellationTokenSource = null;
        }

        private static bool IsStateSeekable(PlayerState state)
        {
            var seekableStates = new[] { PlayerState.Playing, PlayerState.Paused };
            return seekableStates.Contains(state);
        }

        private void Forward(TimeSpan seekTime)
        {
            if (_ui.PlayerHandle == null || !_ui.PlayerHandle.IsSeekingSupported || !IsStateSeekable(_ui.PlayerHandle.State))
                return;

            IsSeekInProgress = true;
            if (_ui.PlayerHandle.Duration - _ui.PlayerHandle.CurrentPosition < seekTime)
                _ui.PlayerHandle.SeekTo(_ui.PlayerHandle.Duration);
            else
                _ui.PlayerHandle.SeekTo(_ui.PlayerHandle.CurrentPosition + seekTime);
        }

        private void Rewind(TimeSpan seekTime)
        {
            if (_ui.PlayerHandle == null || (!_ui.PlayerHandle.IsSeekingSupported || _ui.PlayerHandle.State < PlayerState.Playing))
                return;

            IsSeekInProgress = true;
            if (_ui.PlayerHandle.CurrentPosition < seekTime)
                _ui.PlayerHandle.SeekTo(TimeSpan.Zero);
            else
                _ui.PlayerHandle.SeekTo(_ui.PlayerHandle.CurrentPosition - seekTime);
        }
    }
}
