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
        private volatile int participantCount;
        private volatile int currentCount;
        private readonly object locker = new object();
        private TaskCompletionSource<object> waitTcs;

        private Task GetWaitTask()
        {
            if (waitTcs == null)
                waitTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            return waitTcs.Task;
        }

        private bool ReleaseIfReached(int value, bool valueIsParticipantCount)
        {
            int participants;
            int currently;

            if (valueIsParticipantCount)
            {
                participants = value;
                currently = currentCount;
            }
            else
            {
                participants = participantCount;
                currently = value;
            }

            if (currently < participants)
                return false;

            currentCount = 0;
            waitTcs?.SetResult(null);
            waitTcs = null;
            return true;
        }

        public void Reset()
        {
            lock (locker)
            {
                currentCount = 0;
                participantCount = 0;
            }
        }

        public void AddParticipant()
        {
            lock (locker)
            {
                participantCount++;
            }
        }

        public void RemoveParticipant()
        {
            lock (locker)
            {
                var newCount = participantCount - 1;
                if (newCount < 0)
                    throw new ArgumentOutOfRangeException(nameof(participantCount) + "<0");

                participantCount = newCount;
                ReleaseIfReached(newCount, true);
            }
        }

        public Task Signal()
        {
            lock (locker)
            {
                var newCurrentCount = currentCount + 1;

                if (ReleaseIfReached(newCurrentCount, false))
                    return Task.CompletedTask;

                currentCount = newCurrentCount;
                return GetWaitTask();
            }
        }
    }
}
