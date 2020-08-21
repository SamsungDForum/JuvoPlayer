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
using JuvoLogger;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSLoggerBase
    {
        [Test]
        [Category("Positive")]
        public void TestLoggerBase()
        {
            var dummyChannel = "channel";
            foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
            {
                var logger = new DummyLogger(dummyChannel, level);
                var loggerClient = new LoggerClient(logger);
                loggerClient.Func();

                var expectedLogsCount = (int) level + 1;
                Assert.That(logger.Logs.Count, Is.EqualTo(expectedLogsCount));

                foreach (var log in logger.Logs)
                {
                    // LoggerBase returns full path and subclasses must resolve file name manually.
                    // This is needed because TizenLogger requires full path.
                    Assert.That(log.File, Does.EndWith(LoggerClient.FileName));
                    Assert.That(log.Method, Is.EqualTo(nameof(loggerClient.Func)));
                    Assert.That(log.Message, Is.EqualTo(LoggerClient.LogMessage));
                }

                for (var index = 0; index < expectedLogsCount; ++index)
                {
                    var lineNumber = LoggerClient.FuncFirstLineNumber + index;
                    Assert.That(logger.Logs[index].Line, Is.EqualTo(lineNumber));
                }
            }
        }

        [Test]
        [Category("Positive")]
        public void TestConsoleLogger()
        {
            var dummyChannel = "channel";
            var logger = new ConsoleLogger(dummyChannel, LogLevel.Verbose);
            var loggerClient = new LoggerClient(logger);
            loggerClient.Func();
        }

        [Test]
        [Category("Negative")]
        public void TestConstructorWithNull()
        {
            Assert.Throws<ArgumentNullException>(() => new DummyLogger(null, LogLevel.Fatal));
        }

        [Test]
        [Category("Negative")]
        public void TestInvalidLogLevel()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DummyLogger("channel", (LogLevel) int.MinValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DummyLogger("channel", (LogLevel) int.MaxValue));
        }
    }
}
