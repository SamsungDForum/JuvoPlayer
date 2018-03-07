using JuvoPlayer;
using JuvoPlayer.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xamarin.Forms;
using XamarinPlayer.Services;
using XamarinPlayer.Tizen.TV.Services;

[assembly: Dependency(typeof(ClipReaderService))]
namespace XamarinPlayer.Tizen.TV.Services
{
    class ClipReaderService : IClipReaderService
    {
        public List<Clip> ReadClips(string path)
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
