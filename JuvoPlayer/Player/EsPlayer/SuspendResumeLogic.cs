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
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoLogger;
using Nito.AsyncEx;
using Tizen.TV.Multimedia;

namespace JuvoPlayer.Player.EsPlayer
{
    internal class SuspendResumeLogic
    {
        private static readonly ILogger Logger = LoggerManager.GetInstance().GetLogger("JuvoPlayer");

        // Suspend/Resume logic may be accessed from multiple threads
        // Lock provides resource access serialization, volatiles cross thread data validity.
        private readonly AsyncLock _requestSerializer; //= new AsyncLock();

        // _suspendCount is shared between Pause/Buffering requests, allowing simpler handling
        // of unpause while buffering
        private volatile int _suspendCount;

        // Single access gate for pause/resume oprations preventing _suspendCount growth.
        // UT required. UI will call Resume instead of multiple pause.
        private volatile bool _isPaused;

        // Async operatation flag. Prevents playback restarts when async operation is pending.
        private volatile bool _asyncOpRunning;

        // Functionality required by Suspend/Resume logic. Provided by EsStreamController
        // Suspends playback. Shall not affect data transfer
        private readonly Action _suspendAction;

        // Resumes playback. Shall not affect data transfer
        private readonly Action _resumeAction;

        // Sets player current state
        private readonly Action<Common.PlayerState> _setPlayerStateAction;

        // Retrieves platform video player state
        private readonly Func<ESPlayerState> _getPlayerState;

        // Controls data transfer. Shall not affect playback.
        // true - enable / false - disable
        private readonly Action<bool> _setDataTransfer;

        // EsStreamController termination.
        private readonly CancellationToken _isRunningCt;

        // Buffering event pipeline
        private delegate void RequestBufferingDelegate(int progress);
        private event RequestBufferingDelegate BufferingRequestEvent;
        private readonly IObservable<int> _bufferingProgressObservable;

        public IObservable<int> BufferingProgressObservable() =>
            _bufferingProgressObservable;

        public SuspendResumeLogic(AsyncLock asyncLock, Action suspendPlayback, Action resumePlayback, Action<Common.PlayerState> setPlayerState,
            Func<ESPlayerState> playerState, Action<bool> transferState, CancellationToken token)
        {
            _requestSerializer = asyncLock;
            _suspendAction = suspendPlayback;
            _resumeAction = resumePlayback;
            _setPlayerStateAction = setPlayerState;
            _getPlayerState = playerState;
            _setDataTransfer = transferState;

            _isRunningCt = token;

            _bufferingProgressObservable =
                Observable.FromEvent<RequestBufferingDelegate, int>(
                    h => BufferingRequestEvent += h, h => BufferingRequestEvent -= h);
        }

        public void SetAsyncOpRunningState(bool isRunning)
        {
            _asyncOpRunning = isRunning;
        }

        public async Task RequestPlay()
        {
            using (await _requestSerializer.LockAsync(_isRunningCt))
            {
                // Cancelled tokens can acquire async lock.
                _isRunningCt.ThrowIfCancellationRequested();

                // Honor play requests only when in paused state
                if (!_isPaused)
                    return;

                _isPaused = false;

                if (!Resume())
                    return;

                _setDataTransfer(true);
            }
        }

        public async Task RequestPause()
        {
            using (await _requestSerializer.LockAsync(_isRunningCt))
            {
                // Cancelled tokens can acquire async lock.
                // Non throwing exit for Pause() request.
                if (_isRunningCt.IsCancellationRequested)
                    return;

                // Honor pause requests only when NOT paused.
                if (_isPaused)
                    return;

                _isPaused = true;

                if (!Suspend())
                    return;

                _setDataTransfer(false);
            }
        }

        public async Task RequestBuffering(bool bufferingNeeded)
        {
            // Requests may come from multiple threads
            using (await _requestSerializer.LockAsync(_isRunningCt))
            {
                // Cancelled tokens can acquire async lock.
                _isRunningCt.ThrowIfCancellationRequested();

                if (bufferingNeeded)
                {
                    if (!Suspend())
                        return;

                    // Buffering event is fired on very first suspend.
                    // _suspendCount 0->1 transition
                    SetBuffering(true);
                    return;
                }

                if (!Resume())
                    return;

                // Buffering event is fired on very first resume.
                // _suspendCount 1->0 transition
                SetBuffering(false);

            }
        }

        private bool Resume()
        {
            var playerState = _getPlayerState();
            var asyncOp = _asyncOpRunning;

            Logger.Info($"PlayerState {playerState} Async Running {asyncOp} Suspends {_suspendCount}");
            switch (playerState)
            {
                case ESPlayerState.Paused:
                    _suspendCount--;
                    if (_suspendCount > 0) return false;
                    _setPlayerStateAction(Common.PlayerState.Playing);
                    if (asyncOp) return false;
                    _resumeAction();
                    return true;

                default:
                    return false;
            }
        }

        private bool Suspend()
        {
            var playerState = _getPlayerState();
            var asyncOp = _asyncOpRunning;

            Logger.Info($"PlayerState {playerState} Async Running {asyncOp} Suspends {_suspendCount}");
            switch (playerState)
            {
                case ESPlayerState.Playing:
                    _suspendCount++;
                    if (_suspendCount != 1) return false;
                    _setPlayerStateAction(Common.PlayerState.Paused);
                    _suspendAction();
                    return true;

                case ESPlayerState.Paused:
                    _suspendCount++;
                    return false;

                default:
                    return false;
            }
        }

        public void SetBuffering(bool bufferingOn)
        {
            BufferingRequestEvent?.Invoke(bufferingOn ? 0 : 100);
        }
    }
}
