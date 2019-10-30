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
using System.Threading.Tasks;

namespace JuvoPlayer.Utils
{
    class AsyncBarrier
    {
        private volatile int _participantCount;
        private volatile int _currentCount;
        private volatile object _message;
        private readonly object _locker = new object();
        private TaskCompletionSource<object> _waitTcs;

        private Task<object> GetWaitTask()
        {
            if (_waitTcs == null)
                _waitTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            return _waitTcs.Task;
        }

        private bool ReleaseIfReached(int value, bool valueIsParticipantCount)
        {
            int participants;
            int currently;

            if (valueIsParticipantCount)
            {
                participants = value;
                currently = _currentCount;
            }
            else
            {
                participants = _participantCount;
                currently = value;
            }

            if (currently < participants)
                return false;

            _currentCount = 0;
            _waitTcs?.SetResult(_message);
            _message = null;
            _waitTcs = null;
            return true;
        }

        public void Reset()
        {
            lock (_locker)
            {
                _currentCount = 0;
                _participantCount = 0;
            }
        }

        public void AddParticipant()
        {
            lock (_locker)
            {
                _participantCount++;
            }
        }

        public void RemoveParticipant()
        {
            lock (_locker)
            {
                var newCount = _participantCount - 1;
                if (newCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(_participantCount) + "<0");

                _participantCount = newCount;
                ReleaseIfReached(newCount, true);
            }
        }

        public Task<object> Signal(object msg = null)
        {
            lock (_locker)
            {
                if (msg != null)
                    _message = msg;

                var newCurrentCount = _currentCount + 1;
                var currentWaitTask = GetWaitTask();
                if (ReleaseIfReached(newCurrentCount, false))
                    return currentWaitTask;

                _currentCount = newCurrentCount;
                return currentWaitTask;
            }
        }
    }
}
