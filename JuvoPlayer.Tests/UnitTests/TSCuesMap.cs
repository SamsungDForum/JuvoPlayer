using System;
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
        public void Get_MapIsEmpty_ReturnsNull()
        {
            var map = CreateCuesMap();

            var received = map.Get(TimeSpan.FromSeconds(60));

            Assert.That(received, Is.Null);
        }

        [Test]
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
    }
}
