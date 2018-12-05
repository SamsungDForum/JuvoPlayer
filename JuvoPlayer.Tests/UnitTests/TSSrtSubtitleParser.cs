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

﻿using System;
using JuvoPlayer.Subtitles;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSSrtSubtitleParser
    {
        [Test]
        public void ParseTime_WhenTimeIsValid_ParsesSuccessfully()
        {
            var parser = CreateSrtParser();

            var parsed = parser.ParseTime("00:02:17,440");

            Assert.That(parsed, Is.EqualTo(new TimeSpan(0, 0, 2, 17, 440)));
        }

        [Test]
        public void ParseTime_WhenTimeExceedesADay_ParsesSuccessfully()
        {
            var parser = CreateSrtParser();

            var parsed = parser.ParseTime("25:02:17,440");

            Assert.That(parsed, Is.EqualTo(new TimeSpan(0, 25, 2, 17, 440)));
        }

        [TestCase(null, Description = "Null time")]
        [TestCase("", Description = "Empty time")]
        [TestCase("25.02.17.440", Description = "Invalid separators")]
        [TestCase("25:02:17", Description = "Missing milliseconds")]
        [TestCase("a.b.c.d", Description = "Letters instead of numbers")]
        public void ParseTime_WhenTimeFormatIsInvalid_ThrowsFormatException(string invalidTimeToParse)
        {
            var parser = CreateSrtParser();

            Assert.Throws<FormatException>(() =>
            {
                parser.ParseTime(invalidTimeToParse);
            });
        }

        [Test]
        public void ParseTimeLine_WhenTimeIsValid_ReturnsExpectedBeginAndEnd()
        {
            var parser = CreateSrtParser();

            (var begin, var end) = parser.ParseTimeLine("00:02:17,440 --> 00:02:20,375");

            Assert.That(begin, Is.EqualTo(new TimeSpan(0, 0, 2, 17, 440)));
            Assert.That(end, Is.EqualTo(new TimeSpan(0, 0, 2, 20, 375)));
        }

        [TestCase(null, Description = "Null time line")]
        [TestCase("", Description = "Empty time line")]
        [TestCase("00:02:17,440 - 00:02:20,375", Description="Invalid separator")]
        [TestCase("00:02:17,440 -->", Description = "Only begin time")]
        [TestCase("-->  00:02:17,440", Description = "Only end time")]
        public void ParseTimeLine_WhenLineIsInvalid_ThrowsFormatException(string invalidTimeLineToParse)
        {
            var parser = CreateSrtParser();

            Assert.Throws<FormatException>(() => { parser.ParseTimeLine(invalidTimeLineToParse); });
        }

        private static SrtSubtitleParser CreateSrtParser()
        {
            return new SrtSubtitleParser();
        }
    }
}
