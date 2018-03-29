
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
