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
using System.Threading.Tasks;

namespace JuvoPlayer.Tests.Utils
{
    [Serializable]
    public class RandomDelayOperation : TestOperation
    {
        public TimeSpan Delay { get; set; }

        private bool Equals(RandomDelayOperation other)
        {
            return Delay.Equals(other.Delay);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((RandomDelayOperation)obj);
        }

        public override int GetHashCode()
        {
            return Delay.GetHashCode();
        }

        public void Prepare(TestContext context)
        {
            var maxDelayTime = context.RandomMaxDelayTime;
            var rand = new Random();
            var millisecondsDelay = rand.Next((int)maxDelayTime.TotalMilliseconds);
            Delay = TimeSpan.FromMilliseconds(millisecondsDelay);
        }

        public Task Execute(TestContext context)
        {
            return Task.Delay(Delay);
        }
    }
}