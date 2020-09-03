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
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Tests.Utils;
using NSubstitute;
using NUnit.Framework;

namespace JuvoPlayer.Tests.Impl.Blocks.Player
{
    [TestFixture]
    public class PacketSynchronizerTests
    {
        [Test]
        public async Task TakeAsync_PacketAdded_TakesAfterSpecifiedTime()
        {
            var clockSourceStub = new ClockSourceStub();
            var clockStub = new Clock(clockSourceStub);

            Task Delayer(TimeSpan delay, CancellationToken token)
            {
                clockSourceStub.Advance(delay);
                return Task.CompletedTask;
            }

            var segment = new Segment {Base = clockStub.Elapsed, Start = TimeSpan.Zero, Stop = TimeSpan.MinValue};
            var synchronizer = new PacketSynchronizer(Delayer)
            {
                Clock = clockStub, Segment = segment, Offset = TimeSpan.FromMilliseconds(500)
            };

            clockStub.Start();
            synchronizer.Add(new Packet {Pts = TimeSpan.FromSeconds(1)});
            await synchronizer.TakeAsync();

            var currentTime = clockStub.Elapsed;
            Assert.That(currentTime, Is.EqualTo(TimeSpan.FromMilliseconds(500)));
        }

        [Test]
        public void TakeAsync_CancelledWhenWaitingForPacket_ThrowsException()
        {
            var synchronizer = new PacketSynchronizer();
            var cts = new CancellationTokenSource();

            var takeTask = synchronizer.TakeAsync(cts.Token);
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await takeTask);
        }

        [Test]
        public void TakeAsync_CancelledWhenSynchronizing_ThrowsException()
        {
            var clockSourceStub = new ClockSourceStub();
            var clockStub = new Clock(clockSourceStub);

            async Task Delayer(TimeSpan delay, CancellationToken token)
            {
                var tcs = new TaskCompletionSource<bool>();
                using (token.Register(() =>
                {
                    tcs.SetCanceled();
                }))
                {
                    await tcs.Task;
                }
            }

            var synchronizer = new PacketSynchronizer(Delayer)
            {
                Clock = clockStub, Segment = new Segment {Stop = TimeSpan.MinValue}
            };
            var cts = new CancellationTokenSource();

            synchronizer.Add(new Packet {Pts = TimeSpan.FromSeconds(1)});
            var takeTask = synchronizer.TakeAsync(cts.Token);
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await takeTask);
        }

        [Test]
        public void TakeAsync_CancelledAfterDelayerCompletes_ThrowsException()
        {
            var clockSourceStub = new ClockSourceStub();
            var clockStub = new Clock(clockSourceStub);

            var tcs = new TaskCompletionSource<bool>();

            Task Delayer(TimeSpan delay, CancellationToken token)
            {
                return tcs.Task;
            }

            var synchronizer = new PacketSynchronizer(Delayer)
            {
                Clock = clockStub, Segment = new Segment {Stop = TimeSpan.MinValue}
            };
            var cts = new CancellationTokenSource();

            synchronizer.Add(new Packet {Pts = TimeSpan.FromSeconds(1)});
            var takeTask = synchronizer.TakeAsync(cts.Token);
            cts.Cancel();
            tcs.SetResult(true);

            Assert.ThrowsAsync<OperationCanceledException>(async () => await takeTask);
        }

        [Test]
        public void Flush_Called_FlushesAllPendingPackets()
        {
            var synchronizer = new PacketSynchronizer();
            synchronizer.Add(new Packet());
            synchronizer.Flush();
            var cts = new CancellationTokenSource();
            var takeTask = synchronizer.TakeAsync(cts.Token);
            var isCompleted = takeTask.IsCompleted;
            cts.Cancel();

            Assert.That(isCompleted, Is.False);
        }

        [Test]
        public void Flush_Called_DisposesAllPendingPackets()
        {
            var synchronizer = new PacketSynchronizer();
            var packetMock = Substitute.ForPartsOf<Packet>();
            synchronizer.Add(packetMock);
            synchronizer.Flush();

            packetMock.ReceivedWithAnyArgs().Dispose();
        }
    }
}
