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

using JuvoLogger;
using JuvoLogger.Tizen;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.UnitTests
{
    [TestFixture]
    class TSTizenLoggerManager
    {
        private LoggerManager savedInstance;

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
        public void TestDefaultConfigure()
        {
            TizenLoggerManager.Configure();
            LoggerManager manager = LoggerManager.GetInstance();

            ILogger logger = manager.GetLogger("JuvoPlayer");
            Assert.That(logger.IsLevelEnabled(LogLevel.Info), Is.True);
            Assert.That(logger.IsLevelEnabled(LogLevel.Debug), Is.False);
        }
    }
}
