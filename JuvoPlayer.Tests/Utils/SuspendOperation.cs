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
using JuvoPlayer.Common;
using System.Threading.Tasks;
using NUnit.Framework;

namespace JuvoPlayer.Tests.Utils
{
    public class SuspendOperation : TestOperation
    {
        private Func<Task>[] _preconditions = Array.Empty<Func<Task>>();

        private static Random _rndGenerator;

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
            if (_preconditions.Length == 0)
                return;

            _rndGenerator = new Random();
        }

        public async Task Execute(TestContext context)
        {
            try
            {
                context.Service.Start();

                foreach (var preconditionTask in _preconditions)
                    await preconditionTask();
            }
            catch (Exception e)
            {
                // Defined preconditions to execute test were not met.
                // Success? Test was not run. Failure? Test was not run. 
                // Failure to run test for sure, but failure of test itself?
                Assert.Inconclusive("Test precondition criteria not met", e.Message, e);
            }

            var suspendedTask = WaitForState.Observe(context.Service, PlayerState.Idle, context.Token, context.Timeout);
            context.Service.Suspend();

            await suspendedTask;
        }

        public void SetPreconditions(params Func<Task>[] preconditions)
        {
            _preconditions = preconditions;
        }

        public static TimeSpan GetRandomTimeSpan(TimeSpan maxTimeSpan)
        {
            return TimeSpan.FromMilliseconds(_rndGenerator.Next((int)maxTimeSpan.TotalMilliseconds));
        }
    }
}
