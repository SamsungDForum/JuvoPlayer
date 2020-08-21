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

﻿using System;
using System.Collections.Generic;
using System.IO;
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
        class WebVttBuilder
        {
            StringBuilder webvttContents = new StringBuilder();
            public static readonly string CueTimeLine = "00:01:14.815 --> 00:01:18.114 position:10%";

            public override string ToString()
            {
                return webvttContents.ToString();
            }

            public WebVttBuilder AddWebVttHeader()
            {
                webvttContents.AppendLine("WEBVTT");
                webvttContents.AppendLine("");
                return this;
            }

            public WebVttBuilder AddNote()
            {
                webvttContents.AppendLine("NOTE this is a note");
                webvttContents.AppendLine("this has multiple lines");
                webvttContents.AppendLine("");
                return this;
            }

            public WebVttBuilder AddStyle()
            {
                webvttContents.AppendLine("STYLE");
                webvttContents.AppendLine("::cue {");
                webvttContents.AppendLine("background-image: linear-gradient;");
                webvttContents.AppendLine("}");
                webvttContents.AppendLine("");
                return this;
            }

            public WebVttBuilder AddCueTitle()
            {
                webvttContents.AppendLine("1 - this is a titled cue");
                return this;
            }

            public WebVttBuilder AddCueTimeLine()
            {
                webvttContents.AppendLine(CueTimeLine);
                return this;
            }

            public WebVttBuilder AddCueText()
            {
                webvttContents.AppendLine("This is a cue text");
                webvttContents.AppendLine("");
                return this;
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0,0,0.0")]
        [TestCase("00:00:00:00.0")]
        [TestCase("00.0")]
        [Category("Negative")]
        public void ParseTime_TimeFormatIsInvalid_ThrowsFormatException(string invalidTime)
        {
            var parser = CreateWebVttParser();

            Assert.Throws<FormatException>(() => { parser.ParseTime(invalidTime); });
        }

        [TestCase("00:01:18.171", 0, 1, 18, 171)]
        [TestCase("01:18.171", 0, 1, 18, 171)]
        [TestCase("9999:01:18.171", 9999, 1, 18, 171)]
        [Category("Positive")]
        public void ParseTime_TimeFormatIsValid_ParsesSuccessfully(string validTime, int hours, int minutes,
            int seconds, int milliseconds)
        {
            var parser = CreateWebVttParser();
            var expectedTimeSpan =
                new TimeSpan(0, hours, minutes, seconds, milliseconds);

            var parsedTimeSpan = parser.ParseTime(validTime);

            Assert.That(parsedTimeSpan, Is.EqualTo(expectedTimeSpan));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("01:18.171 01:20.171")]
        [TestCase("01:18.171 -> 01:20.171")]
        [TestCase("01:18.171 -- 01:20.171")]
        [TestCase("01:18.171 01:20.171 -->")]
        [Category("Negative")]
        public void ParseTimeLine_TimeLineIsInvalid_ThrowsFormatException(string invalidTimeLine)
        {
            var parser = CreateWebVttParser();

            Assert.Throws<FormatException>(() => { parser.ParseTimeLine(invalidTimeLine); });
        }

        [TestCase("01:18.171 --> 01:20.171")]
        [TestCase("01:18.171  -->   01:20.171")]
        [TestCase("01:18.171 --> 01:20.171 position:10%,line-left align:left size:35%")]
        [Category("Positive")]
        public void ParseTimeLine_TimeLineIsValid_ParsesSuccessfully(string validTimeLine)
        {
            var parser = Substitute.ForPartsOf<WebVttSubtitleParser>();
            var passTimeSpan = TimeSpan.MaxValue;
            var validTimePattern = @"^\d+:\d+\.\d+$";
            parser.ParseTime(Arg.Is<string>(arg => Regex.IsMatch(arg, validTimePattern))).Returns(passTimeSpan);

            var (begin, end) = parser.ParseTimeLine(validTimeLine);

            Assert.That(begin, Is.EqualTo(passTimeSpan));
            Assert.That(end, Is.EqualTo(passTimeSpan));
        }

        [Test]
        [Category("Positive")]
        public void MoveToTimeLine_WebVttHasAllParts_ReturnsFirstTimeLine()
        {
            WebVttBuilder builder = new WebVttBuilder();
            builder.AddWebVttHeader()
                .AddStyle()
                .AddNote()
                .AddCueTitle()
                .AddCueTimeLine()
                .AddCueText();

            using (var stream = CreateStream(builder.ToString()))
            {
                var parser = CreateWebVttParser();
                var line = parser.MoveToTimeLine(new StreamReader(stream));

                Assert.That(line, Is.EqualTo(WebVttBuilder.CueTimeLine));
            }
        }

        [Test]
        [Category("Positive")]
        public void MoveToTimeLine_WebVttHasNoTimeLine_ReturnsNull()
        {
            WebVttBuilder builder = new WebVttBuilder();
            builder.AddWebVttHeader()
                .AddNote()
                .AddStyle();

            using (var stream = CreateStream(builder.ToString()))
            {
                var parser = CreateWebVttParser();
                var line = parser.MoveToTimeLine(new StreamReader(stream));

                Assert.That(line, Is.Null);
            }
        }

        [Test]
        [Category("Positive")]
        public void MoveToTimeLine_WebVttHasTwoTimeLines_ReturnsBoth()
        {
            WebVttBuilder builder = new WebVttBuilder();
            builder.AddWebVttHeader()
                .AddCueTitle()
                .AddCueTimeLine()
                .AddCueText()
                .AddCueTitle()
                .AddCueTimeLine()
                .AddCueText();

            using (var stream = CreateStream(builder.ToString()))
            {
                var parser = CreateWebVttParser();
                var reader = new StreamReader(stream);

                var firstTimeLine = parser.MoveToTimeLine(reader);
                var secondTimeLine = parser.MoveToTimeLine(reader);

                Assert.That(firstTimeLine, Is.EqualTo(WebVttBuilder.CueTimeLine));
                Assert.That(secondTimeLine, Is.EqualTo(WebVttBuilder.CueTimeLine));
            }
        }

        [TestCase("begin &amp; end", "begin & end")]
        [TestCase("begin &lt; end", "begin < end")]
        [TestCase("begin &gt; end", "begin > end")]
        [Category("Positive")]
        public void ParseText_ContainsEscapeSequence_DecodesSuccessfully(string input, string expectedOuput)
        {
            using (var stream = CreateStream(input))
            {
                var parser = CreateWebVttParser();
                var reader = new StreamReader(stream);

                var output = parser.ParseText(reader);

                Assert.That(output, Is.EqualTo(expectedOuput));
            }
        }

        private WebVttSubtitleParser CreateWebVttParser()
        {
            return new WebVttSubtitleParser();
        }

        private Stream CreateStream(string data)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(data);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}