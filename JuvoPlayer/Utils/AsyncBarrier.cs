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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Player.EsPlayer;

namespace JuvoPlayer.Utils
{
    internal class AsyncBarrier<T>
    {
        private volatile int _participantCount;
        private volatile int _currentCount;
        private volatile T[] _messages;
        private readonly object _locker = new object();
        private volatile TaskCompletionSource<T[]> _waitTcs;

        private Task<T[]> GetWaitTask()
        {
            if (_waitTcs == null)
                _waitTcs = new TaskCompletionSource<T[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            return _waitTcs.Task;
        }

        private void ReleaseIfReached(int currentCount)
        {
            if (currentCount < _participantCount)
                return;

            var currentTcs = _waitTcs;
            _waitTcs = null;
            _currentCount = 0;
            if (_participantCount != _messages.Length)
            {
                currentTcs?.SetResult(_messages.Take(_participantCount).ToArray());
                _messages = new T[_participantCount];
            }
            else
            {
                currentTcs?.SetResult(_messages);
            }
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
                var newCount = ++_participantCount;
                var currentMessages = _messages;
                Array.Resize(ref currentMessages, newCount);
                _messages = currentMessages;

            }
        }

        public void RemoveParticipant()
        {
            lock (_locker)
            {
                var newCount = --_participantCount;
                if (newCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(_participantCount) + "<0");

                ReleaseIfReached(newCount);
            }
        }

        public async Task<T[]> Signal(T message, CancellationToken token)
        {
            Task<T[]> waitTask;

            lock (_locker)
            {
                var newCount = ++_currentCount;
                _messages[newCount - 1] = message;

                waitTask = GetWaitTask();
                ReleaseIfReached(newCount);
            }

            return await waitTask.WithCancellation(token).ConfigureAwait(false);
        }
    }
}
