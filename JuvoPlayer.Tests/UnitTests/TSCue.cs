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
using System.Collections.Generic;
using System.Text;
using JuvoPlayer.Subtitles;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSCue
    {
        [Test]
        [Category("Negative")]
        public void Begin_GreaterThanEnd_ThrowsArgumentException()
        {
            Cue cue = new Cue();
            cue.End = TimeSpan.FromMinutes(10);

            Assert.Throws<ArgumentException>(() => { cue.Begin = TimeSpan.FromMinutes(20);
            });
        }

        [Test]
        [Category("Negative")]
        public void Begin_EqualsEnd_ThrowsArgumentException()
        {
            Cue cue = new Cue
            {
                End = TimeSpan.FromMinutes(10)
            };

            Assert.Throws<ArgumentException>(() => {
                cue.Begin = TimeSpan.FromMinutes(10);
            });
        }

        [Test]
        [Category("Negative")]
        public void End_LessThanBegin_ThrowsArgumentException()
        {
            Cue cue = new Cue {Begin = TimeSpan.FromMinutes(20)};

            Assert.Throws<ArgumentException>(() => {
                cue.End = TimeSpan.FromMinutes(10);
            });
        }

        [Test]
        [Category("Negative")]
        public void End_EqualsBegin_ThrowsArgumentException()
        {
            Cue cue = new Cue { Begin = TimeSpan.FromMinutes(10) };

            Assert.Throws<ArgumentException>(() => {
                cue.End = TimeSpan.FromMinutes(10);
            });
        }

        [Test]
        [Category("Positive")]
        public void End_GreaterThanBegin_SetsSuccessfully()
        {
            Cue cue = new Cue { Begin = TimeSpan.FromMinutes(10) };

            cue.End = TimeSpan.FromMinutes(20);
            Assert.That(cue.End, Is.EqualTo(TimeSpan.FromMinutes(20)));
        }

        [Test]
        [Category("Positive")]
        public void Begin_LessThanEnd_SetsSuccessfully()
        {
            Cue cue = new Cue { End = TimeSpan.FromMinutes(20) };

            cue.Begin = TimeSpan.FromMinutes(10);
            Assert.That(cue.Begin, Is.EqualTo(TimeSpan.FromMinutes(10)));
        }

        [TestCase(5, -1)]
        [TestCase(10, 0)]
        [TestCase(15, 0)]
        [TestCase(20, 1)]
        [TestCase(25, 1)]
        [Category("Positive")]
        public void Compare_VariousTimes_ReturnsExpectedValues(int timeInSeconds, int expectedResult)
        {
            Cue cue = new Cue
            {
                Begin = TimeSpan.FromSeconds(10),
                End = TimeSpan.FromSeconds(20)
            };

            var result = cue.Compare(TimeSpan.FromSeconds(timeInSeconds));

            Assert.That(result, Is.EqualTo(expectedResult));
        }
    }
}
