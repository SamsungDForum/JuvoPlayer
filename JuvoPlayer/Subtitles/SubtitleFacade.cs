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
using System.IO;
using System.Text;
using JuvoPlayer.Common;
using JuvoPlayer.Utils;

namespace JuvoPlayer.Subtitles
{
    class SubtitleFacade
    {

        public CuesMap LoadSubtitles(SubtitleInfo subtitleInfo)
        {
            var stream = LoadSubtitle(subtitleInfo.Path);
            var parser = CreateSubtitleParser(subtitleInfo);
            return FillCuesMap(parser, stream, subtitleInfo.Encoding);
        }

        private ISubtitleParser CreateSubtitleParser(SubtitleInfo subtitleInfo)
        {
            var resolver = new SubtitleFormatResolver();
            var format = resolver.Resolve(subtitleInfo);
            if (format == SubtitleFormat.Invalid)
                throw new ArgumentException("Unsupported subtitle format");

            var factory = new SubtitleParserFactory();
            var parser = factory.CreateParser(format);
            if (parser == null)
                throw new ArgumentException("Unsupported subtitle format");
            return parser;
        }

        private Stream LoadSubtitle(string path)
        {
            var resourceLoader = new ResourceLoader();
            var stream = resourceLoader.Load(path);
            if (stream == null)
                throw new ArgumentException("Cannot load " + path);
            return stream;
        }

        private CuesMap FillCuesMap(ISubtitleParser parser, Stream stream, string encoding)
        {
            if (string.IsNullOrEmpty(encoding))
            {
                encoding = parser.DefaultEncoding;
            }

            var cuesMap = new CuesMap();
            using (var reader = new StreamReader(stream, Encoding.GetEncoding(encoding)))
            {
                foreach (var cue in parser.Parse(reader))
                {
                    cuesMap.Put(cue);
                }
            }

            return cuesMap;
        }
    }
}
