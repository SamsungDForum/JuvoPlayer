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
            IDashClient dashClient = null;
            try
            {
                var libPath = Path.Combine(
                    Path.GetDirectoryName(
                        Application.Current.ApplicationInfo.ExecutablePath),
                    "lib");
                var sharedBuffer = new SharedBufferByteArrayQueue();
                dashClient = new DashClient(sharedBuffer);
                demuxer = new FFmpegDemuxer(sharedBuffer, libPath);
            }
            catch (NullReferenceException)
            {
                Tizen.Log.Error(
                    "JuvoPlayer",
                    "Cannot find application executable path.");
            }
            return new DashDataProvider(dashClient, demuxer, clip);
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
