using NUnit.Framework;
using System;
using System.Collections.Generic;
using JuvoPlayer.Common;
using System.Threading.Tasks;
using System.Threading;

namespace JuvoPlayer.Tests
{
    [TestFixture]
    [Description("")]
    class PacketBufferTests
    {
        [SetUp]
        public static void Init()
        {
        }

        [TearDown]
        public static void Destroy()
        {
        }

        [Test]
        [Description("Test PacketBuffer singlethreaded usage (write all, then read all; FIFO mode; Data, Pts and Dts fields test).")]
        public static void TestPacketBufferFIFO()
        {
            PacketBuffer buffer = new PacketBuffer(PacketBuffer.Ordering.Fifo);
            Assert.NotNull(buffer);
            int testSize = 64 * 1024 * 1024;
            int averageChunkSize = 32 * 1024; // random value will be in range [0, 2 * averageChunkSize]
            int averageSleepTime = 10; // random value will be in range [0, 2 * averageSleepTime]
            bool writingTaskSuccess = true;
            bool readingTaskSuccess = true;
            WritingTask(buffer, ref writingTaskSuccess, testSize, averageChunkSize, averageSleepTime);
            ReadingTask(buffer, ref readingTaskSuccess, testSize, averageChunkSize, averageSleepTime);
            Assert.AreEqual(writingTaskSuccess, true);
            Assert.AreEqual(readingTaskSuccess, true);
        }

        [Test]
        [Description("Test PacketBuffer multithreaded usage (FIFO mode; Data, Pts and Dts fields test).")]
        public static void TestPacketBufferFIFOAsync()
        {
            PacketBuffer buffer = new PacketBuffer(PacketBuffer.Ordering.Fifo);
            Assert.NotNull(buffer);
            int testSize = 64 * 1024 * 1024;
            int averageChunkSize = 32 * 1024; // random value will be in range [0, 2 * averageChunkSize]
            int averageSleepTime = 10; // random value will be in range [0, 2 * averageSleepTime]
            bool writingTaskSuccess = true;
            bool readingTaskSuccess = true;
            Task writingTask = Task.Factory.StartNew(() => WritingTask(buffer, ref writingTaskSuccess, testSize, averageChunkSize, averageSleepTime));
            Task readingTask = Task.Factory.StartNew(() => ReadingTask(buffer, ref readingTaskSuccess, testSize, averageChunkSize, averageSleepTime));
            Task[] tasks = { writingTask, readingTask };
            Task.WaitAll(tasks);
            Assert.AreEqual(writingTaskSuccess, true);
            Assert.AreEqual(readingTaskSuccess, true);
        }

        [Test]
        [Description("Test PacketBuffer multithreaded usage (DtsAsc mode).")]
        public static void TestPacketBufferDtsAscAsync()
        {
            TestPQ(PacketBuffer.Ordering.DtsAsc);
        }

        [Test]
        [Description("Test PacketBuffer multithreaded usage (PtsAsc mode).")]
        public static void TestPacketBufferPtsAscAsync()
        {
            TestPQ(PacketBuffer.Ordering.PtsAsc);
        }

        [Test]
        [Description("Test PacketBuffer multithreaded usage (DtsDesc mode).")]
        public static void TestPacketBufferDtsDescAsync()
        {
            TestPQ(PacketBuffer.Ordering.DtsDesc);
        }

        [Test]
        [Description("Test PacketBuffer multithreaded usage (PtsDesc mode).")]
        public static void TestPacketBufferPtsDescAsync()
        {
            TestPQ(PacketBuffer.Ordering.PtsDesc);
        }

        private static void TestPQ(PacketBuffer.Ordering ordering)
        {
            PacketBuffer buffer = new PacketBuffer(ordering);
            Assert.NotNull(buffer);
            int testSize = 1024;
            int averageChunkSize = 16; // random value will be in range [0, 2 * averageChunkSize]
            int averageSleepTime = 10; // random value will be in range [0, 2 * averageSleepTime]
            bool writingTaskSuccess = true;
            bool readingTaskSuccess = true;
            Task writingTask = Task.Factory.StartNew(() => WritingTaskPQ(buffer, ref writingTaskSuccess, testSize, averageChunkSize, averageSleepTime, ordering));
            Task readingTask = Task.Factory.StartNew(() => ReadingTaskPQ(buffer, ref readingTaskSuccess, testSize, averageChunkSize, averageSleepTime, ordering));
            Task[] tasks = { writingTask, readingTask };
            Task.WaitAll(tasks);
            Assert.AreEqual(writingTaskSuccess, true);
            Assert.AreEqual(readingTaskSuccess, true);
        }

        private static void WritingTaskPQ(PacketBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime, PacketBuffer.Ordering ordering)
        {
            Assert.AreNotEqual(PacketBuffer.Ordering.Fifo, ordering, "Use WritingTask() method for Ordering.Fifo!");
            Random rnd = new Random();
            for (int written = 0, chunk = 0; written < size; written += chunk)
            {
                chunk = Math.Min(size - written, rnd.Next(0, averageChunkSize * 2));
                for (int i = 0; i < chunk; ++i)
                {
                    StreamPacket packet = new StreamPacket();
                    packet.Pts = (ulong) rnd.Next(0, int.MaxValue);
                    packet.Dts = (ulong) rnd.Next(0, int.MaxValue);
                    packet.Data = new byte[0];
                    Assert.DoesNotThrow(() => buffer.Enqueue(packet));
                }
                Thread.Sleep(rnd.Next(0, 2 * averageSleepTime));
            }
            success = true;
        }

        private static void ReadingTaskPQ(PacketBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime, PacketBuffer.Ordering ordering)
        {
            Assert.AreNotEqual(PacketBuffer.Ordering.Fifo, ordering, "Use ReadingTask() method for Ordering.Fifo!");
            Random rnd = new Random();
            for (int written = 0, chunk = 0; written < size; written += chunk)
            {
                chunk = Math.Min(size - written, rnd.Next(0, averageChunkSize * 2));
                ulong lastOne = (ordering == PacketBuffer.Ordering.DtsAsc || ordering == PacketBuffer.Ordering.PtsAsc ? ulong.MinValue : ulong.MaxValue);
                for (int i = 0; i < chunk; ++i)
                {
                    StreamPacket packet = new StreamPacket();
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
                Thread.Sleep(rnd.Next(0, 2 * averageSleepTime));
            }
            success = true;          
        }

        private static void WritingTask(PacketBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime)
        {
            Random rnd = new Random();
            for (int written = 0, chunk = 0; written < size; written += chunk)
            {
                chunk = Math.Min(size - written, rnd.Next(0, averageChunkSize * 2));
                if ((success = WriteConsecutiveValues(buffer, written, chunk)) == false)
                    return;
                Thread.Sleep(rnd.Next(0, 2 * averageSleepTime));
            }
            success = true;
        }

        private static void ReadingTask(PacketBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime)
        {
            Random rnd = new Random();
            for (int read = 0, chunk = 0; read < size; read += chunk)
            {
                chunk = Math.Min(size - read, rnd.Next(0, averageChunkSize * 2));
                if ((success = ReadConsecutiveValues(buffer, read, chunk)) == false)
                    return;
                Thread.Sleep(rnd.Next(0, 2 * averageSleepTime));
            }
            success = true;
        }

        private static bool WriteConsecutiveValues(PacketBuffer buffer, int startingValue, int numberOfValues)
        {
            StreamPacket packet = new StreamPacket();
            packet.Data = new byte[numberOfValues];
            packet.Pts = (ulong)startingValue;
            packet.Dts = (ulong)startingValue;
            for (int i = 0; i < numberOfValues; ++i)
                packet.Data[i] = (byte)((startingValue + i) % byte.MaxValue);
            Assert.DoesNotThrow(() => buffer.Enqueue(packet));
            return true;
        }


        private static bool ReadConsecutiveValues(PacketBuffer buffer, int startingValue, int numberOfValues)
        {
            StreamPacket packet = new StreamPacket();
            Assert.DoesNotThrow(() => packet = buffer.Dequeue());
            if (packet.Pts != (ulong) startingValue || packet.Dts != (ulong) startingValue)
                return false;
            if (packet.Data.Length != numberOfValues)
                return false;
            for(int i = 0; i < numberOfValues; ++i)
                if (packet.Data[i] != (byte) ((startingValue + i) % byte.MaxValue))
                    return false;
            return true;
        }
    }
}
