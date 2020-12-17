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
using JuvoPlayer.Common;
using JuvoPlayer.Demuxers;
using JuvoPlayer.Demuxers.FFmpeg;
using NSubstitute;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    public class TSFFmpegDemuxer
    {
        [Test]
        [Category("Positive")]
        public async Task ReadPacket_InvokedWithSizeGreaterThanBufferSize_ReturnsAllData()
        {
            var glueStub = Substitute.For<IFFmpegGlue>();
            var retrieveReadPacketTask = RetrieveReadPacket(glueStub);

            using (var demuxer = CreateFFmpegDemuxer(glueStub))
            {
                await demuxer.InitForEs();
                var readPacket = await retrieveReadPacketTask;

                var expected = AsByteArray(1);
                demuxer.PushChunk(expected);

                var received = readPacket.Invoke(1024);
                Assert.That(received, Is.EqualTo(expected));
            }
        }

        [Test]
        [Category("Positive")]
        public async Task ReadPacket_InvokedWithSizeLessThanBufferSize_ReturnsTrimmedData()
        {
            var glueStub = Substitute.For<IFFmpegGlue>();
            var retrieveReadPacketTask = RetrieveReadPacket(glueStub);

            using (var demuxer = CreateFFmpegDemuxer(glueStub))
            {
                await demuxer.InitForEs();
                var readPacket = await retrieveReadPacketTask;

                var fourBytes = AsByteArray(1);
                demuxer.PushChunk(fourBytes);

                var received = readPacket.Invoke(1);
                Assert.That(received.Count, Is.EqualTo(1));
            }
        }

        [TestCase(InitType.ForEs)]
        [TestCase(InitType.ForUrl)]
        [Category("Positive")]
        public async Task Init_ClipDurationAvailable_ReturnsDuration(InitType initType)
        {
            var formatContextStub = Substitute.For<IAVFormatContext>();
            formatContextStub.Duration.Returns(TimeSpan.FromSeconds(1));

            var glueStub = Substitute.For<IFFmpegGlue>();
            glueStub.AllocFormatContext().Returns(formatContextStub);

            var demuxer = CreateFFmpegDemuxer(glueStub);
            using (demuxer)
            {
                var clipConfig = await InitDemuxer(demuxer, initType);

                var duration = clipConfig.Duration;
                Assert.That(duration, Is.EqualTo(TimeSpan.FromSeconds(1)));
            }
        }

        [TestCase(InitType.ForEs)]
        [TestCase(InitType.ForUrl)]
        [Category("Positive")]
        public async Task Init_StreamConfigAvailable_ReturnsStreamConfig(InitType initType)
        {
            var expectedConfig = new VideoStreamConfig
            {
                Codec = VideoCodec.H263,
                FrameRateDen = 1,
                FrameRateNum = 2,
                BitRate = 1
            };
            var formatContextStub = Substitute.For<IAVFormatContext>();
            formatContextStub.ReadConfig(Arg.Any<int>()).Returns(expectedConfig);

            var glueStub = Substitute.For<IFFmpegGlue>();
            glueStub.AllocFormatContext().Returns(formatContextStub);

            using (var demuxer = CreateFFmpegDemuxer(glueStub))
            {
                var clipConfig = await InitDemuxer(demuxer, initType);

                var receivedConfig = clipConfig.StreamConfigs[0];
                Assert.That(receivedConfig, Is.EqualTo(expectedConfig));
            }
        }

        [TestCase(InitType.ForEs)]
        [TestCase(InitType.ForUrl)]
        [Category("Positive")]
        public async Task Init_DrmInitDataAvailable_ReturnsDrmInitData(InitType initType)
        {
            var expectedData = new DrmInitData
            {
                StreamType = StreamType.Video,
                InitData = new byte[] {1},
                SystemId = new byte[] {2}
            };
            var formatContextStub = Substitute.For<IAVFormatContext>();
            formatContextStub.DRMInitData.Returns(new[] {expectedData});

            var glueStub = Substitute.For<IFFmpegGlue>();
            glueStub.AllocFormatContext().Returns(formatContextStub);

            using (var demuxer = CreateFFmpegDemuxer(glueStub))
            {
                var clipConfig = await InitDemuxer(demuxer, initType);

                var receivedData = clipConfig.DrmInitDatas[0];
                Assert.That(receivedData, Is.EqualTo(expectedData));
            }
        }

        [TestCase(InitType.ForEs)]
        [TestCase(InitType.ForUrl)]
        [Category("Negative")]
        public async Task Init_CalledSecondTime_ThrowsInvalidOperationException(InitType initType)
        {
            using (var demuxer = CreateFFmpegDemuxer())
            {
                await InitDemuxer(demuxer, initType);
                Assert.ThrowsAsync<InvalidOperationException>(async () => await InitDemuxer(demuxer, initType));
            }
        }

        [Test]
        [Category("Negative")]
        public void NextPacket_CalledBeforeInit_ThrowsInvalidOperationException()
        {
            using (var demuxer = CreateFFmpegDemuxer())
            {
                Assert.ThrowsAsync<InvalidOperationException>(async () => await demuxer.NextPacket());
            }
        }

        [TestCase(InitType.ForEs)]
        [TestCase(InitType.ForUrl)]
        [Category("Positive")]
        public async Task Reset_Called_DeallocsAVFormatContext(InitType initType)
        {
            var formatContextMock = Substitute.For<IAVFormatContext>();

            var glueStub = Substitute.For<IFFmpegGlue>();
            glueStub.AllocFormatContext().Returns(formatContextMock);

            using (var demuxer = CreateFFmpegDemuxer(glueStub))
            {
                await InitDemuxer(demuxer, initType);
                demuxer.Reset();

                formatContextMock.Received().Dispose();
            }
        }

        [Test]
        [Category("Positive")]
        public async Task Reset_Called_DeallocsAVIOFormatContext()
        {
            var ioContextMock = Substitute.For<IAVIOContext>();

            var glueStub = Substitute.For<IFFmpegGlue>();
            glueStub.AllocIOContext(Arg.Any<ulong>(), Arg.Any<ReadPacket>()).Returns(ioContextMock);

            using (var demuxer = CreateFFmpegDemuxer(glueStub))
            {
                await demuxer.InitForEs();
                demuxer.Reset();

                ioContextMock.Received().Dispose();
            }
        }

        [Test]
        [Category("Negative")]
        public void PushChunk_CalledBeforeInitForEs_ThrowsException()
        {
            var glueStub = Substitute.For<IFFmpegGlue>();
            using (var demuxer = CreateFFmpegDemuxer(glueStub))
            {
                var dummyBuffer = AsByteArray(1);
                Assert.Throws<InvalidOperationException>(() => demuxer.PushChunk(dummyBuffer));
            }
        }

        [Test]
        [Category("Negative")]
        public async Task PushChunk_CalledAfterReset_ThrowsException()
        {
            var glueStub = Substitute.For<IFFmpegGlue>();
            using (var demuxer = CreateFFmpegDemuxer(glueStub))
            {
                await demuxer.InitForEs();

                demuxer.Reset();

                var dummyBuffer = AsByteArray(1);
                Assert.Throws<InvalidOperationException>(() => demuxer.PushChunk(dummyBuffer));
            }
        }

        public enum InitType
        {
            ForEs,
            ForUrl
        }

        private Task<ClipConfiguration> InitDemuxer(FFmpegDemuxer demuxer, InitType initType)
        {
            switch (initType)
            {
                case InitType.ForEs:
                    return demuxer.InitForEs();
                case InitType.ForUrl:
                    return demuxer.InitForUrl("dummy_url");
                default:
                    throw new ArgumentOutOfRangeException(nameof(initType), initType, null);
            }
        }

        private Task<ReadPacket> RetrieveReadPacket(IFFmpegGlue glue)
        {
            var tcs = new TaskCompletionSource<ReadPacket>();
            glue.When(stub => stub.AllocIOContext(Arg.Any<ulong>(), Arg.Any<ReadPacket>()))
                .Do(args => { tcs.SetResult(args.ArgAt<ReadPacket>(1)); });

            return tcs.Task;
        }

        private byte[] AsByteArray(int val)
        {
            return BitConverter.GetBytes(val);
        }

        private static FFmpegDemuxer CreateFFmpegDemuxer(IFFmpegGlue glue = null)
        {
            if (glue == null)
                glue = Substitute.For<IFFmpegGlue>();
            return new FFmpegDemuxer(glue);
        }
    }
}
