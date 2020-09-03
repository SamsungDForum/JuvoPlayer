/*!
 *
 * [https://github.com/SamsungDForum/JuvoPlayer])
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
 *
 */

using System;
using JuvoPlayer.Common;
using JuvoPlayer.Tests.Utils;
using NUnit.Framework;

namespace JuvoPlayer.Tests.Impl.Framework
{
    [TestFixture]
    public class ClockTests
    {
        [Test]
        public void Elapsed_CalledWhenNotRunning_ReturnsZero()
        {
            var source = new ClockSourceStub();
            var clock = new Clock(source);

            var elapsed = clock.Elapsed;

            Assert.That(elapsed, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void Elapsed_CalledAWhenRunningAfterOneSecond_ReturnsOneSecond()
        {
            var source = new ClockSourceStub();
            var clock = new Clock(source);

            clock.Start();
            source.Advance(TimeSpan.FromSeconds(1));
            var elapsed = clock.Elapsed;

            Assert.That(elapsed, Is.EqualTo(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void Elapsed_CalledAfterReset_ReturnsZero()
        {
            var source = new ClockSourceStub();
            var clock = new Clock(source);

            clock.Start();
            source.Advance(TimeSpan.FromSeconds(2));
            clock.Reset();
            var elapsed = clock.Elapsed;

            Assert.That(elapsed, Is.EqualTo(TimeSpan.Zero));
        }
    }
}
