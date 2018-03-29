using System;
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
