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
using System.IO;
using System.Reflection;
using JuvoLogger;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSLoggerManager
    {
        private LoggerManager savedInstance;
        LoggerBase CreateLogger(string channel, LogLevel level) => new DummyLogger(channel, level);

        [SetUp]
        public void SetUp()
        {
            savedInstance = LoggerManager.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            LoggerManager.RestoreForTests(savedInstance);
        }

        [Test]
        [Category("Positive")]
        public void TestConfigureWithDefaultLevels()
        {
            LoggerManager.Configure(CreateLogger);
            LoggerManager manager = LoggerManager.GetInstance();

            ILogger firstLogger = manager.GetLogger("LogChannel1");
            Assert.That(firstLogger.IsLevelEnabled(LogLevel.Debug), Is.True);

            ILogger secondLogger = manager.GetLogger("LogChannel2");
            Assert.That(secondLogger.IsLevelEnabled(LogLevel.Debug), Is.True);
        }

        [Test]
        [Category("Positive")]
        public void TestConfigureWithValidConfigFile()
        {
            var contents = LoadConfig("logging_valid.config");
            LoggerManager.Configure(contents, CreateLogger);
            LoggerManager manager = LoggerManager.GetInstance();

            ILogger juvoPlayerLogger = manager.GetLogger("JuvoPlayer");
            Assert.That(juvoPlayerLogger.IsLevelEnabled(LogLevel.Debug), Is.True);
            Assert.That(juvoPlayerLogger.IsLevelEnabled(LogLevel.Verbose), Is.False);

            ILogger juvoPlayerCommonLogger = manager.GetLogger("JuvoPlayer.Common");
            Assert.That(juvoPlayerCommonLogger.IsLevelEnabled(LogLevel.Warn), Is.True);
            Assert.That(juvoPlayerCommonLogger.IsLevelEnabled(LogLevel.Debug), Is.False);
        }

        [Test]
        [Category("Negative")]
        public void TestConfigureWithNull()
        {
            // configData cannot be null
            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure((string) null, null));
            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure((string) null, null));
            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure((string) null, CreateLogger));

            // configStream cannot be null
            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure((Stream) null, null));
            Assert.Throws<ArgumentNullException>(() => LoggerManager.Configure((Stream) null, CreateLogger));

            Assert.DoesNotThrow(() => LoggerManager.Configure(null));
            Assert.DoesNotThrow(() => LoggerManager.Configure(string.Empty, null));
            Assert.DoesNotThrow(() => LoggerManager.GetInstance());
        }

        [Test]
        [Category("Positive")]
        public void TestConfigureMultipleTimes()
        {
            var contents = LoadConfig("logging_valid.config");
            LoggerManager.Configure(contents, CreateLogger);
            Assert.That(LoggerManager.GetInstance(), Is.Not.Null);

            var channelName = "JuvoPlayer.Common";

            ILogger logger = LoggerManager.GetInstance().GetLogger(channelName);
            Assert.That(logger.IsLevelEnabled(LogLevel.Warn), Is.True);
            Assert.That(logger.IsLevelEnabled(LogLevel.Info), Is.False);

            LoggerManager.Configure(null);

            ILogger reconfiguredLogger = LoggerManager.GetInstance().GetLogger(channelName);
            Assert.That(reconfiguredLogger, Is.SameAs(logger));
            Assert.That(logger.IsLevelEnabled(LogLevel.Debug), Is.True);
        }

        [Test]
        [Category("Positive")]
        public void TestGetInstanceBeforeConfigure()
        {
            Assert.DoesNotThrow(() => LoggerManager.GetInstance());
        }

        private static string LoadConfig(string filename)
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.Contains(filename)) continue;
                using (var reader = new StreamReader(assembly.GetManifestResourceStream(name)))
                {
                    return reader.ReadToEnd();
                }
            }

            return null;
        }
    }
}

