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
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Players;
using JuvoPlayer.Tests.Utils;
using Nito.AsyncEx;
using NUnit.Framework;

namespace JuvoPlayer.Tests.Players
{
    [TestFixture]
    public class BufferingObserverTests
    {
        private static readonly TimeSpan StarvingThreshold = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan FilledThreshold = TimeSpan.FromSeconds(1);

        [Test]
        public async Task Start_CalledBeforeUpdate_PublishesStarvingState()
        {
            var (observer, clock) = CreateObserverAndClock();
            var segment = default(Segment);

            var bufferingTask = observer
                .OnBuffering()
                .FirstAsync()
                .ToTask();
            observer.Reset(segment);
            observer.Start();

            var isStarving = await bufferingTask;
            Assert.That(isStarving, Is.True);
        }

        [Test]
        public async Task Update_BufferFilled_PublishesFilledState()
        {
            var (observer, clock) = CreateObserverAndClock();
            var segment = default(Segment);

            observer.Reset(segment);
            observer.Start();
            var bufferingTask = observer
                .OnBuffering()
                .FirstAsync()
                .ToTask();
            observer.Update(new Packet {Pts = TimeSpan.FromSeconds(2)});

            var isStarving = await bufferingTask;
            Assert.That(isStarving, Is.False);
        }

        [Test]
        public void Update_BufferStarved_PublishesStarvingState()
        {
            AsyncContext.Run(async () =>
            {
                var (observer, clock) = CreateObserverAndClock();
                var segment = default(Segment);

                clock.Start();
                observer.Reset(segment);
                observer.Start();
                observer.Update(new Packet {Pts = TimeSpan.FromSeconds(1)});
                var bufferingTask = observer
                    .OnBuffering()
                    .FirstAsync()
                    .ToTask();
                var isStarving = await bufferingTask;

                Assert.That(isStarving, Is.True);
                Assert.That(clock.Elapsed, Is.EqualTo(TimeSpan.FromMilliseconds(800)));
            });
        }

        private static (BufferingObserver, Clock) CreateObserverAndClock()
        {
            var clockSourceStub = new ClockSourceStub();
            var clock = new Clock(clockSourceStub);

            async Task Delayer(TimeSpan delay, CancellationToken token)
            {
                clockSourceStub.Advance(delay);
                await Task.Yield();
            }

            var bufferingObserver = new BufferingObserver(
                clock,
                StarvingThreshold,
                FilledThreshold,
                Delayer);
            return (bufferingObserver, clock);
        }
    }
}