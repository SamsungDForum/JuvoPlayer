using System;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.SharedBuffers;
using NUnit.Framework;

namespace JuvoPlayer.Tests
{
    [TestFixture]
    [Description("Tests for SharedBuffer class - cyclic, expandable byte array buffer.")]
    class SharedBufferTests
    {
        private static FramesSharedBuffer buffer;

        [SetUp]
        public static void Init()
        {
            buffer = new FramesSharedBuffer();
        }

        [TearDown]
        public static void Destroy()
        {
            buffer = null;
        }

        [Test]
        [Description("Test SharedBuffer singlethreaded usage (write everything, then read everything; nondeterministic).")]
        [Ignore("This test hangs sometimes")]
        public static void TestSharedBuffer()
        {
            Assert.NotNull(buffer);
            int testSize = 64 * 1024 * 1024; // total number of bytes to be read and written during the test
            int averageChunkSize = 32 * 1024; // number of bytes to be read/written in one operation at average ( => random value in range [0, 2 * averageChunkSize])
            int averageSleepTime = 0; // average argument for Thread.Sleep in milliseconds ( => random value in range [0, 2 * averageSleepTime])
            bool writingTaskSuccess = true;
            bool readingTaskSuccess = true;
            WritingTask(buffer, ref writingTaskSuccess, testSize, averageChunkSize, averageSleepTime);
            ReadingTask(buffer, ref readingTaskSuccess, testSize, averageChunkSize, averageSleepTime);
            Assert.AreEqual(writingTaskSuccess, true, "SharedBuffer sync writing failed.");
            Assert.AreEqual(readingTaskSuccess, true, "SharedBuffer sync reading failed.");
        }

        [Test]
        [Description("Test SharedBuffer multithreaded usage (one writer, one reader; multiple write/read operations; nondeterministic).")]
        public static void TestSharedBufferAsync()
        {
            Assert.NotNull(buffer);
            int testSize = 64 * 1024 * 1024; // total number of bytes to be read and written during the test
            int averageChunkSize = 32 * 1024; // number of bytes to be read/written in one operation at average ( => random value in range [0, 2 * averageChunkSize])
            int averageSleepTime = 0; // average argument for Thread.Sleep in milliseconds ( => random value in range [0, 2 * averageSleepTime])
            bool writingTaskSuccess = true;
            bool readingTaskSuccess = true;
            Task writingTask = Task.Factory.StartNew(() => WritingTask(buffer, ref writingTaskSuccess, testSize, averageChunkSize, averageSleepTime));
            Task readingTask = Task.Factory.StartNew(() => ReadingTask(buffer, ref readingTaskSuccess, testSize, averageChunkSize, averageSleepTime));
            Task[] tasks = { writingTask, readingTask };
            Task.WaitAll(tasks);
            Assert.AreEqual(writingTaskSuccess, true, "SharedBuffer sync writing failed.");
            Assert.AreEqual(readingTaskSuccess, true, "SharedBuffer sync reading failed.");
        }

        [Test]
        [Description("Test SharedBuffer.ClearData, SharedBuffer.Length and SharedBuffer.EndOfData basic functionality.")]
        public static void TestSharedBufferClearData()
        {
            Assert.NotNull(buffer);
            int testLength = 1024;
            Assert.AreEqual(buffer.EndOfData, false);
            Assert.AreEqual(buffer.Length(), 0);
            buffer.WriteData(new byte[testLength]);
            Assert.AreEqual(buffer.Length(), testLength);
            buffer.EndOfData = true;
            Assert.AreEqual(buffer.EndOfData, true);
            buffer.ClearData();
            Assert.AreEqual(buffer.EndOfData, false);
            Assert.AreEqual(buffer.Length(), 0);
        }

        private static void WritingTask(FramesSharedBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime)
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

        private static void ReadingTask(FramesSharedBuffer buffer, ref bool success, int size, int averageChunkSize, int averageSleepTime)
        {
            Random rnd = new Random();
            for (int read = 0, chunk = 0; read < size; read += chunk)
            {
                chunk = Math.Min(size - read, rnd.Next(0, averageChunkSize * 2));
                if ((success = ReadConsecutiveValues(buffer, read, ref chunk)) == false)
                    return;
                Thread.Sleep(rnd.Next(0, 2 * averageSleepTime));
            }
            success = true;
        }

        private static bool WriteConsecutiveValues(FramesSharedBuffer buffer, int startingValue, int numberOfValues)
        {
            byte[] data = new byte[numberOfValues];
            for (int i = 0; i < numberOfValues; ++i)
                data[i] = (byte)((startingValue + i) % byte.MaxValue);
            Assert.DoesNotThrow(() => buffer.WriteData(data), "SharedBuffer writing data failed.");
            return true;
        }


        private static bool ReadConsecutiveValues(FramesSharedBuffer buffer, int startingValue, ref int numberOfValues)
        {
            ArraySegment<byte>? data = null;
            int size = numberOfValues;
            Assert.DoesNotThrow(() => data = buffer.ReadData(size), "SharedBuffer reading data failed.");
            Assert.NotNull(data);
            numberOfValues = data.Value.Count;
            for(int i = 0; i < numberOfValues; ++i)
                if (data.Value.Array[i] != (byte) ((startingValue + i) % byte.MaxValue))
                    return false;
            return true;
        }
    }
}
