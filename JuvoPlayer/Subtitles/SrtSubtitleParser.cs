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

namespace JuvoPlayer.Subtitles
{
    internal class SrtSubtitleParser : ISubtitleParser
    {
        // According to Wikipedia, windows-1252 is default SubRip's text encoding.
        // See https://en.wikipedia.org/wiki/SubRip
        public string DefaultEncoding { get; } = "windows-1252";

        public IEnumerable<Cue> Parse(StreamReader reader)
        {
            try
            {
                return ParseLoop(reader);
            }
            catch (FormatException ex)
            {
                throw new SubtitleParserException("Cannot parse subtitle file", ex);
            }
        }

        private IEnumerable<Cue> ParseLoop(StreamReader reader)
        {
            while (!reader.EndOfStream)
            {
                var line = MoveToFirstNonEmptyLine(reader);
                if (string.IsNullOrEmpty(line)) yield break;

                if (!IsCounterLine(line)) yield break;

                var timeLine = reader.ReadLine();
                var (begin, end) = ParseTimeLine(timeLine);

                var text = ParserUtils.ParseText(reader);

                yield return new Cue {Text = text, Begin = begin, End = end};
            }
        }

        private string MoveToFirstNonEmptyLine(StreamReader reader)
        {
            string line;
            do
            {
                line = reader.ReadLine();
            } while (string.IsNullOrEmpty(line));
            return line;
        }

        internal bool IsCounterLine(string line)
        {
            return Regex.IsMatch(line, @"^\d+$");
        }

        internal (TimeSpan, TimeSpan) ParseTimeLine(string line)
        {
            if (line == null)
                throw new FormatException("Line cannot be null");
            const string timeSeparator = "-->";
            if (!line.Contains(timeSeparator))
                throw new FormatException("Missing separator");
            string[] parts = line.Split(new[] { timeSeparator }, StringSplitOptions.None);
            var beginString = parts[0];
            var endString = parts[1];
            return (ParseTime(beginString), ParseTime(endString));
        }

        internal TimeSpan ParseTime(string timeString)
        {
            if (string.IsNullOrEmpty(timeString))
                throw new FormatException("Invalid time format");

            timeString = timeString.Trim().Replace(',', ':');
            string[] parts = timeString.Split(':');

            if (parts.Length != 4)
                throw new FormatException("Invalid time format");
            int days = 0;
            int hours = Int32.Parse(parts[0]);
            int minutes = Int32.Parse(parts[1]);
            int seconds = Int32.Parse(parts[2]);
            int milliseconds = Int32.Parse(parts[3]);
            return new TimeSpan(days, hours, minutes, seconds, milliseconds);
        }
    }
}
