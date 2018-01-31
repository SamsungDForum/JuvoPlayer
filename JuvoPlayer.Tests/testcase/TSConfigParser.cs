using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using JuvoPlayer.Common.Logging;
using NUnit.Framework;

namespace JuvoPlayer.Tests
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

            Assert.That(parser.LoggingLevels.Count, Is.EqualTo(expectedValues.Count));

            foreach (var pair in expectedValues)
            {
                Assert.That(parser.LoggingLevels.ContainsKey(pair.Key), Is.True);
                Assert.That(parser.LoggingLevels[pair.Key], Is.EqualTo(pair.Value));
            }
        }

        [Test]
        public void TestInvalidConfig()
        {
            var contents = LoadConfigFile("logging_invalid.config");
            Assert.That(contents, Is.Not.Empty);

            var parser = new ConfigParser(contents);
            Assert.That(parser.LoggingLevels, Is.Empty);
        }

        [Test]
        public void TestConstructorWithNull()
        {
            Assert.Throws<ArgumentNullException>(() => new ConfigParser(null));
        }
    }
}
