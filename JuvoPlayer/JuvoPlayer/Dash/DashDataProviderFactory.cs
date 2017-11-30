using JuvoPlayer.Common;
using System;
using System.IO;
using Tizen.Applications;

namespace JuvoPlayer.Dash
{
    class DashDataProviderFactory : IDataProviderFactory
    {
        public DashDataProviderFactory()
        {
        }

        public IDataProvider Create(ClipDefinition clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException("Clip", "Clip cannot be null.");
            }

            if (!SupportsClip(clip))
            {
                throw new ArgumentException("Unsupported clip type.");
            }


            IDemuxer demuxer = null;
            try
            {
                var libPath = Path.Combine(
                    Path.GetDirectoryName(
                        Application.Current.ApplicationInfo.ExecutablePath),
                    "lib");
                //var demuxer = new FFmpegDemuxer(sharedBuffer, libPath);
            }
            catch (NullReferenceException ex)
            {
                Tizen.Log.Error(
                    "JuvoPlayer",
                    "Cannot find application executable path.");
            }
            return new DashDataProvider(demuxer, clip);
        }

        public bool SupportsClip(ClipDefinition clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException("Clip", "Clip cannot be null.");
            }
            return clip.Type == "DASH";
        }
    }
}
