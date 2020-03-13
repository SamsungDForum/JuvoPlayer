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
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using static Configuration.SeekLogic;
using System.Reactive.Threading.Tasks;

namespace JuvoPlayer.Common
{
    public class SeekLogic
    {
        public TimeSpan CurrentPositionUI
        {
            get
            {
                if (IsSeekAccumulationInProgress == false && IsSeekInProgress == false)
                {
                    _currentPositionUI = Clamp(CurrentPositionPlayer, TimeSpan.Zero,
                        Duration > TimeSpan.Zero ? Duration : TimeSpan.MaxValue);
                }

                return _currentPositionUI;
            }
            set => _currentPositionUI = value;
        }

        private TimeSpan _currentPositionUI;
        public TimeSpan CurrentPositionPlayer => _client.Player?.CurrentPosition ?? TimeSpan.Zero;
        public TimeSpan Duration => _client.Player?.Duration ?? TimeSpan.Zero;
        public PlayerState State => _client.Player?.State ?? PlayerState.Idle;
        public bool IsSeekingSupported => _client.Player?.IsSeekingSupported ?? false;

        public bool IsSeekInProgress { get; set; }
        public bool IsSeekAccumulationInProgress { get; set; }

        private Subject<Unit> _seekCompleted = new Subject<Unit>();

        public IObservable<Unit> SeekCompleted()
        {
            return _seekCompleted.AsObservable();
        }

        private readonly Stopwatch _seekStopwatch = new Stopwatch();
        private TimeSpan _targetSeekTime;
        private Task _seekDelay;
        private CancellationTokenSource _seekCancellationTokenSource;

        private readonly ISeekLogicClient _client;
        private StoryboardReader _storyboardReader;

        public SeekLogic(ISeekLogicClient client)
        {
            _client = client;
        }

        public StoryboardReader StoryboardReader
        {
            set => _storyboardReader = value;
        }

        public void Reset()
        {
            _seekCancellationTokenSource?.Cancel();
            IsSeekInProgress = false;
            IsSeekAccumulationInProgress = false;
            _seekStopwatch.Reset();
            _targetSeekTime = TimeSpan.Zero;
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
            TimeSpan seekInterval = DefaultSeekInterval;
            TimeSpan contentLength = Duration;
            TimeSpan intervalSinceLastSeek = IntervalSinceLastSeek();

            if (intervalSinceLastSeek < DefaultSeekIntervalValueThreshold) // key is being hold
                seekInterval = TimeSpan.FromMilliseconds(Math.Max(
                    0.01 * DefaultMaximumSeekIntervalPercentOfContentTotalTime * contentLength.TotalMilliseconds,
                    DefaultSeekInterval.TotalMilliseconds));

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
            if (IsSeekingSupported == false || IsSeekInProgress)
                return;

            _seekCancellationTokenSource?.Cancel();

            AccumulateSeekInterval(seekInterval);

            _seekCancellationTokenSource = new CancellationTokenSource();
            _seekDelay = Task.Delay(DefaultSeekAccumulateInterval, _seekCancellationTokenSource.Token);

            try
            {
                await _seekDelay;
                var player = _client.Player;
                if (player == null)
                    return;
                if (player.State != PlayerState.Playing)
                {
                    await player.StateChanged()
                        .Where(state => state == PlayerState.Playing)
                        .FirstAsync()
                        .ToTask(_seekCancellationTokenSource.Token);
                }
            }
            catch (TaskCanceledException)
            {
                return;
            }

            try
            {
                await ExecuteSeek();
                _seekCompleted.OnNext(Unit.Default);
            }
            catch (SeekException)
            {
                // ignored
            }
        }

        private void AccumulateSeekInterval(TimeSpan seekInterval)
        {
            if (IsSeekAccumulationInProgress)
            {
                _targetSeekTime += seekInterval;
            }
            else
            {
                _targetSeekTime = CurrentPositionPlayer + seekInterval;
            }

            _targetSeekTime = Clamp(_targetSeekTime, TimeSpan.Zero, Duration);
            CurrentPositionUI = _targetSeekTime;
            IsSeekAccumulationInProgress = true;
        }

        private async Task ExecuteSeek()
        {
            _seekStopwatch.Reset();

            if (IsStateSeekable(State))
            {
                IsSeekInProgress = true;
                await _client.Player?.SeekTo(_targetSeekTime);
                IsSeekAccumulationInProgress = false;
                IsSeekInProgress = false;
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
            var seekableStates = new[] {PlayerState.Playing, PlayerState.Paused};
            return seekableStates.Contains(state);
        }

        public bool ShallDisplaySeekPreview()
        {
            return IsSeekAccumulationInProgress
                   && _storyboardReader != null
                   && _storyboardReader.LoadTask.Status == TaskStatus.RanToCompletion;
        }

        public SubSkBitmap GetSeekPreviewFrame()
        {
            return ShallDisplaySeekPreview() ? _storyboardReader.GetFrame(CurrentPositionUI) : null;
        }
    }
}