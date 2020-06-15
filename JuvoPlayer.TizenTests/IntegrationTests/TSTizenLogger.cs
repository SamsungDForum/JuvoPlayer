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

using System.Text;
using JuvoLogger;
using JuvoLogger.Tizen;
using JuvoPlayer.Tests.UnitTests;
using NUnit.Framework;

namespace JuvoPlayer.TizenTests.IntegrationTests
{
    [TestFixture]
    public class TSTizenLogger
    {
        [Test]
        public void TestTizenLogger()
        {
            var dummyChannel = "TizenLog";
            var logger = new TizenLogger(dummyChannel, LogLevel.Verbose);
            var loggerClient = new LoggerClient(logger);
            loggerClient.Func();
        }

        [Test]
        public void LongLog_SingleLongLine_SplitsByMaxLineSize()
        {
            var logger = new TizenLogger("TizenLog", LogLevel.Verbose);
            var stringBuilder = new StringBuilder();
            for (var i = 0; i < 2000; i++)
                stringBuilder.Append($"{i % 10}");

            var longLine = stringBuilder.ToString();
            logger.Debug(longLine);
        }

        [Test]
        public void LongLog_MultipleShortLines_SplitsByNewLineCharacter()
        {
            var logger = new TizenLogger("TizenLog", LogLevel.Verbose);
            var stringBuilder = new StringBuilder();
            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 300; j++)
                    stringBuilder.Append($"{j % 10}");
                stringBuilder.Append("\n");
            }

            var longLines = stringBuilder.ToString();
            logger.Debug(longLines);
        }

        [Test]
        public void LongLog_MultipleLongLines_SplitsByNewLineCharacterAndMaxLineSize()
        {
            var logger = new TizenLogger("TizenLog", LogLevel.Verbose);
            var stringBuilder = new StringBuilder();
            for (var i = 0; i < 2; i++)
            {
                for (var j = 0; j < 900; j++)
                    stringBuilder.Append($"{j % 10}");
                stringBuilder.Append("\n");
            }

            var longLines = stringBuilder.ToString();
            logger.Debug(longLines);
        }
    }
}
