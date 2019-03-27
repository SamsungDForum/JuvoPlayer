using System;
using JuvoPlayer.Common;
using JuvoPlayer.Player.EsPlayer;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    public class TSPacketBarrier
    {
        private static readonly TimeSpan TimeFrame = TimeSpan.FromSeconds(1);

        [Test]
        public void Reached_CalledBeforeFirstPacket_ThrowsInvalidOperationException()
        {
            var barrier = new PacketBarrier(TimeFrame);

            Assert.That(barrier.Reached(), Is.False);
        }

        [Test]
        public void Reached_FirstPacketPushed_BarrierIsNotReached()
        {
            var barrier = new PacketBarrier(TimeFrame);
            var packet = CreateDataPacket(TimeSpan.FromSeconds(2));

            barrier.PacketPushed(packet);

            Assert.That(barrier.Reached(), Is.False);
        }

        [Test]
        public void Reached_SecondPacketDoesntReachBarrier_BarrierIsNotReached()
        {
            var barrier = new PacketBarrier(TimeFrame);
            var firstPacket = CreateDataPacket(TimeSpan.FromSeconds(2));
            var secondPacket = CreateDataPacket(TimeSpan.FromSeconds(2.5));

            barrier.PacketPushed(firstPacket);
            barrier.PacketPushed(secondPacket);

            Assert.That(barrier.Reached(), Is.False);
        }

        [Test]
        public void Reached_SecondPacketReachesBarrier_BarrierIsReached()
        {
            var barrier = new PacketBarrier(TimeFrame);
            var firstPacket = CreateDataPacket(TimeSpan.FromSeconds(2));
            var secondPacket = CreateDataPacket(TimeSpan.FromSeconds(3));

            barrier.PacketPushed(firstPacket);
            barrier.PacketPushed(secondPacket);

            Assert.That(barrier.Reached(), Is.True);
        }

        [Test]
        public void Reached_SecondPacketIsNonDataPacket_BarrierIsNotReached()
        {
            var barrier = new PacketBarrier(TimeFrame);
            var firstPacket = CreateDataPacket(TimeSpan.FromSeconds(2));
            var secondPacket = CreateNonDataPacket(TimeSpan.FromSeconds(3));

            barrier.PacketPushed(firstPacket);
            barrier.PacketPushed(secondPacket);

            Assert.That(barrier.Reached(), Is.False);
        }

        [Test]
        public void TimeToNextFrame_CalledBeforeFirstPacket_ThrowsInvalidOperationException()
        {
            var barrier = new PacketBarrier(TimeFrame);

            Assert.Throws<InvalidOperationException>(() => barrier.TimeToNextFrame());
        }

        [Test]
        public void TimeToNextFrame_CalledWhenBarrierIsReached_ReturnsPositiveTime()
        {
            var barrier = new PacketBarrier(TimeFrame);

            var firstPacket = CreateDataPacket(TimeSpan.FromSeconds(2));
            var secondPacket = CreateDataPacket(TimeSpan.FromSeconds(3));

            barrier.PacketPushed(firstPacket);
            barrier.PacketPushed(secondPacket);

            Assert.That(barrier.TimeToNextFrame(), Is.GreaterThan(TimeSpan.Zero));
        }

        [Test]
        public void Reset_CalledAfterBarrierIsReached_ResetsBarrier()
        {
            var barrier = new PacketBarrier(TimeFrame);

            var firstPacket = CreateDataPacket(TimeSpan.FromSeconds(2));
            var secondPacket = CreateDataPacket(TimeSpan.FromSeconds(3));

            barrier.PacketPushed(firstPacket);
            barrier.PacketPushed(secondPacket);

            barrier.Reset();

            Assert.That(barrier.Reached(), Is.False);
        }

        private static Packet CreateDataPacket(TimeSpan pts)
        {
            return new Packet
            {
                Pts = pts,
                Storage = new ManagedDataStorage()
            };
        }

        private static Packet CreateNonDataPacket(TimeSpan pts)
        {
            return new Packet
            {
                Pts = pts
            };
        }
    }
}