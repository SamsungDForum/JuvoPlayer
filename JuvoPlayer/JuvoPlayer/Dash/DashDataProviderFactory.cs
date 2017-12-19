using JuvoPlayer.Common;
using JuvoPlayer.RTSP;
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
            Tizen.Log.Info("JuvoPlayer", "Create.");
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
            string libPath = null;
            try
            {
                libPath = Path.Combine(
                    Path.GetDirectoryName(Path.GetDirectoryName(
                        Application.Current.ApplicationInfo.ExecutablePath)),
                    "lib");
            }
            catch (NullReferenceException)
            {
                Tizen.Log.Error(
                    "JuvoPlayer",
                    "Cannot find application executable path.");
            }
            try
            {
                var sharedBuffer = new SharedBuffer();
                var manifest = new DashManifest(clip.Url);
                dashClient = new DashClient(manifest, null);
                demuxer = new FFmpegDemuxer(null, libPath);
            }
            catch (Exception ex)
            {
                Tizen.Log.Error("JuvoPlayer", ex.Message);
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
