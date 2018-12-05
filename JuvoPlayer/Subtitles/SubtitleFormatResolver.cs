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

﻿
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JuvoPlayer.Common;

namespace JuvoPlayer.Subtitles
{
    internal class SubtitleFormatResolver
    {
        internal Dictionary<string, SubtitleFormat> extensionToSubtitleFormat;
        internal Dictionary<string, SubtitleFormat> mimetypeToSubtitleFormat;

        public SubtitleFormatResolver()
        {
            FillExtensionToSubtitleFormatDict();
            FillMimeTypeToSubtitleFormatDict();
        }

        public SubtitleFormat Resolve(SubtitleInfo info)
        {
            // we can extend this logic to resolve subtitle format by mimetype
            return ResolveByPath(info.Path);
        }

        public SubtitleFormat ResolveByPath(string path)
        {
            var extension = Path.GetExtension(path);
            return extensionToSubtitleFormat.ContainsKey(extension)
                ? extensionToSubtitleFormat[extension]
                : SubtitleFormat.Invalid;
        }

        public SubtitleFormat ResolveByMimeType(string mimeType)
        {
            return mimetypeToSubtitleFormat.ContainsKey(mimeType) ? mimetypeToSubtitleFormat[mimeType] : SubtitleFormat.Invalid;
        }

        private void FillExtensionToSubtitleFormatDict()
        {
            extensionToSubtitleFormat = new Dictionary<string, SubtitleFormat>
            {
                [".srt"] = SubtitleFormat.Subrip,
                [".vtt"] = SubtitleFormat.WebVtt
            };
        }

        private void FillMimeTypeToSubtitleFormatDict()
        {
            mimetypeToSubtitleFormat =
                new Dictionary<string, SubtitleFormat>
                {
                    ["application/x-subrip"] = SubtitleFormat.Subrip,
                    ["text/vtt"] = SubtitleFormat.WebVtt
                };
        }
    }
}
