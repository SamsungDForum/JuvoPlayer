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

using System;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.Tests.Utils
{
    [Serializable]
    public class ResumeOperation : TestOperation
    {
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType();
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public void Prepare(TestContext context)
        {
            /* Resume operation requires no preparation */
        }

        public Task Execute(TestContext context)
        {
            var stateTask = WaitForState.Observe(context.Service, PlayerState.Playing, context.Token, context.Timeout);
            var clockTask = RunningClockTask.Observe(context.Service, context.Token, context.Timeout);
            var resumeTask = context.Service.Resume().WithTimeout(context.Timeout).WithCancellation(context.Token);

            // Jolly resume operation is when:
            // - Resume() completes.
            // - Playing state is observed.
            // - Running clock is observed.
            return Task.WhenAll(resumeTask, stateTask, clockTask);
        }
    }
}
