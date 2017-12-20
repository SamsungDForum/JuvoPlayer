using JuvoPlayer.Common;
using JuvoPlayer.FFmpeg;
using System;
using System.IO;
using Tizen.Applications;

namespace JuvoPlayer.Dash
{
    public class DashDataProviderFactory : IDataProviderFactory
    {
        public IDataProvider Create(ClipDefinition clip)
        {
            Tizen.Log.Info("JuvoPlayer", "Create.");
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip), "Clip cannot be null.");
            }

            if (!SupportsClip(clip))
            {
                throw new ArgumentException("Unsupported clip type.");
            }


            IDemuxer demuxer = null;
            IDashClient dashClient = null;
            DashManifest manifest = null;
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
                manifest = new DashManifest(clip.Url);
                dashClient = new DashClient(manifest, sharedBuffer);
                demuxer = new FFmpegDemuxer(sharedBuffer, libPath);
            }
            catch (Exception ex)
            {
                Tizen.Log.Error("JuvoPlayer", ex.Message);
            }
            return new DashDataProvider(dashClient, demuxer, clip, manifest);
        }

        public bool SupportsClip(ClipDefinition clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip), "Clip cannot be null.");
            }
            return string.Equals(clip.Type, "Dash", StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
