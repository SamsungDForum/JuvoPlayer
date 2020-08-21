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

﻿using System.IO;
using JuvoPlayer.Subtitles;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    class TSParserUtils
    {
        [Test]
        [Category("Positive")]
        public void ParseText_TextHasOneLine_ParsesSuccessfullyWithoutNewLine()
        {
            var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream))
            {
                writer.WriteLine("- How did he do that?");
                writer.Flush();
                memoryStream.Position = 0;

                var utils = CreateParserUtils();
                var text = ParserUtils.ParseText(new StreamReader(memoryStream));
                Assert.That(text, Is.EqualTo("- How did he do that?"));
            }
        }

        [Test]
        [Category("Positive")]
        public void ParseText_TextHasTwoLines_ReturnsTextWithTwoSeparatedLines()
        {
            var memoryStream = new MemoryStream();
            using (var writer = new StreamWriter(memoryStream))
            {
                writer.WriteLine("- How did he do that?");
                writer.WriteLine("- Made him an offer he couldn't refuse.");
                writer.Flush();
                memoryStream.Position = 0;

                var utils = CreateParserUtils();
                var text = ParserUtils.ParseText(new StreamReader(memoryStream));

                using (var reader = new StringReader(text))
                {
                    Assert.That(reader.ReadLine(), Is.EqualTo("- How did he do that?"));
                    Assert.That(reader.ReadLine(), Is.EqualTo("- Made him an offer he couldn't refuse."));
                }
            }
        }

        private ParserUtils CreateParserUtils()
        {
            return new ParserUtils();
        }
    }
}
