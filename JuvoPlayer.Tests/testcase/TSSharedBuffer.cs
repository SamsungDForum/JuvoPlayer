using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.Common;

namespace JuvoPlayer.Tests
{
    [TestFixture]
    [Description("")]
    class SharedBufferTests
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
        [Description("Test SharedBuffer singlethreaded usage (write all, then read all).")]
        public static void TestSharedBuffer()
        {
            SharedBuffer buffer = new SharedBuffer();
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
        [Description("Test SharedBuffer multithreaded usage.")]
        public static void TestSharedBufferAsync()
        {
            SharedBuffer buffer = new SharedBuffer();
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

        private static void WritingTask(SharedBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime)
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

        private static void ReadingTask(SharedBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime)
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

        private static bool WriteConsecutiveValues(SharedBuffer buffer, int startingValue, int numberOfValues)
        {
            byte[] data = new byte[numberOfValues];
            for (int i = 0; i < numberOfValues; ++i)
                data[i] = (byte)((startingValue + i) % byte.MaxValue);
            Assert.DoesNotThrow(() => buffer.WriteData(data));
            return true;
        }


        private static bool ReadConsecutiveValues(SharedBuffer buffer, int startingValue, int numberOfValues)
        {
            byte[] data = new byte[0];
            Assert.DoesNotThrow(() => data = buffer.ReadData(numberOfValues));
            if (data.Length != numberOfValues)
                return false;
            for(int i = 0; i < numberOfValues; ++i)
                if (data[i] != (byte) ((startingValue + i) % byte.MaxValue))
                    return false;
            return true;
        }
    }
}
