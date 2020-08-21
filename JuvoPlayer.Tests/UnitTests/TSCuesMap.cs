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
    class TSCuesMap
    {
        private static CuesMap CreateCuesMap()
        {
            CuesMap map = new CuesMap();
            return map;
        }

        [Test]
        [Category("Negative")]
        public void Get_MapIsEmpty_ReturnsNull()
        {
            var map = CreateCuesMap();

            var received = map.Get(TimeSpan.FromSeconds(60));

            Assert.That(received, Is.Null);
        }

        [Test]
        [Category("Positive")]
        public void Get_ValidKey_ReturnsCue()
        {
            var map = CreateCuesMap();
            Cue cue = new Cue()
            {
                Begin = TimeSpan.FromSeconds(10),
                End = TimeSpan.FromSeconds(20)
            };

            map.Put(cue);
            var received = map.Get(TimeSpan.FromSeconds(15));

            Assert.That(received.Begin, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(received.End, Is.EqualTo(TimeSpan.FromSeconds(20)));
        }

        [Test]
        [Category("Negative")]
        public void Get_KeyBetweenTwoCues_ReturnsNull()
        {
            var map = CreateCuesMap();

            Cue first = new Cue()
            {
                Begin = TimeSpan.FromSeconds(10),
                End = TimeSpan.FromSeconds(20)
            };
            map.Put(first);

            Cue second = new Cue()
            {
                Begin = TimeSpan.FromSeconds(30),
                End = TimeSpan.FromSeconds(40)
            };
            map.Put(second);

            var received = map.Get(TimeSpan.FromSeconds(25));

            Assert.That(received, Is.Null);
        }

        [Test]
        [Category("Positive")]
        public void Get_KeyForSecondCue_ReturnsSecondCue()
        {
            var map = CreateCuesMap();

            Cue first = new Cue()
            {
                Begin = TimeSpan.FromSeconds(10),
                End = TimeSpan.FromSeconds(20)
            };
            map.Put(first);

            Cue second = new Cue()
            {
                Begin = TimeSpan.FromSeconds(30),
                End = TimeSpan.FromSeconds(40)
            };
            map.Put(second);

            var received = map.Get(TimeSpan.FromSeconds(30));

            Assert.That(received.Begin, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(received.End, Is.EqualTo(TimeSpan.FromSeconds(40)));
        }

        [Test]
        [Category("Positive")]
        public void Get_TwoAdjacentCuesAndGetForFirst_ReturnsFirstOne()
        {
            var map = CreateCuesMap();

            Cue first = new Cue()
            {
                Begin = TimeSpan.FromSeconds(10),
                End = TimeSpan.FromSeconds(20)
            };
            map.Put(first);

            Cue second = new Cue()
            {
                Begin = TimeSpan.FromSeconds(20),
                End = TimeSpan.FromSeconds(30)
            };
            map.Put(second);

            var received = map.Get(TimeSpan.FromSeconds(15));

            Assert.That(received.Begin, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(received.End, Is.EqualTo(TimeSpan.FromSeconds(20)));
        }

        [Test]
        [Category("Positive")]
        public void Get_TwoAdjacentCuesAndGetInBetween_ReturnsSecondOne()
        {
            var map = CreateCuesMap();

            Cue first = new Cue()
            {
                Begin = TimeSpan.FromSeconds(10),
                End = TimeSpan.FromSeconds(20)
            };
            map.Put(first);

            Cue second = new Cue()
            {
                Begin = TimeSpan.FromSeconds(20),
                End = TimeSpan.FromSeconds(30)
            };
            map.Put(second);

            var received = map.Get(TimeSpan.FromSeconds(20));

            Assert.That(received.Begin, Is.EqualTo(TimeSpan.FromSeconds(20)));
            Assert.That(received.End, Is.EqualTo(TimeSpan.FromSeconds(30)));
        }
    }
}
