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

﻿using JuvoPlayer.Common;
using JuvoPlayer.Subtitles;
using NUnit.Framework;

namespace JuvoPlayer.Tests.UnitTests
{
    [TestFixture]
    class TSSubtitleFormatResolver
    {
        [Test]
        [Category("Positive")]
        public void ResolveByPath_SrtPath_ReturnSrtFormat()
        {
            var subtitleFormatResolver = new SubtitleFormatResolver();

            var format = subtitleFormatResolver.ResolveByPath("subtitles.srt");

            Assert.That(format, Is.EqualTo(SubtitleFormat.Subrip));
        }

        [Test]
        [Category("Negative")]
        public void ResolveByPath_InvalidPath_ReturnInvalidFormat()
        {
            var subtitleFormatResolver = new SubtitleFormatResolver();

            var format = subtitleFormatResolver.ResolveByPath("subtitles.mp4");

            Assert.That(format, Is.EqualTo(SubtitleFormat.Invalid));
        }

        [Test]
        [Category("Positive")]
        public void ResolveByMimeType_WebVttMimeType_ReturnsWebVttFormat()
        {
            var subtitleFormatResolver = new SubtitleFormatResolver();

            var format = subtitleFormatResolver.ResolveByMimeType("text/vtt");

            Assert.That(format, Is.EqualTo(SubtitleFormat.WebVtt));
        }

        [Test]
        [Category("Negative")]
        public void ResolveByMimeType_InvalidMimeType_ReturnsInvalidFormat()
        {
            var subtitleFormatResolver = new SubtitleFormatResolver();

            var format = subtitleFormatResolver.ResolveByMimeType("video/mp4");

            Assert.That(format, Is.EqualTo(SubtitleFormat.Invalid));
        }

        [Test]
        [Category("Positive")]
        public void Resolve_SrtSubtitleInfo_ReturnsSrtFormat()
        {
            var subtitleFormatResolver = new SubtitleFormatResolver();
            var subtitleInfo = new SubtitleInfo()
            {
                Path = "subtitles.srt"
            };

            var format = subtitleFormatResolver.Resolve(subtitleInfo);

            Assert.That(format, Is.EqualTo(SubtitleFormat.Subrip));
        }
    }
}
