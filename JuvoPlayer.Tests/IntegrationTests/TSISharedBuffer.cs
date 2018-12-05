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

using System;
using System.Threading;
using System.Threading.Tasks;
using JuvoPlayer.SharedBuffers;
using NUnit.Framework;

namespace JuvoPlayer.Tests.IntegrationTests
{
    [TestFixture]
    [Description("Tests for ISharedBuffer class")]
    abstract class TSISharedBuffer
    {
        private static ISharedBuffer buffer;

        [SetUp]
        public void Init()
        {
            buffer = CreateSharedBuffer();
        }

        public abstract ISharedBuffer CreateSharedBuffer();

        [Test]
        [Description("Test SharedBuffer singlethreaded usage (write everything, then read everything; nondeterministic).")]
        [Ignore("This test hangs sometimes")]
        public void TestSharedBuffer()
        {
            Assert.NotNull(buffer);
            int testSize = 64 * 1024 * 1024; // total number of bytes to be read and written during the test
            int averageChunkSize = 32 * 1024; // number of bytes to be read/written in one operation at average ( => random value in range [0, 2 * averageChunkSize])
            int averageSleepTime = 0; // average argument for Thread.Sleep in milliseconds ( => random value in range [0, 2 * averageSleepTime])
            bool writingTaskSuccess = WritingTask(testSize, averageChunkSize, averageSleepTime);
            bool readingTaskSuccess = ReadingTask(testSize, averageChunkSize, averageSleepTime);
            Assert.AreEqual(true, writingTaskSuccess, "SharedBuffer sync writing failed.");
            Assert.AreEqual(true, readingTaskSuccess, "SharedBuffer sync reading failed.");
        }

        [Test]
        [Description("Test SharedBuffer multithreaded usage (one writer, one reader; multiple write/read operations; nondeterministic).")]
        public void TestSharedBufferAsync()
        {
            Assert.NotNull(buffer);
            int testSize = 64 * 1024 * 1024; // total number of bytes to be read and written during the test
            int averageChunkSize = 32 * 1024; // number of bytes to be read/written in one operation at average ( => random value in range [0, 2 * averageChunkSize])
            int averageSleepTime = 0; // average argument for Thread.Sleep in milliseconds ( => random value in range [0, 2 * averageSleepTime])
            Task<bool> writingTask = Task.Factory.StartNew(() => WritingTask(testSize, averageChunkSize, averageSleepTime));
            Task<bool> readingTask = Task.Factory.StartNew(() => ReadingTask(testSize, averageChunkSize, averageSleepTime));
            Task[] tasks = { writingTask, readingTask };
            Task.WaitAll(tasks);
            Assert.AreEqual(true, writingTask.Result, "SharedBuffer sync writing failed.");
            Assert.AreEqual(true, readingTask.Result, "SharedBuffer sync reading failed.");
        }

        [Test]
        [Description("Test SharedBuffer.ClearData, SharedBuffer.Length and SharedBuffer.EndOfData basic functionality.")]
        public void TestSharedBufferClearData()
        {
            Assert.NotNull(buffer);
            int testLength = 1024;
            Assert.AreEqual(false, buffer.EndOfData);
            Assert.AreEqual(0, buffer.Length());
            buffer.WriteData(new byte[testLength]);
            Assert.AreEqual(testLength, buffer.Length());
            buffer.ClearData();
            Assert.AreEqual(false, buffer.EndOfData);
            Assert.AreEqual(0, buffer.Length());
        }

        private bool WritingTask(int size, int averageChunkSize, int averageSleepTime)
        {
            Random rnd = new Random();
            for (int written = 0, chunk = 0; written < size; written += chunk)
            {
                chunk = Math.Min(size - written, rnd.Next(0, averageChunkSize * 2));
                if ((WriteConsecutiveValues(written, chunk)) == false)
                    return false;
                Thread.Sleep(rnd.Next(0, 2 * averageSleepTime));
            }

            return true;
        }

        private bool ReadingTask(int size, int averageChunkSize, int averageSleepTime)
        {
            Random rnd = new Random();
            for (int read = 0, chunk = 0; read < size; read += chunk)
            {
                chunk = Math.Min(size - read, rnd.Next(0, averageChunkSize * 2));
                if ((ReadConsecutiveValues(read, ref chunk)) == false)
                    return false;
                Thread.Sleep(rnd.Next(0, 2 * averageSleepTime));
            }

            return true;
        }

        private bool WriteConsecutiveValues(int startingValue, int numberOfValues)
        {
            byte[] data = new byte[numberOfValues];
            for (int i = 0; i < numberOfValues; ++i)
                data[i] = (byte)((startingValue + i) % byte.MaxValue);
            Assert.DoesNotThrow(() => buffer.WriteData(data), "SharedBuffer writing data failed.");
            return true;
        }

        private bool ReadConsecutiveValues(int startingValue, ref int numberOfValues)
        {
            ArraySegment<byte>? data = null;
            int size = numberOfValues;
            Assert.DoesNotThrow(() => data = buffer.ReadData(size), "SharedBuffer reading data failed.");
            Assert.NotNull(data);
            numberOfValues = data.Value.Count;
            for (int i = 0; i < numberOfValues; ++i)
                if (data.Value.Array[data.Value.Offset + i] != (byte) ((startingValue + i) % byte.MaxValue))
                    return false;
            return true;
        }
    }
}
