using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using JuvoPlayer.Subtitles;
using NSubstitute;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSWebVttSubtitleParser
    {
        [TestCase(null, Description = "Null time")]
        [TestCase("", Description = "Empty time")]
        [TestCase("0,0,0.0", Description = "Invalid separators")]
        [TestCase("0.0.0.0", Description = "Invalid separators")]
        [TestCase("00:00:00:00.0", Description = "Too many elements")]
        [TestCase("00.0", Description = "Not enough elements")]
        public void ParseTime_TimeFormatIsInvalid_ThrowsFormatException(string invalidTime)
        {
            var parser = CreateWebVttParser();

            Assert.Throws<FormatException>(() => { parser.ParseTime(invalidTime); });
        }

        [TestCase("00:01:18.171", 0, 1, 18, 171)]
        [TestCase("01:18.171", 0, 1, 18, 171)]
        [TestCase("9999:01:18.171", 9999, 1, 18, 171)]
        public void ParseTime_TimeFormatIsValid_ParsesSuccessfully(string validTime, int hours, int minutes,
            int seconds, int milliseconds)
        {
            var parser = CreateWebVttParser();
            var expectedTimeSpan =
                new TimeSpan(0, hours, minutes, seconds, milliseconds);

            var parsedTimeSpan = parser.ParseTime(validTime);

            Assert.That(parsedTimeSpan, Is.EqualTo(expectedTimeSpan));
        }

        [TestCase(null, Description = "Null line")]
        [TestCase("", Description = "Empty line")]
        [TestCase("01:18.171 -> 01:20.171", Description = "Invalid separator")]
        [TestCase("01:18.171 -- 01:20.171", Description = "Invalid separator")]
        public void ParseTimeLine_TimeLineIsInvalid_ThrowsFormatException(string invalidTimeLine)
        {
            var parser = CreateWebVttParser();

            Assert.Throws<FormatException>(() => { parser.ParseTimeLine(invalidTimeLine); });
        }

        [TestCase("01:18.171 --> 01:20.171")]
        [TestCase("01:18.171  -->   01:20.171")]
        [TestCase("01:18.171 --> 01:20.171 position:10%,line-left align:left size:35%")]
        public void ParseTimeLine_TimeLineIsValid_ParsesSuccessfully(string validTimeLine)
        {
            var parser = Substitute.ForPartsOf<WebVttSubtitleParser>();
            var passTimeSpan = TimeSpan.MaxValue;
            var validTimePattern = @"^\d+:\d+\.\d+$";
            parser.ParseTime(Arg.Is<string>(arg => Regex.IsMatch(arg, validTimePattern))).Returns(passTimeSpan);

            (var begin, var end) = parser.ParseTimeLine(validTimeLine);

            Assert.That(begin, Is.EqualTo(passTimeSpan));
            Assert.That(end, Is.EqualTo(passTimeSpan));
        }

        private WebVttSubtitleParser CreateWebVttParser()
        {
            return new WebVttSubtitleParser();
        }
    }
}