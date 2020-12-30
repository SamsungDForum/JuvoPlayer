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

using System.Threading.Tasks;
using JuvoPlayer.Dash;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Demuxers.FFmpeg;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests
{
    [TestFixture]
    public class FFmpegDemuxerTests
    {
        [Test]
        public async Task InitForEs_CompleteCalled_ThrowsDemuxerException()
        {
            using (var demuxer = CreateDemuxer())
            {
                var initTask = demuxer.InitForEs();
                demuxer.Complete();
                await demuxer.Completion;
                Assert.ThrowsAsync<DemuxerException>(
                    async () => await initTask);
            }
        }

        [Test]
        public async Task NextPacket_CalledAfterInitForEsFail_ReturnsNull()
        {
            using (var demuxer = CreateDemuxer())
            {
                _ = demuxer.InitForEs();
                demuxer.Complete();
                await demuxer.Completion;
                var packet = await demuxer.NextPacket();
                Assert.That(packet, Is.Null);
            }
        }

        private static FFmpegDemuxer CreateDemuxer()
        {
            var ffmpegDemuxer = new FFmpegDemuxer(new FFmpegGlue());
            ffmpegDemuxer.SetClient(new DashDemuxerClient());
            return ffmpegDemuxer;
        }
    }
}