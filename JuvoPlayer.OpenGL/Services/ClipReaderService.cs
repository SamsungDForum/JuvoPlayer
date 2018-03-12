using JuvoPlayer;
using JuvoPlayer.Common;
using JuvoPlayer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JuvoPlayer.OpenGL.Services {
    class ClipReaderService
    {
        public static List<Clip> ReadClips(string path)
        {
            var clips = JSONFileReader.DeserializeJsonFile<List<ClipDefinition>>(path).Select(
                o => new Clip() { Image = o.Poster, Description = o.Description, Source = o.Description, Title = o.Title, ClipDetailsHandle = o }
                ).ToList();

            return clips;
        }

        public void Dispose()
        {
        }
    }
}
