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
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Demuxers.FFmpeg;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    public class TSChunksBuffer
    {
        [Test]
        [Category("Negative")]
        public void Take_SizeGreaterThanBufferSize_ReturnsSizeEqualToBufferSize()
        {
            var buffer = new ChunksBuffer();

            buffer.Add(new byte[1]);
            var received = buffer.Take(2);

            Assert.That(received.Count, Is.EqualTo(1));
        }

        [Test]
        [Category("Positive")]
        public async Task TakeAsync_SizeLessThanBufferSize_ReturnsRequestedSize()
        {
            var buffer = new ChunksBuffer();

            var takeTask = buffer.TakeAsync(1);
            buffer.Add(new byte[2]);
            var received = await takeTask;

            Assert.That(received.Count, Is.EqualTo(1));
        }

        [Test]
        [Category("Positive")]
        public void Take_MultipleAdds_ProperlyReturnsData()
        {
            var buffer = new ChunksBuffer();

            var expected = new byte[] { 1, 2 };

            buffer.Add(expected);
            buffer.Add(new byte[3]);
            var received = buffer.Take(5);

            Assert.That(received, Is.EqualTo(expected));
        }

        [Test]
        [Category("Positive")]
        public void Take_MultipleTakes_ProperlyReturnsData()
        {
            var buffer = new ChunksBuffer();

            var expected = new byte[] {3, 4, 5};

            buffer.Add(new byte[2]);
            buffer.Add(expected);

            buffer.Take(2);
            var received = buffer.Take(3);

            Assert.That(received, Is.EqualTo(expected));
        }

        [Test]
        [Category("Negative")]
        public void CompleteAdding_TakeAsyncCalled_ThrowsInvalidOperationException()
        {
            var buffer = new ChunksBuffer();

            buffer.CompleteAdding();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await buffer.TakeAsync(1));
        }

        [Test]
        [Category("Negative")]
        public void CompleteAdding_AddAsyncCalled_ThrowsInvalidOperationException()
        {
            var buffer = new ChunksBuffer();

            buffer.CompleteAdding();

            Assert.Throws<InvalidOperationException>(() => buffer.Add(new byte[] {}));
        }

        [Test]
        [Category("Negative")]
        public void TakeAsync_TokenCancelled_ThrowsTaskCanceledException()
        {
            var buffer = new ChunksBuffer();
            var cts = new CancellationTokenSource();

            var takeAsync = buffer.TakeAsync(1, cts.Token);
            cts.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(async () => await takeAsync);
        }
    }
}