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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JuvoLogger;
using NUnit.Framework;
using IniParser.Model;
using IniParser.Parser;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSConfigParser
    {
        private string LoadConfigFile(string filename)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var names = assembly.GetManifestResourceNames();
            foreach (var name in names)
            {
                if (!name.Contains(filename)) continue;
                var stream = assembly.GetManifestResourceStream(name);
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            return string.Empty;
        }

        [Test]
        [Category("Positive")]
        public void TestValidConfig()
        {
            var contents = LoadConfigFile("logging_valid.config");
            Assert.That(contents, Is.Not.Empty);

            var parser = new ConfigParser(contents);

            var expectedValues = new Dictionary<string, LogLevel>
            {
                {"JuvoPlayer", LogLevel.Debug},
                {"JuvoPlayer.Common", LogLevel.Warn},
                {"XamarinPlayer.Tizen.TV", LogLevel.Info}
            };

            VerifyConfig(parser, expectedValues);

            var iniParser = new IniDataParser();
            parser = new ConfigParser(iniParser.Parse(contents));

            VerifyConfig(parser, expectedValues);
        }

        private void VerifyConfig(in ConfigParser parser, in Dictionary<string, LogLevel> expectedValues)
        {
            Assert.That(parser.LoggingLevels.Count, Is.EqualTo(expectedValues.Count));

            foreach (var pair in expectedValues)
            {
                Assert.That(parser.LoggingLevels.ContainsKey(pair.Key), Is.True);
                Assert.That(parser.LoggingLevels[pair.Key], Is.EqualTo(pair.Value));
            }
        }

        [Test]
        [Category("Negative")]
        public void TestInvalidConfig()
        {
            var contents = LoadConfigFile("logging_invalid.config");
            Assert.That(contents, Is.Not.Empty);

            var parser = new ConfigParser(contents);
            Assert.That(parser.LoggingLevels, Is.Empty);
        }

        [Test]
        [Category("Negative")]
        public void TestConstructorWithNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigParser(null));
            Assert.Throws<ArgumentNullException>(() => new ConfigParser(default(IniData)));
        }
    }
}
