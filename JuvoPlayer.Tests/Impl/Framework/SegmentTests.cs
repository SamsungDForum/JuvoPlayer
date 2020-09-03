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
using NUnit.Framework;

namespace JuvoPlayer.Tests.Impl.Framework
{
    [TestFixture]
    public class SegmentTests
    {
        [Test]
        public void ToClockTime_PositionIsLessThanStart_ThrowsInvalidOperationException()
        {
            var segment = new Segment {Start = TimeSpan.FromSeconds(2)};

            Assert.Throws<InvalidOperationException>(() => segment.ToClockTime(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void ToClockTime_PositionIsGreaterThanStop_ThrowsInvalidOperationException()
        {
            var segment = new Segment {Stop = TimeSpan.FromSeconds(1)};

            Assert.Throws<InvalidOperationException>(() => segment.ToClockTime(TimeSpan.FromSeconds(2)));
        }

        [TestCase(0, 0, 1, 0, 0)]
        [TestCase(0, 0, 2, 1, 1)]
        [TestCase(0, 0, 1, 1, 1)]
        [TestCase(0, 1, 2, 1, 0)]
        [TestCase(1, 0, 1, 0, 1)]
        [TestCase(1, 0, 1, 1, 2)]
        [TestCase(1, 1, 2, 1, 1)]
        [TestCase(1, 1, 2, 2, 2)]
        public void ToClockTime_DifferentSettings_CalculatesProperly(int baseTimeSec, int startSec,
            int stopSec, int positionSec, int expectedSec)
        {
            var segment = new Segment
            {
                Base = TimeSpan.FromSeconds(baseTimeSec),
                Start = TimeSpan.FromSeconds(startSec),
                Stop = TimeSpan.FromSeconds(stopSec)
            };
            var position = TimeSpan.FromSeconds(positionSec);
            var expected = TimeSpan.FromSeconds(expectedSec);

            var result = segment.ToClockTime(position);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
