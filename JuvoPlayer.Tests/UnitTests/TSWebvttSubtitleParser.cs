using System;
using System.Collections.Generic;
using System.Text;
using JuvoPlayer.Subtitles;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSWebVttSubtitleParser
    {

        [TestCase(null, Description = "Null time")]
        [TestCase("", Description = "Empty time")]
        [TestCase("0,0,0.0", Description = "Invalid separators")]
        public void ParseTime_TimeFormatIsInvalid_ThrowsFormatException(string invalidTimeToParse)
        {
            var parser = CreateWebVttParser();

            Assert.Throws<FormatException>(() => { parser.ParseTime(invalidTimeToParse);});
        }

        [TestCase("00:01:18.171", 0, 1, 18, 171)]
        [TestCase("01:18.171", 0, 1, 18, 171)]
        [TestCase("9999:01:18.171", 9999, 1, 18, 171)]
        public void ParseTime_TimeFormatIsValid_ParsesSuccessfully(string validTimeToParse, int expectedHours, int expectedMinutes, int expectedSeconds, int expectedMilliseconds)
        {
            var parser = CreateWebVttParser();
            var expectedTimeSpan =
                new TimeSpan(0, expectedHours, expectedMinutes, expectedSeconds, expectedMilliseconds);

            var parsedTimeSpan = parser.ParseTime(validTimeToParse);

            Assert.That(parsedTimeSpan, Is.EqualTo(expectedTimeSpan));
        }

        private WebVttSubtitleParser CreateWebVttParser()
        {
            return new WebVttSubtitleParser();
        }
    }
}
