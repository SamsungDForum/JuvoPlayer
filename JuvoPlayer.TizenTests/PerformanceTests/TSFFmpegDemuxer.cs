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

#if NBENCH_TESTS

using JuvoPlayer.Demuxers;
using JuvoPlayer.Demuxers.FFmpeg;
using JuvoPlayer.TizenTests.NUnitPerformanceTesting;
using JuvoPlayer.TizenTests.Utils;
using NBench;
using Nito.AsyncEx;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.PerformanceTests
{
    public class TSFFmpegDemuxer : PerformanceTestSuite<TSFFmpegDemuxer>
    {
        private DashContent content;

        [PerfSetup]
        public void Setup()
        {
            var provider = new DashContentProvider();
            content = provider.GetGoogleCar();
            Assert.That(content.IsInitialized, Is.True);
        }

        [PerfBenchmark]
        [TimingMeasurement]
        public void InitForEs_Called_MeasuresPerformance()
        {
            AsyncContext.Run(async () =>
            {
                var demuxer = CreateDemuxer();
                using (demuxer)
                {
                    var initTask = demuxer.InitForEs();

                    demuxer.PushChunk(content.InitSegment);
                    foreach (var segment in content.Segments)
                        demuxer.PushChunk(segment);

                    await initTask;
                }
            });
        }

        public IDemuxer CreateDemuxer()
        {
            return new FFmpegDemuxer(new FFmpegGlue());
        }
    }
}

#endif