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
using JuvoPlayer.Player;
using JuvoPlayer.TizenTests.Utils;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    class TSPlayerService
    {
        [TestCase("Clean MP4 over HTTP")]
        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        [TestCase("Clean HLS")]
        [TestCase("Clean HEVC 4k MPEG DASH")]
        public async Task Playback_Basic_PreparesAndStarts(string clipTitle)
        {
            using (var service = new PlayerService())
            {
                PrepareAndStart(service, clipTitle);

                await Task.Delay(TimeSpan.FromSeconds(1));

                Assert.That(service.CurrentPosition, Is.GreaterThan(TimeSpan.Zero));
            }
        }

        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        public void Seek_Random10Times_SeeksWithin500Milliseconds(string clipTitle)
        {
            var rand = new Random();
            using (var service = new PlayerService())
            {
                PrepareAndStart(service, clipTitle);

                for (var i = 0; i < 10; ++i)
                {
                    var seekTime = TimeSpan.FromSeconds(rand.Next((int) service.Duration.TotalSeconds));

                    service.SeekTo(seekTime);

                    Assert.That(() => service.CurrentPosition,
                        Is.EqualTo(seekTime)
                            .Within(500).Milliseconds
                            .After(10).Seconds
                            .PollEvery(100).MilliSeconds);
                }
            }
        }

        [TestCase("Clean byte range MPEG DASH")]
        [TestCase("Clean fMP4 MPEG DASH")]
        [TestCase("Sintel - Clean fMP4 MPEG DASH - multiple languages")]
        [TestCase("Encrypted MPEG DASH")]
        [TestCase("Encrypted 4K MPEG DASH")]
        public void Seek_Backward_SeeksWithin500Milliseconds(string clipTitle)
        {
            using (var service = new PlayerService())
            {
                PrepareAndStart(service, clipTitle);

                for (var nextSeekTime = service.Duration - TimeSpan.FromSeconds(5); nextSeekTime > TimeSpan.Zero; nextSeekTime -= TimeSpan.FromSeconds(20))
                {
                    service.SeekTo(nextSeekTime);

                    Assert.That(() => service.CurrentPosition,
                        Is.EqualTo(nextSeekTime)
                            .Within(500).Milliseconds
                            .After(10).Seconds
                            .PollEvery(100).MilliSeconds
                        );
                }
            }
        }

        private static void PrepareAndStart(PlayerService service, string clipTitle)
        {
            var clips = service.ReadClips();
            var clip = clips.Find(_ => _.Title.Equals(clipTitle));

            Assert.That(clip, Is.Not.Null);

            service.SetClipDefinition(clip);

            Assert.That(() => service.State, Is.EqualTo(PlayerState.Prepared)
                .After(10).Seconds
                .PollEvery(100).MilliSeconds);

            service.Start();
        }
    }
}
