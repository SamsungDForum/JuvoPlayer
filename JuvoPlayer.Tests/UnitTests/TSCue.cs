using System;
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
        public void Begin_GreaterThanEnd_ThrowsArgumentException()
        {
            Cue cue = new Cue();
            cue.End = TimeSpan.FromMinutes(10);

            Assert.Throws<ArgumentException>(() => { cue.Begin = TimeSpan.FromMinutes(20);
            });
        }

        [Test]
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
        public void End_LessThanBegin_ThrowsArgumentException()
        {
            Cue cue = new Cue {Begin = TimeSpan.FromMinutes(20)};

            Assert.Throws<ArgumentException>(() => {
                cue.End = TimeSpan.FromMinutes(10);
            });
        }

        [Test]
        public void End_EqualsBegin_ThrowsArgumentException()
        {
            Cue cue = new Cue { Begin = TimeSpan.FromMinutes(10) };

            Assert.Throws<ArgumentException>(() => {
                cue.End = TimeSpan.FromMinutes(10);
            });
        }

        [Test]
        public void End_GreaterThanBegin_SetsSuccessfully()
        {
            Cue cue = new Cue { Begin = TimeSpan.FromMinutes(10) };

            cue.End = TimeSpan.FromMinutes(20);
            Assert.That(cue.End, Is.EqualTo(TimeSpan.FromMinutes(20)));
        }

        [Test]
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
