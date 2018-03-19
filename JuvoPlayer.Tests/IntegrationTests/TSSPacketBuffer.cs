using System;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;
using JuvoPlayer.Player.SMPlayer;
using NUnit.Framework;

namespace JuvoPlayer.Tests.IntegrationTests
{
    [TestFixture]
    [Description("Tests for PacketBuffer class - buffer for Packet objects that can act as a FIFO or a Prority Queue (sorted by Dts/Pts, Asc/Desc).")]
    class PacketBufferTests
    {
        private static PacketBuffer buffer;

        [SetUp]
        public static void Init()
        {
            buffer = new PacketBuffer(PacketBuffer.Ordering.Fifo);
        }

        [TearDown]
        public static void Destroy()
        {
            buffer = null;
        }

        [Test]
        [Description("Test PacketBuffer singlethreaded usage (write all, then read all; FIFO mode; Data, Pts and Dts fields test; nondeterministic).")]
        public static void TestPacketBufferFIFO()
        {
            Assert.NotNull(buffer, "PacketBuffer object creation failed.");
            int testSize = 64 * 1024;
            int averageChunkSize = 32; // random value will be in range [0, 2 * averageChunkSize]
            int averageSleepTime = 0; // random value will be in range [0, 2 * averageSleepTime]
            bool writingTaskSuccess = true;
            bool readingTaskSuccess = true;
            WritingTask(buffer, ref writingTaskSuccess, testSize, averageChunkSize, averageSleepTime);
            ReadingTask(buffer, ref readingTaskSuccess, testSize, averageChunkSize, averageSleepTime);
            Assert.AreEqual(writingTaskSuccess, true, "PacketBuffer sync " + PacketBuffer.Ordering.Fifo + " writing failed.");
            Assert.AreEqual(readingTaskSuccess, true, "PacketBuffer sync " + PacketBuffer.Ordering.Fifo + " reading failed.");
        }

        [Test]
        [Description("Test PacketBuffer multithreaded usage (FIFO mode; Data, Pts and Dts fields test; nondeterministic).")]
        public static void TestPacketBufferFIFOAsync()
        {
            Assert.NotNull(buffer, "PacketBuffer object creation failed.");
            int testSize = 64 * 1024;
            int averageChunkSize = 32; // random value will be in range [0, 2 * averageChunkSize]
            int averageSleepTime = 5; // random value will be in range [0, 2 * averageSleepTime]
            bool writingTaskSuccess = true;
            bool readingTaskSuccess = true;
            Task writingTask = Task.Factory.StartNew(() => WritingTask(buffer, ref writingTaskSuccess, testSize, averageChunkSize, averageSleepTime));
            Task readingTask = Task.Factory.StartNew(() => ReadingTask(buffer, ref readingTaskSuccess, testSize, averageChunkSize, averageSleepTime));
            Task[] tasks = { writingTask, readingTask };
            Task.WaitAll(tasks);
            Assert.AreEqual(writingTaskSuccess, true, "PacketBuffer async " + PacketBuffer.Ordering.Fifo + " writing failed.");
            Assert.AreEqual(readingTaskSuccess, true, "PacketBuffer async " + PacketBuffer.Ordering.Fifo + " reading failed.");
        }

        [Test]
        [Description("Test PacketBuffer multithreaded usage (DtsAsc mode).")]
        public static void TestPacketBufferDtsAscAsync()
        {
            Assert.NotNull(buffer, "PacketBuffer object creation failed.");
            TestPQ(PacketBuffer.Ordering.DtsAsc);
        }

        [Test]
        [Description("Test PacketBuffer multithreaded usage (PtsAsc mode).")]
        public static void TestPacketBufferPtsAscAsync()
        {
            Assert.NotNull(buffer, "PacketBuffer object creation failed.");
            TestPQ(PacketBuffer.Ordering.PtsAsc);
        }

        [Test]
        [Description("Test PacketBuffer multithreaded usage (DtsDesc mode).")]
        public static void TestPacketBufferDtsDescAsync()
        {
            Assert.NotNull(buffer, "PacketBuffer object creation failed.");
            TestPQ(PacketBuffer.Ordering.DtsDesc);
        }

        [Test]
        [Description("Test PacketBuffer multithreaded usage (PtsDesc mode).")]
        public static void TestPacketBufferPtsDescAsync()
        {
            Assert.NotNull(buffer, "PacketBuffer object creation failed.");
            TestPQ(PacketBuffer.Ordering.PtsDesc);
        }

        [Test]
        [Description("Test PacketBuffer methods for Fifo mode: Peek, PeekSortingValue, AvailablePacketsCount, Count, Clear.")]
        public static void TestPacketBufferFifoMinorMethods()
        {
            Assert.NotNull(buffer, "PacketBuffer object creation failed.");
            Assert.AreEqual(buffer.QueueOrdering, PacketBuffer.Ordering.Fifo);
            TestMinorMethods(buffer);
        }

        [Test]
        [Description("Test PacketBuffer methods for PtsAsc mode: Peek, PeekSortingValue, AvailablePacketsCount, Count, Clear.")]
        public static void TestPacketBufferPtsAscMinorMethods()
        {
            buffer = new PacketBuffer(PacketBuffer.Ordering.PtsAsc);
            Assert.NotNull(buffer, "PacketBuffer object creation failed.");
            Assert.AreEqual(buffer.QueueOrdering, PacketBuffer.Ordering.PtsAsc);
            TestMinorMethods(buffer);
        }

        private static void TestMinorMethods(PacketBuffer buffer) // should work the same for Fifo and PtsAsc => it's not testing priority queues' implementation correctness, but other tests are, so it' enough for now
        {
            // Test empty buffer
            Assert.AreEqual(buffer.Count(), 0);
            Assert.AreEqual(buffer.AvailablePacketsCount(), 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Peek());

            // Test after some packets are enqueued
            int testCount = 1024;
            for (int i = 0; i < testCount; ++i)
            {
                Packet packet = new Packet
                {
                    Pts = TimeSpan.FromMilliseconds(i + testCount),
                    Dts = TimeSpan.FromMilliseconds(i + 2 * testCount),
                    Data = null
                };
                buffer.Enqueue(packet);
            }
            Assert.AreEqual(buffer.Count(), testCount);
            Assert.AreEqual(buffer.AvailablePacketsCount(), testCount);
            Assert.AreEqual(buffer.Peek().Pts.TotalMilliseconds, testCount); // first element in PtsAsc priority queue
            Assert.AreEqual(buffer.PeekSortingValue(), testCount); // first element's pts

            // Test after some packets are dequeued
            int secondTestCount = 123;
            for (int i = 0; i < secondTestCount; ++i)
                buffer.Dequeue();
            Assert.AreEqual(buffer.Count(), testCount - secondTestCount);
            Assert.AreEqual(buffer.AvailablePacketsCount(), testCount - secondTestCount);
            Assert.AreEqual(buffer.Peek().Pts.TotalMilliseconds, testCount + secondTestCount); // first element in PtsAsc priority queue
            Assert.AreEqual(buffer.PeekSortingValue(), testCount + secondTestCount); // first element's pts

            // Test cleared buffer
            buffer.Clear();
            Assert.AreEqual(buffer.Count(), 0);
            Assert.AreEqual(buffer.AvailablePacketsCount(), 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Peek());
        }

        [Test]
        [Description("Test PacketBuffer thresholds' test.")]
        public static void TestPacketBufferThresholds()
        {
            Assert.NotNull(buffer, "PacketBuffer object creation failed.");
            // TODO(g.skowinski): Implement such test.
            //                    Must be done with at least 2 threads since not meeting threshold requirements blocks inside called Enqueue/Dequeue method.
            //                    But thresholds are not used right now, so tests can be written latter.
            //                    Or the threshold functionality can be removed if it won't be used.
        }

        private static void TestPQ(PacketBuffer.Ordering ordering)
        {
            buffer = new PacketBuffer(ordering);
            Assert.NotNull(buffer, "PacketBuffer " + ordering + " object creation failed.");
            int testSize = 1024;
            int averageChunkSize = 16; // random value will be in range [0, 2 * averageChunkSize]
            int averageSleepTime = 5; // random value will be in range [0, 2 * averageSleepTime]
            bool writingTaskSuccess = true;
            bool readingTaskSuccess = true;
            object lockPQ = new object();
            Task writingTask = Task.Factory.StartNew(() => WritingTaskPQ(buffer, ref writingTaskSuccess, testSize, averageChunkSize, averageSleepTime, ordering, lockPQ));
            Task readingTask = Task.Factory.StartNew(() => ReadingTaskPQ(buffer, ref readingTaskSuccess, testSize, averageChunkSize, averageSleepTime, ordering, lockPQ));
            Task[] tasks = { writingTask, readingTask };
            Task.WaitAll(tasks);
            Assert.AreEqual(writingTaskSuccess, true, "PacketBuffer async " + ordering + " writing failed.");
            Assert.AreEqual(readingTaskSuccess, true, "PacketBuffer async " + ordering + " reading failed.");
        }

        private static void WritingTaskPQ(PacketBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime, PacketBuffer.Ordering ordering, object lockPQ)
        {
            Assert.AreNotEqual(PacketBuffer.Ordering.Fifo, ordering, "Use WritingTask() method for Ordering.Fifo!");
            Random rnd = new Random();
            for (int written = 0, chunk = 0; written < size; written += chunk)
            {
                lock (lockPQ)
                {
                    chunk = Math.Min(size - written, rnd.Next(0, averageChunkSize * 2));
                    for (int i = 0; i < chunk; ++i)
                    {
                        Packet packet =
                            new Packet
                            {
                                Pts = TimeSpan.FromMilliseconds(rnd.Next(0, int.MaxValue)),
                                Dts = TimeSpan.FromMilliseconds(rnd.Next(0, int.MaxValue)),
                                Data = new byte[0]
                            };
                        Assert.DoesNotThrow(() => buffer.Enqueue(packet));
                    }
                }
                Thread.Sleep(rnd.Next(0, 2 * averageSleepTime));
            }
            success = true;
        }

        private static void ReadingTaskPQ(PacketBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime, PacketBuffer.Ordering ordering, object lockPQ)
        {
            Assert.AreNotEqual(PacketBuffer.Ordering.Fifo, ordering, "Use ReadingTask() method for Ordering.Fifo!");
            Random rnd = new Random();
            for (int written = 0, chunk = 0; written < size; written += chunk)
            {
                lock (lockPQ) // we need to read whole chunk-number packets at once, because allowing writer task to write in between reads would mess up ordering check
                {
                    chunk = Math.Min(size - written, rnd.Next(0, averageChunkSize * 2)); // we're syncing with write method through lock, so we can't let the buffer wait for more data - it would cause a deadlock
                    chunk = Math.Min(chunk, (int) buffer.Count());
                    TimeSpan lastOne =
                    (ordering == PacketBuffer.Ordering.DtsAsc || ordering == PacketBuffer.Ordering.PtsAsc
                        ? TimeSpan.MinValue
                        : TimeSpan.MaxValue);
                    for (int i = 0; i < chunk; ++i)
                    {
                        Packet packet = new Packet();
                        Assert.DoesNotThrow(() => packet = buffer.Dequeue());
                        switch (ordering)
                        {
                            case PacketBuffer.Ordering.DtsAsc:
                                if (lastOne > packet.Dts)
                                {
                                    success = false;
                                    return;
                                }
                                lastOne = packet.Dts;
                                break;
                            case PacketBuffer.Ordering.PtsAsc:
                                if (lastOne > packet.Pts)
                                {
                                    success = false;
                                    return;
                                }
                                lastOne = packet.Pts;
                                break;
                            case PacketBuffer.Ordering.DtsDesc:
                                if (lastOne < packet.Dts)
                                {
                                    success = false;
                                    return;
                                }
                                lastOne = packet.Dts;
                                break;
                            case PacketBuffer.Ordering.PtsDesc:
                                if (lastOne < packet.Pts)
                                {
                                    success = false;
                                    return;
                                }
                                lastOne = packet.Pts;
                                break;
                        }
                    }
                }
                Thread.Sleep(rnd.Next(0, 2 * averageSleepTime));
            }
            success = true;          
        }

        private static void WritingTask(PacketBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime, bool random = true)
        {
            Random rnd = new Random();
            for (int written = 0, chunk = 0; written < size; written += chunk)
            {
                chunk = Math.Min(size - written, random ? rnd.Next(0, averageChunkSize * 2) : averageChunkSize);
                if ((success = WriteConsecutiveValues(buffer, written, chunk)) == false)
                    return;
                Thread.Sleep(random ? rnd.Next(0, 2 * averageSleepTime) : averageSleepTime);
            }
            success = true;
        }

        private static void ReadingTask(PacketBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime, bool random = true)
        {
            Random rnd = new Random();
            for (int read = 0, chunk = 0; read < size; read += chunk)
            {
                chunk = Math.Min(size - read, random ? rnd.Next(0, averageChunkSize * 2) : averageChunkSize);
                if ((success = ReadConsecutiveValues(buffer, read, chunk)) == false)
                    return;
                Thread.Sleep(random ? rnd.Next(0, 2 * averageSleepTime) : averageSleepTime);
            }
            success = true;
        }

        private static bool WriteConsecutiveValues(PacketBuffer buffer, int startingValue, int numberOfValues)
        {
            for (int i = 0; i < numberOfValues; ++i)
            {
                Packet packet = new Packet
                {
                    Data = new byte[0],
                    Pts = TimeSpan.FromMilliseconds(startingValue + i),
                    Dts = TimeSpan.FromMilliseconds(startingValue + i)
                };
                Assert.DoesNotThrow(() => buffer.Enqueue(packet));
            }
            return true;
        }


        private static bool ReadConsecutiveValues(PacketBuffer buffer, int startingValue, int numberOfValues)
        {
            for (int i = 0; i < numberOfValues; ++i)
            {
                Packet packet = new Packet();
                Assert.DoesNotThrow(() => packet = buffer.Dequeue());
                if (packet.Pts.TotalMilliseconds != startingValue + i || packet.Dts.TotalMilliseconds != startingValue + i)
                    return false;
            }
            return true;
        }
    }
}
